/*
** SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008) 
** Copyright (C) 2011 Silicon Graphics, Inc.
** All Rights Reserved.
**
** Permission is hereby granted, free of charge, to any person obtaining a copy
** of this software and associated documentation files (the "Software"), to deal
** in the Software without restriction, including without limitation the rights
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
** of the Software, and to permit persons to whom the Software is furnished to do so,
** subject to the following conditions:
** 
** The above copyright notice including the dates of first publication and either this
** permission notice or a reference to http://oss.sgi.com/projects/FreeB/ shall be
** included in all copies or substantial portions of the Software. 
**
** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
** INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
** PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL SILICON GRAPHICS, INC.
** BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
** TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
** OR OTHER DEALINGS IN THE SOFTWARE.
** 
** Except as contained in this notice, the name of Silicon Graphics, Inc. shall not
** be used in advertising or otherwise to promote the sale, use or other dealings in
** this Software without prior written authorization from Silicon Graphics, Inc.
*/
/*
** Original Author: Eric Veach, July 1994.
** libtess2: Mikko Mononen, http://code.google.com/p/libtess2/.
** LibTessDotNet: Remi Gillig, https://github.com/speps/LibTessDotNet
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LibTessDotNet
{
    public enum WindingRule
    {
        EvenOdd,
        NonZero,
        Positive,
        Negative,
        AbsGeqTwo
    }

    public enum ElementType
    {
        Polygons,
        ConnectedPolygons,
        BoundaryContours
    }

    public enum ContourOrientation
    {
        Original,
        Clockwise,
        CounterClockwise
    }

    public struct ContourVertex
    {
        public Vec3 Position;
        public object Data;

        public override string ToString()
        {
            return string.Format("{0}, {1}", Position, Data);
        }
    }

    public delegate object CombineCallback(Vec3 position, object[] data, float[] weights);

    internal class Dict<TValue> where TValue : class
    {
        public class Node
        {
            internal TValue _key;
            internal Node _prev, _next;

            public TValue Key { get { return _key; } }
            public Node Prev { get { return _prev; } }
            public Node Next { get { return _next; } }
        }

        public delegate bool LessOrEqual(TValue lhs, TValue rhs);

        private LessOrEqual _leq;
        Node _head;

        public Dict(LessOrEqual leq)
        {
            _leq = leq;

            _head = new Node { _key = null };
            _head._prev = _head;
            _head._next = _head;
        }

        public Node Insert(TValue key)
        {
            return InsertBefore(_head, key);
        }

        public Node InsertBefore(Node node, TValue key)
        {
            do {
                node = node._prev;
            } while (node._key != null && !_leq(node._key, key));

            var newNode = new Node { _key = key };
            newNode._next = node._next;
            node._next._prev = newNode;
            newNode._prev = node;
            node._next = newNode;

            return newNode;
        }

        public Node Find(TValue key)
        {
            var node = _head;
            do {
                node = node._next;
            } while (node._key != null && !_leq(key, node._key));
            return node;
        }

        public Node Min()
        {
            return _head._next;
        }

        public void Remove(Node node)
        {
            node._next._prev = node._prev;
            node._prev._next = node._next;
        }
    }
    
    internal static class Geom
    {
        public static bool IsWindingInside(WindingRule rule, int n)
        {
            switch (rule)
            {
                case WindingRule.EvenOdd:
                    return (n & 1) == 1;
                case WindingRule.NonZero:
                    return n != 0;
                case WindingRule.Positive:
                    return n > 0;
                case WindingRule.Negative:
                    return n < 0;
                case WindingRule.AbsGeqTwo:
                    return n >= 2 || n <= -2;
            }
            throw new Exception("Wrong winding rule");
        }

        public static bool VertCCW(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            return (u._s * (v._t - w._t) + v._s * (w._t - u._t) + w._s * (u._t - v._t)) >= 0.0f;
        }
        public static bool VertEq(MeshUtils.Vertex lhs, MeshUtils.Vertex rhs)
        {
            return lhs._s == rhs._s && lhs._t == rhs._t;
        }
        public static bool VertLeq(MeshUtils.Vertex lhs, MeshUtils.Vertex rhs)
        {
            return (lhs._s < rhs._s) || (lhs._s == rhs._s && lhs._t <= rhs._t);
        }

        /// <summary>
        /// Given three vertices u,v,w such that VertLeq(u,v) && VertLeq(v,w),
        /// evaluates the t-coord of the edge uw at the s-coord of the vertex v.
        /// Returns v->t - (uw)(v->s), ie. the signed distance from uw to v.
        /// If uw is vertical (and thus passes thru v), the result is zero.
        /// 
        /// The calculation is extremely accurate and stable, even when v
        /// is very close to u or w.  In particular if we set v->t = 0 and
        /// let r be the negated result (this evaluates (uw)(v->s)), then
        /// r is guaranteed to satisfy MIN(u->t,w->t) <= r <= MAX(u->t,w->t).
        /// </summary>
        public static float EdgeEval(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(VertLeq(u, v) && VertLeq(v, w));

            var gapL = v._s - u._s;
            var gapR = w._s - v._s;

            if (gapL + gapR > 0.0f)
            {
                if (gapL < gapR)
                {
                    return (v._t - u._t) + (u._t - w._t) * (gapL / (gapL + gapR));
                }
                else
                {
                    return (v._t - w._t) + (w._t - u._t) * (gapR / (gapL + gapR));
                }
            }
            /* vertical line */
            return 0;
        }

        /// <summary>
        /// Returns a number whose sign matches EdgeEval(u,v,w) but which
        /// is cheaper to evaluate. Returns > 0, == 0 , or < 0
        /// as v is above, on, or below the edge uw.
        /// </summary>
        public static float EdgeSign(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(VertLeq(u, v) && VertLeq(v, w));

            var gapL = v._s - u._s;
            var gapR = w._s - v._s;

            if (gapL + gapR > 0.0f)
            {
                return (v._t - w._t) * gapL + (v._t - u._t) * gapR;
            }
            /* vertical line */
            return 0;
        }

        public static bool TransLeq(MeshUtils.Vertex lhs, MeshUtils.Vertex rhs)
        {
            return (lhs._t < rhs._t) || (lhs._t == rhs._t && lhs._s <= rhs._s);
        }

        public static float TransEval(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(TransLeq(u, v) && TransLeq(v, w));

            var gapL = v._t - u._t;
            var gapR = w._t - v._t;

            if (gapL + gapR > 0.0f)
            {
                if (gapL < gapR)
                {
                    return (v._s - u._s) + (u._s - w._s) * (gapL / (gapL + gapR));
                }
                else
                {
                    return (v._s - w._s) + (w._s - u._s) * (gapR / (gapL + gapR));
                }
            }
            /* vertical line */
            return 0;
        }

        public static float TransSign(MeshUtils.Vertex u, MeshUtils.Vertex v, MeshUtils.Vertex w)
        {
            Debug.Assert(TransLeq(u, v) && TransLeq(v, w));

            var gapL = v._t - u._t;
            var gapR = w._t - v._t;

            if (gapL + gapR > 0.0f)
            {
                return (v._s - w._s) * gapL + (v._s - u._s) * gapR;
            }
            /* vertical line */
            return 0;
        }

        public static bool EdgeGoesLeft(MeshUtils.Edge e)
        {
            return VertLeq(e._Dst, e._Org);
        }

        public static bool EdgeGoesRight(MeshUtils.Edge e)
        {
            return VertLeq(e._Org, e._Dst);
        }

        public static float VertL1dist(MeshUtils.Vertex u, MeshUtils.Vertex v)
        {
            return Math.Abs(u._s - v._s) + Math.Abs(u._t - v._t);
        }

        public static void AddWinding(MeshUtils.Edge eDst, MeshUtils.Edge eSrc)
        {
            eDst._winding += eSrc._winding;
            eDst._Sym._winding += eSrc._Sym._winding;
        }

        public static float Interpolate(float a, float x, float b, float y)
        {
            if (a < 0.0f)
            {
                a = 0.0f;
            }
            if (b < 0.0f)
            {
                b = 0.0f;
            }
            return ((a <= b) ? ((b == 0.0f) ? ((x+y) / 2.0f)
                    : (x + (y-x) * (a/(a+b))))
                    : (y + (x-y) * (b/(a+b))));
        }

        static void Swap(ref MeshUtils.Vertex a, ref MeshUtils.Vertex b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }

        /// <summary>
        /// Given edges (o1,d1) and (o2,d2), compute their point of intersection.
        /// The computed point is guaranteed to lie in the intersection of the
        /// bounding rectangles defined by each edge.
        /// </summary>
        public static void EdgeIntersect(MeshUtils.Vertex o1, MeshUtils.Vertex d1, MeshUtils.Vertex o2, MeshUtils.Vertex d2, MeshUtils.Vertex v)
        {
            // This is certainly not the most efficient way to find the intersection
            // of two line segments, but it is very numerically stable.
            // 
            // Strategy: find the two middle vertices in the VertLeq ordering,
            // and interpolate the intersection s-value from these.  Then repeat
            // using the TransLeq ordering to find the intersection t-value.

            if (!VertLeq(o1, d1)) { Swap(ref o1, ref d1); }
            if (!VertLeq(o2, d2)) { Swap(ref o2, ref d2); }
            if (!VertLeq(o1, o2)) { Swap(ref o1, ref o2); Swap(ref d1, ref d2); }

            if (!VertLeq(o2, d1))
            {
                // Technically, no intersection -- do our best
                v._s = (o2._s + d1._s) / 2.0f;
            }
            else if (VertLeq(d1, d2))
            {
                // Interpolate between o2 and d1
                var z1 = EdgeEval(o1, o2, d1);
                var z2 = EdgeEval(o2, d1, d2);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._s = Interpolate(z1, o2._s, z2, d1._s);
            }
            else
            {
                // Interpolate between o2 and d2
                var z1 = EdgeSign(o1, o2, d1);
                var z2 = -EdgeSign(o1, d2, d1);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._s = Interpolate(z1, o2._s, z2, d2._s);
            }

            // Now repeat the process for t

            if (!TransLeq(o1, d1)) { Swap(ref o1, ref d1); }
            if (!TransLeq(o2, d2)) { Swap(ref o2, ref d2); }
            if (!TransLeq(o1, o2)) { Swap(ref o1, ref o2); Swap(ref d1, ref d2); }

            if (!TransLeq(o2, d1))
            {
                // Technically, no intersection -- do our best
                v._t = (o2._t + d1._t) / 2.0f;
            }
            else if (TransLeq(d1, d2))
            {
                // Interpolate between o2 and d1
                var z1 = TransEval(o1, o2, d1);
                var z2 = TransEval(o2, d1, d2);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._t = Interpolate(z1, o2._t, z2, d1._t);
            }
            else
            {
                // Interpolate between o2 and d2
                var z1 = TransSign(o1, o2, d1);
                var z2 = -TransSign(o1, d2, d1);
                if (z1 + z2 < 0.0f)
                {
                    z1 = -z1;
                    z2 = -z2;
                }
                v._t = Interpolate(z1, o2._t, z2, d2._t);
            }
        }
    }
    
    internal class Mesh : MeshUtils.Pooled<Mesh>
    {
        internal MeshUtils.Vertex _vHead;
        internal MeshUtils.Face _fHead;
        internal MeshUtils.Edge _eHead, _eHeadSym;

        public Mesh()
        {
            var v = _vHead = MeshUtils.Vertex.Create();
            var f = _fHead = MeshUtils.Face.Create();

            var pair = MeshUtils.EdgePair.Create();
            var e = _eHead = pair._e;
            var eSym = _eHeadSym = pair._eSym;

            v._next = v._prev = v;
            v._anEdge = null;

            f._next = f._prev = f;
            f._anEdge = null;
            f._trail = null;
            f._marked = false;
            f._inside = false;

            e._next = e;
            e._Sym = eSym;
            e._Onext = null;
            e._Lnext = null;
            e._Org = null;
            e._Lface = null;
            e._winding = 0;
            e._activeRegion = null;

            eSym._next = eSym;
            eSym._Sym = e;
            eSym._Onext = null;
            eSym._Lnext = null;
            eSym._Org = null;
            eSym._Lface = null;
            eSym._winding = 0;
            eSym._activeRegion = null;
        }

        public override void Reset()
        {
            _vHead = null;
            _fHead = null;
            _eHead = _eHeadSym = null;
        }

        public override void OnFree()
        {
            for (MeshUtils.Face f = _fHead._next, fNext = _fHead; f != _fHead; f = fNext)
            {
                fNext = f._next;
                f.Free();
            }
            for (MeshUtils.Vertex v = _vHead._next, vNext = _vHead; v != _vHead; v = vNext)
            {
                vNext = v._next;
                v.Free();
            }
            for (MeshUtils.Edge e = _eHead._next, eNext = _eHead; e != _eHead; e = eNext)
            {
                eNext = e._next;
                e.Free();
            }
        }

        /// <summary>
        /// Creates one edge, two vertices and a loop (face).
        /// The loop consists of the two new half-edges.
        /// </summary>
        public MeshUtils.Edge MakeEdge()
        {
            var e = MeshUtils.MakeEdge(_eHead);

            MeshUtils.MakeVertex(e, _vHead);
            MeshUtils.MakeVertex(e._Sym, _vHead);
            MeshUtils.MakeFace(e, _fHead);

            return e;
        }

        /// <summary>
        /// Splice is the basic operation for changing the
        /// mesh connectivity and topology.  It changes the mesh so that
        ///     eOrg->Onext = OLD( eDst->Onext )
        ///     eDst->Onext = OLD( eOrg->Onext )
        /// where OLD(...) means the value before the meshSplice operation.
        /// 
        /// This can have two effects on the vertex structure:
        ///  - if eOrg->Org != eDst->Org, the two vertices are merged together
        ///  - if eOrg->Org == eDst->Org, the origin is split into two vertices
        /// In both cases, eDst->Org is changed and eOrg->Org is untouched.
        /// 
        /// Similarly (and independently) for the face structure,
        ///  - if eOrg->Lface == eDst->Lface, one loop is split into two
        ///  - if eOrg->Lface != eDst->Lface, two distinct loops are joined into one
        /// In both cases, eDst->Lface is changed and eOrg->Lface is unaffected.
        /// 
        /// Some special cases:
        /// If eDst == eOrg, the operation has no effect.
        /// If eDst == eOrg->Lnext, the new face will have a single edge.
        /// If eDst == eOrg->Lprev, the old face will have a single edge.
        /// If eDst == eOrg->Onext, the new vertex will have a single edge.
        /// If eDst == eOrg->Oprev, the old vertex will have a single edge.
        /// </summary>
        public void Splice(MeshUtils.Edge eOrg, MeshUtils.Edge eDst)
        {
            if (eOrg == eDst)
            {
                return;
            }

            bool joiningVertices = false;
            if (eDst._Org != eOrg._Org)
            {
                // We are merging two disjoint vertices -- destroy eDst->Org
                joiningVertices = true;
                MeshUtils.KillVertex(eDst._Org, eOrg._Org);
            }
            bool joiningLoops = false;
            if (eDst._Lface != eOrg._Lface)
            {
                // We are connecting two disjoint loops -- destroy eDst->Lface
                joiningLoops = true;
                MeshUtils.KillFace(eDst._Lface, eOrg._Lface);
            }

            // Change the edge structure
            MeshUtils.Splice(eDst, eOrg);

            if (!joiningVertices)
            {
                // We split one vertex into two -- the new vertex is eDst->Org.
                // Make sure the old vertex points to a valid half-edge.
                MeshUtils.MakeVertex(eDst, eOrg._Org);
                eOrg._Org._anEdge = eOrg;
            }
            if (!joiningLoops)
            {
                // We split one loop into two -- the new loop is eDst->Lface.
                // Make sure the old face points to a valid half-edge.
                MeshUtils.MakeFace(eDst, eOrg._Lface);
                eOrg._Lface._anEdge = eOrg;
            }
        }

        /// <summary>
        /// Removes the edge eDel. There are several cases:
        /// if (eDel->Lface != eDel->Rface), we join two loops into one; the loop
        /// eDel->Lface is deleted. Otherwise, we are splitting one loop into two;
        /// the newly created loop will contain eDel->Dst. If the deletion of eDel
        /// would create isolated vertices, those are deleted as well.
        /// </summary>
        public void Delete(MeshUtils.Edge eDel)
        {
            var eDelSym = eDel._Sym;

            // First step: disconnect the origin vertex eDel->Org.  We make all
            // changes to get a consistent mesh in this "intermediate" state.

            bool joiningLoops = false;
            if (eDel._Lface != eDel._Rface)
            {
                // We are joining two loops into one -- remove the left face
                joiningLoops = true;
                MeshUtils.KillFace(eDel._Lface, eDel._Rface);
            }

            if (eDel._Onext == eDel)
            {
                MeshUtils.KillVertex(eDel._Org, null);
            }
            else
            {
                // Make sure that eDel->Org and eDel->Rface point to valid half-edges
                eDel._Rface._anEdge = eDel._Oprev;
                eDel._Org._anEdge = eDel._Onext;

                MeshUtils.Splice(eDel, eDel._Oprev);

                if (!joiningLoops)
                {
                    // We are splitting one loop into two -- create a new loop for eDel.
                    MeshUtils.MakeFace(eDel, eDel._Lface);
                }
            }

            // Claim: the mesh is now in a consistent state, except that eDel->Org
            // may have been deleted.  Now we disconnect eDel->Dst.

            if (eDelSym._Onext == eDelSym)
            {
                MeshUtils.KillVertex(eDelSym._Org, null);
                MeshUtils.KillFace(eDelSym._Lface, null);
            }
            else
            {
                // Make sure that eDel->Dst and eDel->Lface point to valid half-edges
                eDel._Lface._anEdge = eDelSym._Oprev;
                eDelSym._Org._anEdge = eDelSym._Onext;
                MeshUtils.Splice(eDelSym, eDelSym._Oprev);
            }

            // Any isolated vertices or faces have already been freed.
            MeshUtils.KillEdge(eDel);
        }

        /// <summary>
        /// Creates a new edge such that eNew == eOrg.Lnext and eNew.Dst is a newly created vertex.
        /// eOrg and eNew will have the same left face.
        /// </summary>
        public MeshUtils.Edge AddEdgeVertex(MeshUtils.Edge eOrg)
        {
            var eNew = MeshUtils.MakeEdge(eOrg);
            var eNewSym = eNew._Sym;

            // Connect the new edge appropriately
            MeshUtils.Splice(eNew, eOrg._Lnext);

            // Set vertex and face information
            eNew._Org = eOrg._Dst;
            MeshUtils.MakeVertex(eNewSym, eNew._Org);
            eNew._Lface = eNewSym._Lface = eOrg._Lface;

            return eNew;
        }

        /// <summary>
        /// Splits eOrg into two edges eOrg and eNew such that eNew == eOrg.Lnext.
        /// The new vertex is eOrg.Dst == eNew.Org.
        /// eOrg and eNew will have the same left face.
        /// </summary>
        public MeshUtils.Edge SplitEdge(MeshUtils.Edge eOrg)
        {
            var eTmp = AddEdgeVertex(eOrg);
            var eNew = eTmp._Sym;

            // Disconnect eOrg from eOrg->Dst and connect it to eNew->Org
            MeshUtils.Splice(eOrg._Sym, eOrg._Sym._Oprev);
            MeshUtils.Splice(eOrg._Sym, eNew);

            // Set the vertex and face information
            eOrg._Dst = eNew._Org;
            eNew._Dst._anEdge = eNew._Sym; // may have pointed to eOrg->Sym
            eNew._Rface = eOrg._Rface;
            eNew._winding = eOrg._winding; // copy old winding information
            eNew._Sym._winding = eOrg._Sym._winding;

            return eNew;
        }

        /// <summary>
        /// Creates a new edge from eOrg->Dst to eDst->Org, and returns the corresponding half-edge eNew.
        /// If eOrg->Lface == eDst->Lface, this splits one loop into two,
        /// and the newly created loop is eNew->Lface.  Otherwise, two disjoint
        /// loops are merged into one, and the loop eDst->Lface is destroyed.
        /// 
        /// If (eOrg == eDst), the new face will have only two edges.
        /// If (eOrg->Lnext == eDst), the old face is reduced to a single edge.
        /// If (eOrg->Lnext->Lnext == eDst), the old face is reduced to two edges.
        /// </summary>
        public MeshUtils.Edge Connect(MeshUtils.Edge eOrg, MeshUtils.Edge eDst)
        {
            var eNew = MeshUtils.MakeEdge(eOrg);
            var eNewSym = eNew._Sym;

            bool joiningLoops = false;
            if (eDst._Lface != eOrg._Lface)
            {
                // We are connecting two disjoint loops -- destroy eDst->Lface
                joiningLoops = true;
                MeshUtils.KillFace(eDst._Lface, eOrg._Lface);
            }

            // Connect the new edge appropriately
            MeshUtils.Splice(eNew, eOrg._Lnext);
            MeshUtils.Splice(eNewSym, eDst);

            // Set the vertex and face information
            eNew._Org = eOrg._Dst;
            eNewSym._Org = eDst._Org;
            eNew._Lface = eNewSym._Lface = eOrg._Lface;

            // Make sure the old face points to a valid half-edge
            eOrg._Lface._anEdge = eNewSym;

            if (!joiningLoops)
            {
                MeshUtils.MakeFace(eNew, eOrg._Lface);
            }

            return eNew;
        }

        /// <summary>
        /// Destroys a face and removes it from the global face list. All edges of
        /// fZap will have a NULL pointer as their left face. Any edges which
        /// also have a NULL pointer as their right face are deleted entirely
        /// (along with any isolated vertices this produces).
        /// An entire mesh can be deleted by zapping its faces, one at a time,
        /// in any order. Zapped faces cannot be used in further mesh operations!
        /// </summary>
        public void ZapFace(MeshUtils.Face fZap)
        {
            var eStart = fZap._anEdge;

            // walk around face, deleting edges whose right face is also NULL
            var eNext = eStart._Lnext;
            MeshUtils.Edge e, eSym;
            do {
                e = eNext;
                eNext = e._Lnext;

                e._Lface = null;
                if (e._Rface == null)
                {
                    // delete the edge -- see TESSmeshDelete above

                    if (e._Onext == e)
                    {
                        MeshUtils.KillVertex(e._Org, null);
                    }
                    else
                    {
                        // Make sure that e._Org points to a valid half-edge
                        e._Org._anEdge = e._Onext;
                        MeshUtils.Splice(e, e._Oprev);
                    }
                    eSym = e._Sym;
                    if (eSym._Onext == eSym)
                    {
                        MeshUtils.KillVertex(eSym._Org, null);
                    }
                    else
                    {
                        // Make sure that eSym._Org points to a valid half-edge
                        eSym._Org._anEdge = eSym._Onext;
                        MeshUtils.Splice(eSym, eSym._Oprev);
                    }
                    MeshUtils.KillEdge(e);
                }
            } while (e != eStart);

            /* delete from circular doubly-linked list */
            var fPrev = fZap._prev;
            var fNext = fZap._next;
            fNext._prev = fPrev;
            fPrev._next = fNext;

            fZap.Free();
        }

        public void MergeConvexFaces(int maxVertsPerFace)
        {
            for (var f = _fHead._next; f != _fHead; f = f._next)
            {
                // Skip faces which are outside the result
                if (!f._inside)
                {
                    continue;
                }

                var eCur = f._anEdge;
                var vStart = eCur._Org;

                while (true)
                {
                    var eNext = eCur._Lnext;
                    var eSym = eCur._Sym;

                    if (eSym != null && eSym._Lface != null && eSym._Lface._inside)
                    {
                        // Try to merge the neighbour faces if the resulting polygons
                        // does not exceed maximum number of vertices.
                        int curNv = f.VertsCount;
                        int symNv = eSym._Lface.VertsCount;
                        if ((curNv + symNv - 2) <= maxVertsPerFace)
                        {
                            // Merge if the resulting poly is convex.
                            if (Geom.VertCCW(eCur._Lprev._Org, eCur._Org, eSym._Lnext._Lnext._Org) &&
                                Geom.VertCCW(eSym._Lprev._Org, eSym._Org, eCur._Lnext._Lnext._Org))
                            {
                                eNext = eSym._Lnext;
                                Delete(eSym);
                                eCur = null;
                            }
                        }
                    }

                    if (eCur != null && eCur._Lnext._Org == vStart)
                        break;

                    // Continue to next edge.
                    eCur = eNext;
                }
            }
        }

        [Conditional("DEBUG")]
        public void Check()
        {
            MeshUtils.Edge e;

            MeshUtils.Face fPrev = _fHead, f;
            for (fPrev = _fHead; (f = fPrev._next) != _fHead; fPrev = f)
            {
                e = f._anEdge;
                do {
                    Debug.Assert(e._Sym != e);
                    Debug.Assert(e._Sym._Sym == e);
                    Debug.Assert(e._Lnext._Onext._Sym == e);
                    Debug.Assert(e._Onext._Sym._Lnext == e);
                    Debug.Assert(e._Lface == f);
                    e = e._Lnext;
                } while (e != f._anEdge);
            }
            Debug.Assert(f._prev == fPrev && f._anEdge == null);

            MeshUtils.Vertex vPrev = _vHead, v;
            for (vPrev = _vHead; (v = vPrev._next) != _vHead; vPrev = v)
            {
                Debug.Assert(v._prev == vPrev);
                e = v._anEdge;
                do
                {
                    Debug.Assert(e._Sym != e);
                    Debug.Assert(e._Sym._Sym == e);
                    Debug.Assert(e._Lnext._Onext._Sym == e);
                    Debug.Assert(e._Onext._Sym._Lnext == e);
                    Debug.Assert(e._Org == v);
                    e = e._Onext;
                } while (e != v._anEdge);
            }
            Debug.Assert(v._prev == vPrev && v._anEdge == null);

            MeshUtils.Edge ePrev = _eHead;
            for (ePrev = _eHead; (e = ePrev._next) != _eHead; ePrev = e)
            {
                Debug.Assert(e._Sym._next == ePrev._Sym);
                Debug.Assert(e._Sym != e);
                Debug.Assert(e._Sym._Sym == e);
                Debug.Assert(e._Org != null);
                Debug.Assert(e._Dst != null);
                Debug.Assert(e._Lnext._Onext._Sym == e);
                Debug.Assert(e._Onext._Sym._Lnext == e);
            }
            Debug.Assert(e._Sym._next == ePrev._Sym
                && e._Sym == _eHeadSym
                && e._Sym._Sym == e
                && e._Org == null && e._Dst == null
                && e._Lface == null && e._Rface == null);
        }
    }
    
    public struct Vec3
    {
        public readonly static Vec3 Zero = new Vec3();

        public float X, Y, Z;

        public float this[int index]
        {
            get
            {
                if (index == 0) return X;
                if (index == 1) return Y;
                if (index == 2) return Z;
                throw new IndexOutOfRangeException();
            }
            set
            {
                if (index == 0) X = value;
                else if (index == 1) Y = value;
                else if (index == 2) Z = value;
                else throw new IndexOutOfRangeException();
            }
        }

        public static void Sub(ref Vec3 lhs, ref Vec3 rhs, out Vec3 result)
        {
            result.X = lhs.X - rhs.X;
            result.Y = lhs.Y - rhs.Y;
            result.Z = lhs.Z - rhs.Z;
        }

        public static void Neg(ref Vec3 v)
        {
            v.X = -v.X;
            v.Y = -v.Y;
            v.Z = -v.Z;
        }

        public static void Dot(ref Vec3 u, ref Vec3 v, out float dot)
        {
            dot = u.X * v.X + u.Y * v.Y + u.Z * v.Z;
        }

        public static void Normalize(ref Vec3 v)
        {
            var len = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            Debug.Assert(len >= 0.0f);
            len = 1.0f / (float)Math.Sqrt(len);
            v.X *= len;
            v.Y *= len;
            v.Z *= len;
        }

        public static int LongAxis(ref Vec3 v)
        {
            int i = 0;
            if (Math.Abs(v.Y) > Math.Abs(v.X)) i = 1;
            if (Math.Abs(v.Z) > Math.Abs(i == 0 ? v.X : v.Y)) i = 2;
            return i;
        }

        public override string ToString()
        {
            return string.Format("{0}, {1}, {2}", X, Y, Z);
        }
    }

    internal static class MeshUtils
    {
        public const int Undef = ~0;

        public abstract class Pooled<T> where T : Pooled<T>, new()
        {
            private static Stack<T> _stack;

            public abstract void Reset();
            public virtual void OnFree() {}

            public static T Create()
            {
                if (_stack != null && _stack.Count > 0)
                {
                    return _stack.Pop();
                }
                return new T();
            }

            public void Free()
            {
                OnFree();
                Reset();
                if (_stack == null)
                {
                    _stack = new Stack<T>();
                }
                _stack.Push((T)this);
            }
        }

        public class Vertex : Pooled<Vertex>
        {
            internal Vertex _prev, _next;
            internal Edge _anEdge;

            internal Vec3 _coords;
            internal float _s, _t;
            internal PQHandle _pqHandle;
            internal int _n;
            internal object _data;

            public override void Reset()
            {
                _prev = _next = null;
                _anEdge = null;
                _coords = Vec3.Zero;
                _s = 0;
                _t = 0;
                _pqHandle = new PQHandle();
                _n = 0;
                _data = null;
            }
        }

        public class Face : Pooled<Face>
        {
            internal Face _prev, _next;
            internal Edge _anEdge;

            internal Face _trail;
            internal int _n;
            internal bool _marked, _inside;

            internal int VertsCount
            {
                get
                {
                    int n = 0;
                    var eCur = _anEdge;
                    do {
                        n++;
                        eCur = eCur._Lnext;
                    } while (eCur != _anEdge);
                    return n;
                }
            }

            public override void Reset()
            {
                _prev = _next = null;
                _anEdge = null;
                _trail = null;
                _n = 0;
                _marked = false;
                _inside = false;
            }
        }

        public struct EdgePair
        {
            internal Edge _e, _eSym;

            public static EdgePair Create()
            {
                var pair = new MeshUtils.EdgePair();
                pair._e = MeshUtils.Edge.Create();
                pair._e._pair = pair;
                pair._eSym = MeshUtils.Edge.Create();
                pair._eSym._pair = pair;
                return pair;
            }

            public void Reset()
            {
                _e = _eSym = null;
            }
        }

        public class Edge : Pooled<Edge>
        {
            internal EdgePair _pair;
            internal Edge _next, _Sym, _Onext, _Lnext;
            internal Vertex _Org;
            internal Face _Lface;
            internal Tess.ActiveRegion _activeRegion;
            internal int _winding;

            internal Face _Rface { get { return _Sym._Lface; } set { _Sym._Lface = value; } }
            internal Vertex _Dst { get { return _Sym._Org; }  set { _Sym._Org = value; } }

            internal Edge _Oprev { get { return _Sym._Lnext; } set { _Sym._Lnext = value; } }
            internal Edge _Lprev { get { return _Onext._Sym; } set { _Onext._Sym = value; } }
            internal Edge _Dprev { get { return _Lnext._Sym; } set { _Lnext._Sym = value; } }
            internal Edge _Rprev { get { return _Sym._Onext; } set { _Sym._Onext = value; } }
            internal Edge _Dnext { get { return _Rprev._Sym; } set { _Rprev._Sym = value; } }
            internal Edge _Rnext { get { return _Oprev._Sym; } set { _Oprev._Sym = value; } }

            internal static void EnsureFirst(ref Edge e)
            {
                if (e == e._pair._eSym)
                {
                    e = e._Sym;
                }
            }

            public override void Reset()
            {
                _pair.Reset();
                _next = _Sym = _Onext = _Lnext = null;
                _Org = null;
                _Lface = null;
                _activeRegion = null;
                _winding = 0;
            }
        }

        /// <summary>
        /// MakeEdge creates a new pair of half-edges which form their own loop.
        /// No vertex or face structures are allocated, but these must be assigned
        /// before the current edge operation is completed.
        /// </summary>
        public static Edge MakeEdge(Edge eNext)
        {
            Debug.Assert(eNext != null);

            var pair = EdgePair.Create();
            var e = pair._e;
            var eSym = pair._eSym;

            // Make sure eNext points to the first edge of the edge pair
            Edge.EnsureFirst(ref eNext);

            // Insert in circular doubly-linked list before eNext.
            // Note that the prev pointer is stored in Sym->next.
            var ePrev = eNext._Sym._next;
            eSym._next = ePrev;
            ePrev._Sym._next = e;
            e._next = eNext;
            eNext._Sym._next = eSym;

            e._Sym = eSym;
            e._Onext = e;
            e._Lnext = eSym;
            e._Org = null;
            e._Lface = null;
            e._winding = 0;
            e._activeRegion = null;

            eSym._Sym = e;
            eSym._Onext = eSym;
            eSym._Lnext = e;
            eSym._Org = null;
            eSym._Lface = null;
            eSym._winding = 0;
            eSym._activeRegion = null;

            return e;
        }

        /// <summary>
        /// Splice( a, b ) is best described by the Guibas/Stolfi paper or the
        /// CS348a notes (see Mesh.cs). Basically it modifies the mesh so that
        /// a->Onext and b->Onext are exchanged. This can have various effects
        /// depending on whether a and b belong to different face or vertex rings.
        /// For more explanation see Mesh.Splice().
        /// </summary>
        public static void Splice(Edge a, Edge b)
        {
            var aOnext = a._Onext;
            var bOnext = b._Onext;

            aOnext._Sym._Lnext = b;
            bOnext._Sym._Lnext = a;
            a._Onext = bOnext;
            b._Onext = aOnext;
        }

        /// <summary>
        /// MakeVertex( eOrig, vNext ) attaches a new vertex and makes it the
        /// origin of all edges in the vertex loop to which eOrig belongs. "vNext" gives
        /// a place to insert the new vertex in the global vertex list. We insert
        /// the new vertex *before* vNext so that algorithms which walk the vertex
        /// list will not see the newly created vertices.
        /// </summary>
        public static void MakeVertex(Edge eOrig, Vertex vNext)
        {
            var vNew = MeshUtils.Vertex.Create();

            // insert in circular doubly-linked list before vNext
            var vPrev = vNext._prev;
            vNew._prev = vPrev;
            vPrev._next = vNew;
            vNew._next = vNext;
            vNext._prev = vNew;

            vNew._anEdge = eOrig;
            // leave coords, s, t undefined

            // fix other edges on this vertex loop
            var e = eOrig;
            do {
                e._Org = vNew;
                e = e._Onext;
            } while (e != eOrig);
        }

        /// <summary>
        /// MakeFace( eOrig, fNext ) attaches a new face and makes it the left
        /// face of all edges in the face loop to which eOrig belongs. "fNext" gives
        /// a place to insert the new face in the global face list. We insert
        /// the new face *before* fNext so that algorithms which walk the face
        /// list will not see the newly created faces.
        /// </summary>
        public static void MakeFace(Edge eOrig, Face fNext)
        {
            var fNew = MeshUtils.Face.Create();

            // insert in circular doubly-linked list before fNext
            var fPrev = fNext._prev;
            fNew._prev = fPrev;
            fPrev._next = fNew;
            fNew._next = fNext;
            fNext._prev = fNew;

            fNew._anEdge = eOrig;
            fNew._trail = null;
            fNew._marked = false;

            // The new face is marked "inside" if the old one was. This is a
            // convenience for the common case where a face has been split in two.
            fNew._inside = fNext._inside;

            // fix other edges on this face loop
            var e = eOrig;
            do {
                e._Lface = fNew;
                e = e._Lnext;
            } while (e != eOrig);
        }

        /// <summary>
        /// KillEdge( eDel ) destroys an edge (the half-edges eDel and eDel->Sym),
        /// and removes from the global edge list.
        /// </summary>
        public static void KillEdge(Edge eDel)
        {
            // Half-edges are allocated in pairs, see EdgePair above
            Edge.EnsureFirst(ref eDel);

            // delete from circular doubly-linked list
            var eNext = eDel._next;
            var ePrev = eDel._Sym._next;
            eNext._Sym._next = ePrev;
            ePrev._Sym._next = eNext;

            eDel.Free();
        }

        /// <summary>
        /// KillVertex( vDel ) destroys a vertex and removes it from the global
        /// vertex list. It updates the vertex loop to point to a given new vertex.
        /// </summary>
        public static void KillVertex(Vertex vDel, Vertex newOrg)
        {
            var eStart = vDel._anEdge;

            // change the origin of all affected edges
            var e = eStart;
            do {
                e._Org = newOrg;
                e = e._Onext;
            } while (e != eStart);

            // delete from circular doubly-linked list
            var vPrev = vDel._prev;
            var vNext = vDel._next;
            vNext._prev = vPrev;
            vPrev._next = vNext;

            vDel.Free();
        }

        /// <summary>
        /// KillFace( fDel ) destroys a face and removes it from the global face
        /// list. It updates the face loop to point to a given new face.
        /// </summary>
        public static void KillFace(Face fDel, Face newLFace)
        {
            var eStart = fDel._anEdge;

            // change the left face of all affected edges
            var e = eStart;
            do {
                e._Lface = newLFace;
                e = e._Lnext;
            } while (e != eStart);

            // delete from circular doubly-linked list
            var fPrev = fDel._prev;
            var fNext = fDel._next;
            fNext._prev = fPrev;
            fPrev._next = fNext;

            fDel.Free();
        }

        /// <summary>
        /// Return signed area of face.
        /// </summary>
        public static float FaceArea(Face f)
        {
            float area = 0;
            var e = f._anEdge;
            do
            {
                area += (e._Org._s - e._Dst._s) * (e._Org._t + e._Dst._t);
                e = e._Lnext;
            } while (e != f._anEdge);
            return area;
        }
    }
    
    internal struct PQHandle
    {
        public static readonly int Invalid = 0x0fffffff;
        internal int _handle;
    }

    internal class PriorityHeap<TValue> where TValue : class
    {
        public delegate bool LessOrEqual(TValue lhs, TValue rhs);

        protected class HandleElem
        {
            internal TValue _key;
            internal int _node;
        }

        private LessOrEqual _leq;
        private int[] _nodes;
        private HandleElem[] _handles;
        private int _size, _max;
        private int _freeList;
        private bool _initialized;

        public bool Empty { get { return _size == 0; } }

        public PriorityHeap(int initialSize, LessOrEqual leq)
        {
            _leq = leq;

            _nodes = new int[initialSize + 1];
            _handles = new HandleElem[initialSize + 1];

            _size = 0;
            _max = initialSize;
            _freeList = 0;
            _initialized = false;

            _nodes[1] = 1;
            _handles[1] = new HandleElem { _key = null };
        }

        private void FloatDown(int curr)
        {
            int child;
            int hCurr, hChild;

            hCurr = _nodes[curr];
            while (true)
            {
                child = curr << 1;
                if (child < _size && _leq(_handles[_nodes[child + 1]]._key, _handles[_nodes[child]]._key))
                {
                    ++child;
                }

                Debug.Assert(child <= _max);

                hChild = _nodes[child];
                if (child > _size || _leq(_handles[hCurr]._key, _handles[hChild]._key))
                {
                    _nodes[curr] = hCurr;
                    _handles[hCurr]._node = curr;
                    break;
                }

                _nodes[curr] = hChild;
                _handles[hChild]._node = curr;
                curr = child;
            }
        }

        private void FloatUp(int curr)
        {
            int parent;
            int hCurr, hParent;

            hCurr = _nodes[curr];
            while (true)
            {
                parent = curr >> 1;
                hParent = _nodes[parent];
                if (parent == 0 || _leq(_handles[hParent]._key, _handles[hCurr]._key))
                {
                    _nodes[curr] = hCurr;
                    _handles[hCurr]._node = curr;
                    break;
                }
                _nodes[curr] = hParent;
                _handles[hParent]._node = curr;
                curr = parent;
            }
        }

        public void Init()
        {
            for (int i = _size; i >= 1; --i)
            {
                FloatDown(i);
            }
            _initialized = true;
        }

        public PQHandle Insert(TValue value)
        {
            int curr = ++_size;
            if ((curr * 2) > _max)
            {
                _max <<= 1;
                Array.Resize(ref _nodes, _max + 1);
                Array.Resize(ref _handles, _max + 1);
            }

            int free;
            if (_freeList == 0)
            {
                free = curr;
            }
            else
            {
                free = _freeList;
                _freeList = _handles[free]._node;
            }

            _nodes[curr] = free;
            if (_handles[free] == null)
            {
                _handles[free] = new HandleElem { _key = value, _node = curr };
            }
            else
            {
                _handles[free]._node = curr;
                _handles[free]._key = value;
            }

            if (_initialized)
            {
                FloatUp(curr);
            }

            Debug.Assert(free != PQHandle.Invalid);
            return new PQHandle { _handle = free };
        }

        public TValue ExtractMin()
        {
            Debug.Assert(_initialized);

            int hMin = _nodes[1];
            TValue min = _handles[hMin]._key;

            if (_size > 0)
            {
                _nodes[1] = _nodes[_size];
                _handles[_nodes[1]]._node = 1;

                _handles[hMin]._key = null;
                _handles[hMin]._node = _freeList;
                _freeList = hMin;

                if (--_size > 0)
                {
                    FloatDown(1);
                }
            }

            return min;
        }

        public TValue Minimum()
        {
            Debug.Assert(_initialized);
            return _handles[_nodes[1]]._key;
        }

        public void Remove(PQHandle handle)
        {
            Debug.Assert(_initialized);

            int hCurr = handle._handle;
            Debug.Assert(hCurr >= 1 && hCurr <= _max && _handles[hCurr]._key != null);

            int curr = _handles[hCurr]._node;
            _nodes[curr] = _nodes[_size];
            _handles[_nodes[curr]]._node = curr;

            if (curr <= --_size)
            {
                if (curr <= 1 || _leq(_handles[_nodes[curr >> 1]]._key, _handles[_nodes[curr]]._key))
                {
                    FloatDown(curr);
                }
                else
                {
                    FloatUp(curr);
                }
            }

            _handles[hCurr]._key = null;
            _handles[hCurr]._node = _freeList;
            _freeList = hCurr;
        }
    }
    
    internal class PriorityQueue<TValue> where TValue : class
    {
        private PriorityHeap<TValue>.LessOrEqual _leq;
        private PriorityHeap<TValue> _heap;
        private TValue[] _keys;
        private int[] _order;

        private int _size, _max;
        private bool _initialized;

        public bool Empty { get { return _size == 0 && _heap.Empty; } }

        public PriorityQueue(int initialSize, PriorityHeap<TValue>.LessOrEqual leq)
        {
            _leq = leq;
            _heap = new PriorityHeap<TValue>(initialSize, leq);

            _keys = new TValue[initialSize];

            _size = 0;
            _max = initialSize;
            _initialized = false;
        }

        class StackItem
        {
            internal int p, r;
        };

        static void Swap(ref int a, ref int b)
        {
            int tmp = a;
            a = b;
            b = tmp;
        }

        public void Init()
        {
            var stack = new Stack<StackItem>();
            int p, r, i, j, piv;
            uint seed = 2016473283;

            p = 0;
            r = _size - 1;
            _order = new int[_size + 1];
            for (piv = 0, i = p; i <= r; ++piv, ++i)
            {
                _order[i] = piv;
            }

            stack.Push(new StackItem { p = p, r = r });
            while (stack.Count > 0)
            {
                var top = stack.Pop();
                p = top.p;
                r = top.r;

                while (r > p + 10)
                {
                    seed = seed * 1539415821 + 1;
                    i = p + (int)(seed % (r - p + 1));
                    piv = _order[i];
                    _order[i] = _order[p];
                    _order[p] = piv;
                    i = p - 1;
                    j = r + 1;
                    do {
                        do { ++i; } while (!_leq(_keys[_order[i]], _keys[piv]));
                        do { --j; } while (!_leq(_keys[piv], _keys[_order[j]]));
                        Swap(ref _order[i], ref _order[j]);
                    } while (i < j);
                    Swap(ref _order[i], ref _order[j]);
                    if (i - p < r - j)
                    {
                        stack.Push(new StackItem { p = j + 1, r = r });
                        r = i - 1;
                    }
                    else
                    {
                        stack.Push(new StackItem { p = p, r = i - 1 });
                        p = j + 1;
                    }
                }
                for (i = p + 1; i <= r; ++i)
                {
                    piv = _order[i];
                    for (j = i; j > p && !_leq(_keys[piv], _keys[_order[j - 1]]); --j)
                    {
                        _order[j] = _order[j - 1];
                    }
                    _order[j] = piv;
                }
            }

            _max = _size;
            _initialized = true;
            _heap.Init();
        }

        public PQHandle Insert(TValue value)
        {
            if (_initialized)
            {
                return _heap.Insert(value);
            }

            int curr = _size;
            if (++_size >= _max)
            {
                _max <<= 1;
                Array.Resize(ref _keys, _max);
            }

            _keys[curr] = value;
            return new PQHandle { _handle = -(curr + 1) };
        }

        public TValue ExtractMin()
        {
            Debug.Assert(_initialized);

            if (_size == 0)
            {
                return _heap.ExtractMin();
            }
            TValue sortMin = _keys[_order[_size - 1]];
            if (!_heap.Empty)
            {
                TValue heapMin = _heap.Minimum();
                if (_leq(heapMin, sortMin))
                    return _heap.ExtractMin();
            }
            do {
                --_size;
            } while (_size > 0 && _keys[_order[_size - 1]] == null);

            return sortMin;
        }

        public TValue Minimum()
        {
            Debug.Assert(_initialized);

            if (_size == 0)
            {
                return _heap.Minimum();
            }
            TValue sortMin = _keys[_order[_size - 1]];
            if (!_heap.Empty)
            {
                TValue heapMin = _heap.Minimum();
                if (_leq(heapMin, sortMin))
                    return heapMin;
            }
            return sortMin;
        }

        public void Remove(PQHandle handle)
        {
            Debug.Assert(_initialized);

            int curr = handle._handle;
            if (curr >= 0)
            {
                _heap.Remove(handle);
                return;
            }
            curr = -(curr + 1);
            Debug.Assert(curr < _max && _keys[curr] != null);

            _keys[curr] = null;
            while (_size > 0 && _keys[_order[_size - 1]] == null)
            {
                --_size;
            }
        }
    }

    public class Tess
    {
        internal class ActiveRegion
        {
            internal MeshUtils.Edge _eUp;
            internal Dict<ActiveRegion>.Node _nodeUp;
            internal int _windingNumber;
            internal bool _inside, _sentinel, _dirty, _fixUpperEdge;
        }

        private ActiveRegion RegionBelow(ActiveRegion reg)
        {
            return reg._nodeUp._prev._key;
        }

        private ActiveRegion RegionAbove(ActiveRegion reg)
        {
            return reg._nodeUp._next._key;
        }

        /// <summary>
        /// Both edges must be directed from right to left (this is the canonical
        /// direction for the upper edge of each region).
        /// 
        /// The strategy is to evaluate a "t" value for each edge at the
        /// current sweep line position, given by tess->event. The calculations
        /// are designed to be very stable, but of course they are not perfect.
        /// 
        /// Special case: if both edge destinations are at the sweep event,
        /// we sort the edges by slope (they would otherwise compare equally).
        /// </summary>
        private bool EdgeLeq(ActiveRegion reg1, ActiveRegion reg2)
        {
            var e1 = reg1._eUp;
            var e2 = reg2._eUp;

            if (e1._Dst == _event)
            {
                if (e2._Dst == _event)
                {
                    // Two edges right of the sweep line which meet at the sweep event.
                    // Sort them by slope.
                    if (Geom.VertLeq(e1._Org, e2._Org))
                    {
                        return Geom.EdgeSign(e2._Dst, e1._Org, e2._Org) <= 0.0f;
                    }
                    return Geom.EdgeSign(e1._Dst, e2._Org, e1._Org) >= 0.0f;
                }
                return Geom.EdgeSign(e2._Dst, _event, e2._Org) <= 0.0f;
            }
            if (e2._Dst == _event)
            {
                return Geom.EdgeSign(e1._Dst, _event, e1._Org) >= 0.0f;
            }

            // General case - compute signed distance *from* e1, e2 to event
            var t1 = Geom.EdgeEval(e1._Dst, _event, e1._Org);
            var t2 = Geom.EdgeEval(e2._Dst, _event, e2._Org);
            return (t1 >= t2);
        }

        private void DeleteRegion(ActiveRegion reg)
        {
            if (reg._fixUpperEdge)
            {
                // It was created with zero winding number, so it better be
                // deleted with zero winding number (ie. it better not get merged
                // with a real edge).
                Debug.Assert(reg._eUp._winding == 0);
            }
            reg._eUp._activeRegion = null;
            _dict.Remove(reg._nodeUp);
        }

        /// <summary>
        /// Replace an upper edge which needs fixing (see ConnectRightVertex).
        /// </summary>
        private void FixUpperEdge(ActiveRegion reg, MeshUtils.Edge newEdge)
        {
            Debug.Assert(reg._fixUpperEdge);
            _mesh.Delete(reg._eUp);
            reg._fixUpperEdge = false;
            reg._eUp = newEdge;
            newEdge._activeRegion = reg;
        }

        private ActiveRegion TopLeftRegion(ActiveRegion reg)
        {
            var org = reg._eUp._Org;

            // Find the region above the uppermost edge with the same origin
            do {
                reg = RegionAbove(reg);
            } while (reg._eUp._Org == org);

            // If the edge above was a temporary edge introduced by ConnectRightVertex,
            // now is the time to fix it.
            if (reg._fixUpperEdge)
            {
                var e = _mesh.Connect(RegionBelow(reg)._eUp._Sym, reg._eUp._Lnext);
                FixUpperEdge(reg, e);
                reg = RegionAbove(reg);
            }

            return reg;
        }

        private ActiveRegion TopRightRegion(ActiveRegion reg)
        {
            var dst = reg._eUp._Dst;

            // Find the region above the uppermost edge with the same destination
            do {
                reg = RegionAbove(reg);
            } while (reg._eUp._Dst == dst);

            return reg;
        }

        /// <summary>
        /// Add a new active region to the sweep line, *somewhere* below "regAbove"
        /// (according to where the new edge belongs in the sweep-line dictionary).
        /// The upper edge of the new region will be "eNewUp".
        /// Winding number and "inside" flag are not updated.
        /// </summary>
        private ActiveRegion AddRegionBelow(ActiveRegion regAbove, MeshUtils.Edge eNewUp)
        {
            var regNew = new ActiveRegion();

            regNew._eUp = eNewUp;
            regNew._nodeUp = _dict.InsertBefore(regAbove._nodeUp, regNew);
            regNew._fixUpperEdge = false;
            regNew._sentinel = false;
            regNew._dirty = false;

            eNewUp._activeRegion = regNew;

            return regNew;
        }

        private void ComputeWinding(ActiveRegion reg)
        {
            reg._windingNumber = RegionAbove(reg)._windingNumber + reg._eUp._winding;
            reg._inside = Geom.IsWindingInside(_windingRule, reg._windingNumber);
        }

        /// <summary>
        /// Delete a region from the sweep line. This happens when the upper
        /// and lower chains of a region meet (at a vertex on the sweep line).
        /// The "inside" flag is copied to the appropriate mesh face (we could
        /// not do this before -- since the structure of the mesh is always
        /// changing, this face may not have even existed until now).
        /// </summary>
        private void FinishRegion(ActiveRegion reg)
        {
            var e = reg._eUp;
            var f = e._Lface;

            f._inside = reg._inside;
            f._anEdge = e;
            DeleteRegion(reg);
        }

        /// <summary>
        /// We are given a vertex with one or more left-going edges.  All affected
        /// edges should be in the edge dictionary.  Starting at regFirst->eUp,
        /// we walk down deleting all regions where both edges have the same
        /// origin vOrg.  At the same time we copy the "inside" flag from the
        /// active region to the face, since at this point each face will belong
        /// to at most one region (this was not necessarily true until this point
        /// in the sweep).  The walk stops at the region above regLast; if regLast
        /// is null we walk as far as possible.  At the same time we relink the
        /// mesh if necessary, so that the ordering of edges around vOrg is the
        /// same as in the dictionary.
        /// </summary>
        private MeshUtils.Edge FinishLeftRegions(ActiveRegion regFirst, ActiveRegion regLast)
        {
            var regPrev = regFirst;
            var ePrev = regFirst._eUp;

            while (regPrev != regLast)
            {
                regPrev._fixUpperEdge = false;  // placement was OK
                var reg = RegionBelow(regPrev);
                var e = reg._eUp;
                if (e._Org != ePrev._Org)
                {
                    if (!reg._fixUpperEdge)
                    {
                        // Remove the last left-going edge.  Even though there are no further
                        // edges in the dictionary with this origin, there may be further
                        // such edges in the mesh (if we are adding left edges to a vertex
                        // that has already been processed).  Thus it is important to call
                        // FinishRegion rather than just DeleteRegion.
                        FinishRegion(regPrev);
                        break;
                    }
                    // If the edge below was a temporary edge introduced by
                    // ConnectRightVertex, now is the time to fix it.
                    e = _mesh.Connect(ePrev._Lprev, e._Sym);
                    FixUpperEdge(reg, e);
                }

                // Relink edges so that ePrev.Onext == e
                if (ePrev._Onext != e)
                {
                    _mesh.Splice(e._Oprev, e);
                    _mesh.Splice(ePrev, e);
                }
                FinishRegion(regPrev); // may change reg.eUp
                ePrev = reg._eUp;
                regPrev = reg;
            }

            return ePrev;
        }

        /// <summary>
        /// Purpose: insert right-going edges into the edge dictionary, and update
        /// winding numbers and mesh connectivity appropriately.  All right-going
        /// edges share a common origin vOrg.  Edges are inserted CCW starting at
        /// eFirst; the last edge inserted is eLast.Oprev.  If vOrg has any
        /// left-going edges already processed, then eTopLeft must be the edge
        /// such that an imaginary upward vertical segment from vOrg would be
        /// contained between eTopLeft.Oprev and eTopLeft; otherwise eTopLeft
        /// should be null.
        /// </summary>
        private void AddRightEdges(ActiveRegion regUp, MeshUtils.Edge eFirst, MeshUtils.Edge eLast, MeshUtils.Edge eTopLeft, bool cleanUp)
        {
            bool firstTime = true;

            var e = eFirst; do
            {
                Debug.Assert(Geom.VertLeq(e._Org, e._Dst));
                AddRegionBelow(regUp, e._Sym);
                e = e._Onext;
            } while (e != eLast);

            // Walk *all* right-going edges from e.Org, in the dictionary order,
            // updating the winding numbers of each region, and re-linking the mesh
            // edges to match the dictionary ordering (if necessary).
            if (eTopLeft == null)
            {
                eTopLeft = RegionBelow(regUp)._eUp._Rprev;
            }

            ActiveRegion regPrev = regUp, reg;
            var ePrev = eTopLeft;
            while (true)
            {
                reg = RegionBelow(regPrev);
                e = reg._eUp._Sym;
                if (e._Org != ePrev._Org) break;

                if (e._Onext != ePrev)
                {
                    // Unlink e from its current position, and relink below ePrev
                    _mesh.Splice(e._Oprev, e);
                    _mesh.Splice(ePrev._Oprev, e);
                }
                // Compute the winding number and "inside" flag for the new regions
                reg._windingNumber = regPrev._windingNumber - e._winding;
                reg._inside = Geom.IsWindingInside(_windingRule, reg._windingNumber);

                // Check for two outgoing edges with same slope -- process these
                // before any intersection tests (see example in tessComputeInterior).
                regPrev._dirty = true;
                if (!firstTime && CheckForRightSplice(regPrev))
                {
                    Geom.AddWinding(e, ePrev);
                    DeleteRegion(regPrev);
                    _mesh.Delete(ePrev);
                }
                firstTime = false;
                regPrev = reg;
                ePrev = e;
            }
            regPrev._dirty = true;
            Debug.Assert(regPrev._windingNumber - e._winding == reg._windingNumber);

            if (cleanUp)
            {
                // Check for intersections between newly adjacent edges.
                WalkDirtyRegions(regPrev);
            }
        }

        /// <summary>
        /// Two vertices with idential coordinates are combined into one.
        /// e1.Org is kept, while e2.Org is discarded.
        /// </summary>
        private void SpliceMergeVertices(MeshUtils.Edge e1, MeshUtils.Edge e2)
        {
            _mesh.Splice(e1, e2);
        }

        /// <summary>
        /// Find some weights which describe how the intersection vertex is
        /// a linear combination of "org" and "dest".  Each of the two edges
        /// which generated "isect" is allocated 50% of the weight; each edge
        /// splits the weight between its org and dst according to the
        /// relative distance to "isect".
        /// </summary>
        private void VertexWeights(MeshUtils.Vertex isect, MeshUtils.Vertex org, MeshUtils.Vertex dst, out float w0, out float w1)
        {
            var t1 = Geom.VertL1dist(org, isect);
            var t2 = Geom.VertL1dist(dst, isect);

            w0 = (t2 / (t1 + t2)) / 2.0f;
            w1 = (t1 / (t1 + t2)) / 2.0f;

            isect._coords.X += w0 * org._coords.X + w1 * dst._coords.X;
            isect._coords.Y += w0 * org._coords.Y + w1 * dst._coords.Y;
            isect._coords.Z += w0 * org._coords.Z + w1 * dst._coords.Z;
        }

        /// <summary>
        /// We've computed a new intersection point, now we need a "data" pointer
        /// from the user so that we can refer to this new vertex in the
        /// rendering callbacks.
        /// </summary>
        private void GetIntersectData(MeshUtils.Vertex isect, MeshUtils.Vertex orgUp, MeshUtils.Vertex dstUp, MeshUtils.Vertex orgLo, MeshUtils.Vertex dstLo)
        {
            isect._coords = Vec3.Zero;
            float w0, w1, w2, w3;
            VertexWeights(isect, orgUp, dstUp, out w0, out w1);
            VertexWeights(isect, orgLo, dstLo, out w2, out w3);

            if (_combineCallback != null)
            {
                isect._data = _combineCallback(
                    isect._coords,
                    new object[] { orgUp._data, dstUp._data, orgLo._data, dstLo._data },
                    new float[] { w0, w1, w2, w3 }
                );
            }
        }

        /// <summary>
        /// Check the upper and lower edge of "regUp", to make sure that the
        /// eUp->Org is above eLo, or eLo->Org is below eUp (depending on which
        /// origin is leftmost).
        /// 
        /// The main purpose is to splice right-going edges with the same
        /// dest vertex and nearly identical slopes (ie. we can't distinguish
        /// the slopes numerically).  However the splicing can also help us
        /// to recover from numerical errors.  For example, suppose at one
        /// point we checked eUp and eLo, and decided that eUp->Org is barely
        /// above eLo.  Then later, we split eLo into two edges (eg. from
        /// a splice operation like this one).  This can change the result of
        /// our test so that now eUp->Org is incident to eLo, or barely below it.
        /// We must correct this condition to maintain the dictionary invariants.
        /// 
        /// One possibility is to check these edges for intersection again
        /// (ie. CheckForIntersect).  This is what we do if possible.  However
        /// CheckForIntersect requires that tess->event lies between eUp and eLo,
        /// so that it has something to fall back on when the intersection
        /// calculation gives us an unusable answer.  So, for those cases where
        /// we can't check for intersection, this routine fixes the problem
        /// by just splicing the offending vertex into the other edge.
        /// This is a guaranteed solution, no matter how degenerate things get.
        /// Basically this is a combinatorial solution to a numerical problem.
        /// </summary>
        private bool CheckForRightSplice(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;

            if (Geom.VertLeq(eUp._Org, eLo._Org))
            {
                if (Geom.EdgeSign(eLo._Dst, eUp._Org, eLo._Org) > 0.0f)
                {
                    return false;
                }

                // eUp.Org appears to be below eLo
                if (!Geom.VertEq(eUp._Org, eLo._Org))
                {
                    // Splice eUp._Org into eLo
                    _mesh.SplitEdge(eLo._Sym);
                    _mesh.Splice(eUp, eLo._Oprev);
                    regUp._dirty = regLo._dirty = true;
                }
                else if (eUp._Org != eLo._Org)
                {
                    // merge the two vertices, discarding eUp.Org
                    _pq.Remove(eUp._Org._pqHandle);
                    SpliceMergeVertices(eLo._Oprev, eUp);
                }
            }
            else
            {
                if (Geom.EdgeSign(eUp._Dst, eLo._Org, eUp._Org) < 0.0f)
                {
                    return false;
                }

                // eLo.Org appears to be above eUp, so splice eLo.Org into eUp
                RegionAbove(regUp)._dirty = regUp._dirty = true;
                _mesh.SplitEdge(eUp._Sym);
                _mesh.Splice(eLo._Oprev, eUp);
            }
            return true;
        }
        
        /// <summary>
        /// Check the upper and lower edge of "regUp", to make sure that the
        /// eUp->Dst is above eLo, or eLo->Dst is below eUp (depending on which
        /// destination is rightmost).
        /// 
        /// Theoretically, this should always be true.  However, splitting an edge
        /// into two pieces can change the results of previous tests.  For example,
        /// suppose at one point we checked eUp and eLo, and decided that eUp->Dst
        /// is barely above eLo.  Then later, we split eLo into two edges (eg. from
        /// a splice operation like this one).  This can change the result of
        /// the test so that now eUp->Dst is incident to eLo, or barely below it.
        /// We must correct this condition to maintain the dictionary invariants
        /// (otherwise new edges might get inserted in the wrong place in the
        /// dictionary, and bad stuff will happen).
        /// 
        /// We fix the problem by just splicing the offending vertex into the
        /// other edge.
        /// </summary>
        private bool CheckForLeftSplice(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;

            Debug.Assert(!Geom.VertEq(eUp._Dst, eLo._Dst));

            if (Geom.VertLeq(eUp._Dst, eLo._Dst))
            {
                if (Geom.EdgeSign(eUp._Dst, eLo._Dst, eUp._Org) < 0.0f)
                {
                    return false;
                }

                // eLo.Dst is above eUp, so splice eLo.Dst into eUp
                RegionAbove(regUp)._dirty = regUp._dirty = true;
                var e = _mesh.SplitEdge(eUp);
                _mesh.Splice(eLo._Sym, e);
                e._Lface._inside = regUp._inside;
            }
            else
            {
                if (Geom.EdgeSign(eLo._Dst, eUp._Dst, eLo._Org) > 0.0f)
                {
                    return false;
                }

                // eUp.Dst is below eLo, so splice eUp.Dst into eLo
                regUp._dirty = regLo._dirty = true;
                var e = _mesh.SplitEdge(eLo);
                _mesh.Splice(eUp._Lnext, eLo._Sym);
                e._Rface._inside = regUp._inside;
            }
            return true;
        }

        /// <summary>
        /// Check the upper and lower edges of the given region to see if
        /// they intersect.  If so, create the intersection and add it
        /// to the data structures.
        /// 
        /// Returns TRUE if adding the new intersection resulted in a recursive
        /// call to AddRightEdges(); in this case all "dirty" regions have been
        /// checked for intersections, and possibly regUp has been deleted.
        /// </summary>
        private bool CheckForIntersect(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;
            var orgUp = eUp._Org;
            var orgLo = eLo._Org;
            var dstUp = eUp._Dst;
            var dstLo = eLo._Dst;

            Debug.Assert(!Geom.VertEq(dstLo, dstUp));
            Debug.Assert(Geom.EdgeSign(dstUp, _event, orgUp) <= 0.0f);
            Debug.Assert(Geom.EdgeSign(dstLo, _event, orgLo) >= 0.0f);
            Debug.Assert(orgUp != _event && orgLo != _event);
            Debug.Assert(!regUp._fixUpperEdge && !regLo._fixUpperEdge);

            if( orgUp == orgLo )
            {
                // right endpoints are the same
                return false;
            }

            var tMinUp = Math.Min(orgUp._t, dstUp._t);
            var tMaxLo = Math.Max(orgLo._t, dstLo._t);
            if( tMinUp > tMaxLo )
            {
                // t ranges do not overlap
                return false;
            }

            if (Geom.VertLeq(orgUp, orgLo))
            {
                if (Geom.EdgeSign( dstLo, orgUp, orgLo ) > 0.0f)
                {
                    return false;
                }
            }
            else
            {
                if (Geom.EdgeSign( dstUp, orgLo, orgUp ) < 0.0f)
                {
                    return false;
                }
            }

            // At this point the edges intersect, at least marginally

            var isect = MeshUtils.Vertex.Create();
            Geom.EdgeIntersect(dstUp, orgUp, dstLo, orgLo, isect);
            // The following properties are guaranteed:
            Debug.Assert(Math.Min(orgUp._t, dstUp._t) <= isect._t);
            Debug.Assert(isect._t <= Math.Max(orgLo._t, dstLo._t));
            Debug.Assert(Math.Min(dstLo._s, dstUp._s) <= isect._s);
            Debug.Assert(isect._s <= Math.Max(orgLo._s, orgUp._s));

            if (Geom.VertLeq(isect, _event))
            {
                // The intersection point lies slightly to the left of the sweep line,
                // so move it until it''s slightly to the right of the sweep line.
                // (If we had perfect numerical precision, this would never happen
                // in the first place). The easiest and safest thing to do is
                // replace the intersection by tess._event.
                isect._s = _event._s;
                isect._t = _event._t;
            }
            // Similarly, if the computed intersection lies to the right of the
            // rightmost origin (which should rarely happen), it can cause
            // unbelievable inefficiency on sufficiently degenerate inputs.
            // (If you have the test program, try running test54.d with the
            // "X zoom" option turned on).
            var orgMin = Geom.VertLeq(orgUp, orgLo) ? orgUp : orgLo;
            if (Geom.VertLeq(orgMin, isect))
            {
                isect._s = orgMin._s;
                isect._t = orgMin._t;
            }

            if (Geom.VertEq(isect, orgUp) || Geom.VertEq(isect, orgLo))
            {
                // Easy case -- intersection at one of the right endpoints
                CheckForRightSplice(regUp);
                return false;
            }

            if (   (! Geom.VertEq(dstUp, _event)
                && Geom.EdgeSign(dstUp, _event, isect) >= 0.0f)
                || (! Geom.VertEq(dstLo, _event)
                && Geom.EdgeSign(dstLo, _event, isect) <= 0.0f))
            {
                // Very unusual -- the new upper or lower edge would pass on the
                // wrong side of the sweep event, or through it. This can happen
                // due to very small numerical errors in the intersection calculation.
                if (dstLo == _event)
                {
                    // Splice dstLo into eUp, and process the new region(s)
                    _mesh.SplitEdge(eUp._Sym);
                    _mesh.Splice(eLo._Sym, eUp);
                    regUp = TopLeftRegion(regUp);
                    eUp = RegionBelow(regUp)._eUp;
                    FinishLeftRegions(RegionBelow(regUp), regLo);
                    AddRightEdges(regUp, eUp._Oprev, eUp, eUp, true);
                    return true;
                }
                if( dstUp == _event ) {
                    /* Splice dstUp into eLo, and process the new region(s) */
                    _mesh.SplitEdge(eLo._Sym);
                    _mesh.Splice(eUp._Lnext, eLo._Oprev);
                    regLo = regUp;
                    regUp = TopRightRegion(regUp);
                    var e = RegionBelow(regUp)._eUp._Rprev;
                    regLo._eUp = eLo._Oprev;
                    eLo = FinishLeftRegions(regLo, null);
                    AddRightEdges(regUp, eLo._Onext, eUp._Rprev, e, true);
                    return true;
                }
                // Special case: called from ConnectRightVertex. If either
                // edge passes on the wrong side of tess._event, split it
                // (and wait for ConnectRightVertex to splice it appropriately).
                if (Geom.EdgeSign( dstUp, _event, isect ) >= 0.0f)
                {
                    RegionAbove(regUp)._dirty = regUp._dirty = true;
                    _mesh.SplitEdge(eUp._Sym);
                    eUp._Org._s = _event._s;
                    eUp._Org._t = _event._t;
                }
                if (Geom.EdgeSign(dstLo, _event, isect) <= 0.0f)
                {
                    regUp._dirty = regLo._dirty = true;
                    _mesh.SplitEdge(eLo._Sym);
                    eLo._Org._s = _event._s;
                    eLo._Org._t = _event._t;
                }
                // leave the rest for ConnectRightVertex
                return false;
            }

            // General case -- split both edges, splice into new vertex.
            // When we do the splice operation, the order of the arguments is
            // arbitrary as far as correctness goes. However, when the operation
            // creates a new face, the work done is proportional to the size of
            // the new face.  We expect the faces in the processed part of
            // the mesh (ie. eUp._Lface) to be smaller than the faces in the
            // unprocessed original contours (which will be eLo._Oprev._Lface).
            _mesh.SplitEdge(eUp._Sym);
            _mesh.SplitEdge(eLo._Sym);
            _mesh.Splice(eLo._Oprev, eUp);
            eUp._Org._s = isect._s;
            eUp._Org._t = isect._t;
            eUp._Org._pqHandle = _pq.Insert(eUp._Org);
            if (eUp._Org._pqHandle._handle == PQHandle.Invalid)
            {
                throw new InvalidOperationException("PQHandle should not be invalid");
            }
            GetIntersectData(eUp._Org, orgUp, dstUp, orgLo, dstLo);
            RegionAbove(regUp)._dirty = regUp._dirty = regLo._dirty = true;
            return false;
        }

        /// <summary>
        /// When the upper or lower edge of any region changes, the region is
        /// marked "dirty".  This routine walks through all the dirty regions
        /// and makes sure that the dictionary invariants are satisfied
        /// (see the comments at the beginning of this file).  Of course
        /// new dirty regions can be created as we make changes to restore
        /// the invariants.
        /// </summary>
        private void WalkDirtyRegions(ActiveRegion regUp)
        {
            var regLo = RegionBelow(regUp);
            MeshUtils.Edge eUp, eLo;

            while (true)
            {
                // Find the lowest dirty region (we walk from the bottom up).
                while (regLo._dirty)
                {
                    regUp = regLo;
                    regLo = RegionBelow(regLo);
                }
                if (!regUp._dirty)
                {
                    regLo = regUp;
                    regUp = RegionAbove( regUp );
                    if(regUp == null || !regUp._dirty)
                    {
                        // We've walked all the dirty regions
                        return;
                    }
                }
                regUp._dirty = false;
                eUp = regUp._eUp;
                eLo = regLo._eUp;

                if (eUp._Dst != eLo._Dst)
                {
                    // Check that the edge ordering is obeyed at the Dst vertices.
                    if (CheckForLeftSplice(regUp))
                    {

                        // If the upper or lower edge was marked fixUpperEdge, then
                        // we no longer need it (since these edges are needed only for
                        // vertices which otherwise have no right-going edges).
                        if (regLo._fixUpperEdge)
                        {
                            DeleteRegion(regLo);
                            _mesh.Delete(eLo);
                            regLo = RegionBelow(regUp);
                            eLo = regLo._eUp;
                        }
                        else if( regUp._fixUpperEdge )
                        {
                            DeleteRegion(regUp);
                            _mesh.Delete(eUp);
                            regUp = RegionAbove(regLo);
                            eUp = regUp._eUp;
                        }
                    }
                }
                if (eUp._Org != eLo._Org)
                {
                    if(    eUp._Dst != eLo._Dst
                        && ! regUp._fixUpperEdge && ! regLo._fixUpperEdge
                        && (eUp._Dst == _event || eLo._Dst == _event) )
                    {
                        // When all else fails in CheckForIntersect(), it uses tess._event
                        // as the intersection location. To make this possible, it requires
                        // that tess._event lie between the upper and lower edges, and also
                        // that neither of these is marked fixUpperEdge (since in the worst
                        // case it might splice one of these edges into tess.event, and
                        // violate the invariant that fixable edges are the only right-going
                        // edge from their associated vertex).
                        if (CheckForIntersect(regUp))
                        {
                            // WalkDirtyRegions() was called recursively; we're done
                            return;
                        }
                    }
                    else
                    {
                        // Even though we can't use CheckForIntersect(), the Org vertices
                        // may violate the dictionary edge ordering. Check and correct this.
                        CheckForRightSplice(regUp);
                    }
                }
                if (eUp._Org == eLo._Org && eUp._Dst == eLo._Dst)
                {
                    // A degenerate loop consisting of only two edges -- delete it.
                    Geom.AddWinding(eLo, eUp);
                    DeleteRegion(regUp);
                    _mesh.Delete(eUp);
                    regUp = RegionAbove(regLo);
                }
            }
        }

        /// <summary>
        /// Purpose: connect a "right" vertex vEvent (one where all edges go left)
        /// to the unprocessed portion of the mesh.  Since there are no right-going
        /// edges, two regions (one above vEvent and one below) are being merged
        /// into one.  "regUp" is the upper of these two regions.
        /// 
        /// There are two reasons for doing this (adding a right-going edge):
        ///  - if the two regions being merged are "inside", we must add an edge
        ///    to keep them separated (the combined region would not be monotone).
        ///  - in any case, we must leave some record of vEvent in the dictionary,
        ///    so that we can merge vEvent with features that we have not seen yet.
        ///    For example, maybe there is a vertical edge which passes just to
        ///    the right of vEvent; we would like to splice vEvent into this edge.
        /// 
        /// However, we don't want to connect vEvent to just any vertex.  We don''t
        /// want the new edge to cross any other edges; otherwise we will create
        /// intersection vertices even when the input data had no self-intersections.
        /// (This is a bad thing; if the user's input data has no intersections,
        /// we don't want to generate any false intersections ourselves.)
        /// 
        /// Our eventual goal is to connect vEvent to the leftmost unprocessed
        /// vertex of the combined region (the union of regUp and regLo).
        /// But because of unseen vertices with all right-going edges, and also
        /// new vertices which may be created by edge intersections, we don''t
        /// know where that leftmost unprocessed vertex is.  In the meantime, we
        /// connect vEvent to the closest vertex of either chain, and mark the region
        /// as "fixUpperEdge".  This flag says to delete and reconnect this edge
        /// to the next processed vertex on the boundary of the combined region.
        /// Quite possibly the vertex we connected to will turn out to be the
        /// closest one, in which case we won''t need to make any changes.
        /// </summary>
        private void ConnectRightVertex(ActiveRegion regUp, MeshUtils.Edge eBottomLeft)
        {
            var eTopLeft = eBottomLeft._Onext;
            var regLo = RegionBelow(regUp);
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;
            bool degenerate = false;

            if (eUp._Dst != eLo._Dst)
            {
                CheckForIntersect(regUp);
            }

            // Possible new degeneracies: upper or lower edge of regUp may pass
            // through vEvent, or may coincide with new intersection vertex
            if (Geom.VertEq(eUp._Org, _event))
            {
                _mesh.Splice(eTopLeft._Oprev, eUp);
                regUp = TopLeftRegion(regUp);
                eTopLeft = RegionBelow(regUp)._eUp;
                FinishLeftRegions(RegionBelow(regUp), regLo);
                degenerate = true;
            }
            if (Geom.VertEq(eLo._Org, _event))
            {
                _mesh.Splice(eBottomLeft, eLo._Oprev);
                eBottomLeft = FinishLeftRegions(regLo, null);
                degenerate = true;
            }
            if (degenerate)
            {
                AddRightEdges(regUp, eBottomLeft._Onext, eTopLeft, eTopLeft, true);
                return;
            }

            // Non-degenerate situation -- need to add a temporary, fixable edge.
            // Connect to the closer of eLo.Org, eUp.Org.
            MeshUtils.Edge eNew;
            if (Geom.VertLeq(eLo._Org, eUp._Org))
            {
                eNew = eLo._Oprev;
            }
            else
            {
                eNew = eUp;
            }
            eNew = _mesh.Connect(eBottomLeft._Lprev, eNew);

            // Prevent cleanup, otherwise eNew might disappear before we've even
            // had a chance to mark it as a temporary edge.
            AddRightEdges(regUp, eNew, eNew._Onext, eNew._Onext, false);
            eNew._Sym._activeRegion._fixUpperEdge = true;
            WalkDirtyRegions(regUp);
        }

        /// <summary>
        /// The event vertex lies exacty on an already-processed edge or vertex.
        /// Adding the new vertex involves splicing it into the already-processed
        /// part of the mesh.
        /// </summary>
        private void ConnectLeftDegenerate(ActiveRegion regUp, MeshUtils.Vertex vEvent)
        {
            var e = regUp._eUp;
            if (Geom.VertEq(e._Org, vEvent))
            {
                // e.Org is an unprocessed vertex - just combine them, and wait
                // for e.Org to be pulled from the queue
                // C# : in the C version, there is a flag but it was never implemented
                // the vertices are before beginning the tesselation
                throw new InvalidOperationException("Vertices should have been merged before");
            }

            if (!Geom.VertEq(e._Dst, vEvent))
            {
                // General case -- splice vEvent into edge e which passes through it
                _mesh.SplitEdge(e._Sym);
                if (regUp._fixUpperEdge)
                {
                    // This edge was fixable -- delete unused portion of original edge
                    _mesh.Delete(e._Onext);
                    regUp._fixUpperEdge = false;
                }
                _mesh.Splice(vEvent._anEdge, e);
                SweepEvent(vEvent); // recurse
                return;
            }

            // See above
            throw new InvalidOperationException("Vertices should have been merged before");
        }

        /// <summary>
        /// Purpose: connect a "left" vertex (one where both edges go right)
        /// to the processed portion of the mesh.  Let R be the active region
        /// containing vEvent, and let U and L be the upper and lower edge
        /// chains of R.  There are two possibilities:
        /// 
        /// - the normal case: split R into two regions, by connecting vEvent to
        ///   the rightmost vertex of U or L lying to the left of the sweep line
        /// 
        /// - the degenerate case: if vEvent is close enough to U or L, we
        ///   merge vEvent into that edge chain.  The subcases are:
        ///     - merging with the rightmost vertex of U or L
        ///     - merging with the active edge of U or L
        ///     - merging with an already-processed portion of U or L
        /// </summary>
        private void ConnectLeftVertex(MeshUtils.Vertex vEvent)
        {
            var tmp = new ActiveRegion();

            // Get a pointer to the active region containing vEvent
            tmp._eUp = vEvent._anEdge._Sym;
            var regUp = _dict.Find(tmp).Key;
            var regLo = RegionBelow(regUp);
            if (regLo == null)
            {
                // This may happen if the input polygon is coplanar.
                return;
            }
            var eUp = regUp._eUp;
            var eLo = regLo._eUp;

            // Try merging with U or L first
            if (Geom.EdgeSign(eUp._Dst, vEvent, eUp._Org) == 0.0f)
            {
                ConnectLeftDegenerate(regUp, vEvent);
                return;
            }

            // Connect vEvent to rightmost processed vertex of either chain.
            // e._Dst is the vertex that we will connect to vEvent.
            var reg = Geom.VertLeq(eLo._Dst, eUp._Dst) ? regUp : regLo;

            if (regUp._inside || reg._fixUpperEdge)
            {
                MeshUtils.Edge eNew;
                if (reg == regUp)
                {
                    eNew = _mesh.Connect(vEvent._anEdge._Sym, eUp._Lnext);
                }
                else
                {
                    eNew = _mesh.Connect(eLo._Dnext, vEvent._anEdge)._Sym;
                }
                if (reg._fixUpperEdge)
                {
                    FixUpperEdge(reg, eNew);
                }
                else
                {
                    ComputeWinding(AddRegionBelow(regUp, eNew));
                }
                SweepEvent(vEvent);
            }
            else
            {
                // The new vertex is in a region which does not belong to the polygon.
                // We don't need to connect this vertex to the rest of the mesh.
                AddRightEdges(regUp, vEvent._anEdge, vEvent._anEdge, null, true);
            }
        }

        /// <summary>
        /// Does everything necessary when the sweep line crosses a vertex.
        /// Updates the mesh and the edge dictionary.
        /// </summary>
        private void SweepEvent(MeshUtils.Vertex vEvent)
        {
            _event = vEvent;

            // Check if this vertex is the right endpoint of an edge that is
            // already in the dictionary. In this case we don't need to waste
            // time searching for the location to insert new edges.
            var e = vEvent._anEdge;
            while (e._activeRegion == null)
            {
                e = e._Onext;
                if (e == vEvent._anEdge)
                {
                    // All edges go right -- not incident to any processed edges
                    ConnectLeftVertex(vEvent);
                    return;
                }
            }

            // Processing consists of two phases: first we "finish" all the
            // active regions where both the upper and lower edges terminate
            // at vEvent (ie. vEvent is closing off these regions).
            // We mark these faces "inside" or "outside" the polygon according
            // to their winding number, and delete the edges from the dictionary.
            // This takes care of all the left-going edges from vEvent.
            var regUp = TopLeftRegion(e._activeRegion);
            var reg = RegionBelow(regUp);
            var eTopLeft = reg._eUp;
            var eBottomLeft = FinishLeftRegions(reg, null);

            // Next we process all the right-going edges from vEvent. This
            // involves adding the edges to the dictionary, and creating the
            // associated "active regions" which record information about the
            // regions between adjacent dictionary edges.
            if (eBottomLeft._Onext == eTopLeft)
            {
                // No right-going edges -- add a temporary "fixable" edge
                ConnectRightVertex(regUp, eBottomLeft);
            }
            else
            {
                AddRightEdges(regUp, eBottomLeft._Onext, eTopLeft, eTopLeft, true);
            }
        }

        /// <summary>
        /// Make the sentinel coordinates big enough that they will never be
        /// merged with real input features.
        /// 
        /// We add two sentinel edges above and below all other edges,
        /// to avoid special cases at the top and bottom.
        /// </summary>
        private void AddSentinel(float smin, float smax, float t)
        {
            var e = _mesh.MakeEdge();
            e._Org._s = smax;
            e._Org._t = t;
            e._Dst._s = smin;
            e._Dst._t = t;
            _event = e._Dst; // initialize it

            var reg = new ActiveRegion();
            reg._eUp = e;
            reg._windingNumber = 0;
            reg._inside = false;
            reg._fixUpperEdge = false;
            reg._sentinel = true;
            reg._dirty = false;
            reg._nodeUp = _dict.Insert(reg);
        }

        /// <summary>
        /// We maintain an ordering of edge intersections with the sweep line.
        /// This order is maintained in a dynamic dictionary.
        /// </summary>
        private void InitEdgeDict()
        {
            _dict = new Dict<ActiveRegion>(EdgeLeq);

            AddSentinel(-SentinelCoord, SentinelCoord, -SentinelCoord);
            AddSentinel(-SentinelCoord, SentinelCoord, +SentinelCoord);
        }

        private void DoneEdgeDict()
        {
            int fixedEdges = 0;

            ActiveRegion reg;
            while ((reg = _dict.Min().Key) != null)
            {
                // At the end of all processing, the dictionary should contain
                // only the two sentinel edges, plus at most one "fixable" edge
                // created by ConnectRightVertex().
                if (!reg._sentinel)
                {
                    Debug.Assert(reg._fixUpperEdge);
                    Debug.Assert(++fixedEdges == 1);
                }
                Debug.Assert(reg._windingNumber == 0);
                DeleteRegion(reg);
            }

            _dict = null;
        }

        /// <summary>
        /// Remove zero-length edges, and contours with fewer than 3 vertices.
        /// </summary>
        private void RemoveDegenerateEdges()
        {
            MeshUtils.Edge eHead = _mesh._eHead, e, eNext, eLnext;

            for (e = eHead._next; e != eHead; e = eNext)
            {
                eNext = e._next;
                eLnext = e._Lnext;

                if (Geom.VertEq(e._Org, e._Dst) && e._Lnext._Lnext != e)
                {
                    // Zero-length edge, contour has at least 3 edges

                    SpliceMergeVertices(eLnext, e); // deletes e.Org
                    _mesh.Delete(e); // e is a self-loop
                    e = eLnext;
                    eLnext = e._Lnext;
                }
                if (eLnext._Lnext == e)
                {
                    // Degenerate contour (one or two edges)

                    if (eLnext != e)
                    {
                        if (eLnext == eNext || eLnext == eNext._Sym)
                        {
                            eNext = eNext._next;
                        }
                        _mesh.Delete(eLnext);
                    }
                    if (e == eNext || e == eNext._Sym)
                    {
                        eNext = eNext._next;
                    }
                    _mesh.Delete(e);
                }
            }
        }

        /// <summary>
        /// Insert all vertices into the priority queue which determines the
        /// order in which vertices cross the sweep line.
        /// </summary>
        private void InitPriorityQ()
        {
            MeshUtils.Vertex vHead = _mesh._vHead, v;
            int vertexCount = 0;

            for (v = vHead._next; v != vHead; v = v._next)
            {
                vertexCount++;
            }
            // Make sure there is enough space for sentinels.
            vertexCount += 8;
    
            _pq = new PriorityQueue<MeshUtils.Vertex>(vertexCount, Geom.VertLeq);

            vHead = _mesh._vHead;
            for( v = vHead._next; v != vHead; v = v._next ) {
                v._pqHandle = _pq.Insert(v);
                if (v._pqHandle._handle == PQHandle.Invalid)
                {
                    throw new InvalidOperationException("PQHandle should not be invalid");
                }
            }
            _pq.Init();
        }

        private void DonePriorityQ()
        {
            _pq = null;
        }

        /// <summary>
        /// Delete any degenerate faces with only two edges.  WalkDirtyRegions()
        /// will catch almost all of these, but it won't catch degenerate faces
        /// produced by splice operations on already-processed edges.
        /// The two places this can happen are in FinishLeftRegions(), when
        /// we splice in a "temporary" edge produced by ConnectRightVertex(),
        /// and in CheckForLeftSplice(), where we splice already-processed
        /// edges to ensure that our dictionary invariants are not violated
        /// by numerical errors.
        /// 
        /// In both these cases it is *very* dangerous to delete the offending
        /// edge at the time, since one of the routines further up the stack
        /// will sometimes be keeping a pointer to that edge.
        /// </summary>
        private void RemoveDegenerateFaces()
        {
            MeshUtils.Face f, fNext;
            MeshUtils.Edge e;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = fNext)
            {
                fNext = f._next;
                e = f._anEdge;
                Debug.Assert(e._Lnext != e);

                if (e._Lnext._Lnext == e)
                {
                    // A face with only two edges
                    Geom.AddWinding(e._Onext, e);
                    _mesh.Delete(e);
                }
            }
        }

        /// <summary>
        /// ComputeInterior computes the planar arrangement specified
        /// by the given contours, and further subdivides this arrangement
        /// into regions.  Each region is marked "inside" if it belongs
        /// to the polygon, according to the rule given by windingRule.
        /// Each interior region is guaranteed to be monotone.
        /// </summary>
        protected void ComputeInterior()
        {
            // Each vertex defines an event for our sweep line. Start by inserting
            // all the vertices in a priority queue. Events are processed in
            // lexicographic order, ie.
            // 
            // e1 < e2  iff  e1.x < e2.x || (e1.x == e2.x && e1.y < e2.y)
            RemoveDegenerateEdges();
            InitPriorityQ();
            RemoveDegenerateFaces();
            InitEdgeDict();

            MeshUtils.Vertex v, vNext;
            while ((v = _pq.ExtractMin()) != null)
            {
                 while (true)
                 {
                    vNext = _pq.Minimum();
                    if (vNext == null || !Geom.VertEq(vNext, v))
                    {
                        break;
                    }

                    // Merge together all vertices at exactly the same location.
                    // This is more efficient than processing them one at a time,
                    // simplifies the code (see ConnectLeftDegenerate), and is also
                    // important for correct handling of certain degenerate cases.
                    // For example, suppose there are two identical edges A and B
                    // that belong to different contours (so without this code they would
                    // be processed by separate sweep events). Suppose another edge C
                    // crosses A and B from above. When A is processed, we split it
                    // at its intersection point with C. However this also splits C,
                    // so when we insert B we may compute a slightly different
                    // intersection point. This might leave two edges with a small
                    // gap between them. This kind of error is especially obvious
                    // when using boundary extraction (BoundaryOnly).
                    vNext = _pq.ExtractMin();
                    SpliceMergeVertices(v._anEdge, vNext._anEdge);
                }
                SweepEvent(v);
            }

            DoneEdgeDict();
            DonePriorityQ();

            RemoveDegenerateFaces();
            _mesh.Check();
        }
    
        private Mesh _mesh;
        private Vec3 _normal;
        private Vec3 _sUnit;
        private Vec3 _tUnit;

        private float _bminX, _bminY, _bmaxX, _bmaxY;

        private WindingRule _windingRule;

        private Dict<ActiveRegion> _dict;
        private PriorityQueue<MeshUtils.Vertex> _pq;
        private MeshUtils.Vertex _event;

        private CombineCallback _combineCallback;

        private ContourVertex[] _vertices;
        private int _vertexCount;
        private int[] _elements;
        private int _elementCount;

        public Vec3 Normal { get { return _normal; } set { _normal = value; } }

        public float SUnitX = 1;
        public float SUnitY = 0;
        public float SentinelCoord = 4e30f;

        /// <summary>
        /// If true, will remove empty (zero area) polygons.
        /// </summary>
        public bool NoEmptyPolygons = false;

        /// <summary>
        /// If true, will use pooling to reduce GC (compare performance with/without, can vary wildly).
        /// </summary>
        public bool UsePooling = false;

        public ContourVertex[] Vertices { get { return _vertices; } }
        public int VertexCount { get { return _vertexCount; } }

        public int[] Elements { get { return _elements; } }
        public int ElementCount { get { return _elementCount; } }

        public Tess()
        {
            _normal = Vec3.Zero;
            _bminX = _bminY = _bmaxX = _bmaxY = 0;

            _windingRule = WindingRule.EvenOdd;
            _mesh = null;

            _vertices = null;
            _vertexCount = 0;
            _elements = null;
            _elementCount = 0;
        }

        private void ComputeNormal(ref Vec3 norm)
        {
            var v = _mesh._vHead._next;

            var minVal = new float[3] { v._coords.X, v._coords.Y, v._coords.Z };
            var minVert = new MeshUtils.Vertex[3] { v, v, v };
            var maxVal = new float[3] { v._coords.X, v._coords.Y, v._coords.Z };
            var maxVert = new MeshUtils.Vertex[3] { v, v, v };

            for (; v != _mesh._vHead; v = v._next)
            {
                if (v._coords.X < minVal[0]) { minVal[0] = v._coords.X; minVert[0] = v; }
                if (v._coords.Y < minVal[1]) { minVal[1] = v._coords.Y; minVert[1] = v; }
                if (v._coords.Z < minVal[2]) { minVal[2] = v._coords.Z; minVert[2] = v; }
                if (v._coords.X > maxVal[0]) { maxVal[0] = v._coords.X; maxVert[0] = v; }
                if (v._coords.Y > maxVal[1]) { maxVal[1] = v._coords.Y; maxVert[1] = v; }
                if (v._coords.Z > maxVal[2]) { maxVal[2] = v._coords.Z; maxVert[2] = v; }
            }

            // Find two vertices separated by at least 1/sqrt(3) of the maximum
            // distance between any two vertices
            int i = 0;
            if (maxVal[1] - minVal[1] > maxVal[0] - minVal[0]) { i = 1; }
            if (maxVal[2] - minVal[2] > maxVal[i] - minVal[i]) { i = 2; }
            if (minVal[i] >= maxVal[i])
            {
                // All vertices are the same -- normal doesn't matter
                norm = new Vec3 { X = 0, Y = 0, Z = 1 };
                return;
            }

            // Look for a third vertex which forms the triangle with maximum area
            // (Length of normal == twice the triangle area)
            float maxLen2 = 0, tLen2;
            var v1 = minVert[i];
            var v2 = maxVert[i];
            Vec3 d1, d2, tNorm;
            Vec3.Sub(ref v1._coords, ref v2._coords, out d1);
            for (v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                Vec3.Sub(ref v._coords, ref v2._coords, out d2);
                tNorm.X = d1.Y * d2.Z - d1.Z * d2.Y;
                tNorm.Y = d1.Z * d2.X - d1.X * d2.Z;
                tNorm.Z = d1.X * d2.Y - d1.Y * d2.X;
                tLen2 = tNorm.X*tNorm.X + tNorm.Y*tNorm.Y + tNorm.Z*tNorm.Z;
                if (tLen2 > maxLen2)
                {
                    maxLen2 = tLen2;
                    norm = tNorm;
                }
            }

            if (maxLen2 <= 0.0f)
            {
                // All points lie on a single line -- any decent normal will do
                norm = Vec3.Zero;
                i = Vec3.LongAxis(ref d1);
                norm[i] = 1;
            }
        }

        private void CheckOrientation()
        {
            // When we compute the normal automatically, we choose the orientation
            // so that the the sum of the signed areas of all contours is non-negative.
            float area = 0.0f;
            for (var f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (f._anEdge._winding <= 0)
                {
                    continue;
                }
                area += MeshUtils.FaceArea(f);
            }
            if (area < 0.0f)
            {
                // Reverse the orientation by flipping all the t-coordinates
                for (var v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
                {
                    v._t = -v._t;
                }
                Vec3.Neg(ref _tUnit);
            }
        }

        private void ProjectPolygon()
        {
            var norm = _normal;

            bool computedNormal = false;
            if (norm.X == 0.0f && norm.Y == 0.0f && norm.Z == 0.0f)
            {
                ComputeNormal(ref norm);
                _normal = norm;
                computedNormal = true;
            }

            int i = Vec3.LongAxis(ref norm);

            _sUnit[i] = 0;
            _sUnit[(i + 1) % 3] = SUnitX;
            _sUnit[(i + 2) % 3] = SUnitY;

            _tUnit[i] = 0;
            _tUnit[(i + 1) % 3] = norm[i] > 0.0f ? -SUnitY : SUnitY;
            _tUnit[(i + 2) % 3] = norm[i] > 0.0f ? SUnitX : -SUnitX;

            // Project the vertices onto the sweep plane
            for (var v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                Vec3.Dot(ref v._coords, ref _sUnit, out v._s);
                Vec3.Dot(ref v._coords, ref _tUnit, out v._t);
            }
            if (computedNormal)
            {
                CheckOrientation();
            }

            // Compute ST bounds.
            bool first = true;
            for (var v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                if (first)
                {
                    _bminX = _bmaxX = v._s;
                    _bminY = _bmaxY = v._t;
                    first = false;
                }
                else
                {
                    if (v._s < _bminX) _bminX = v._s;
                    if (v._s > _bmaxX) _bmaxX = v._s;
                    if (v._t < _bminY) _bminY = v._t;
                    if (v._t > _bmaxY) _bmaxY = v._t;
                }
            }
        }

        /// <summary>
        /// TessellateMonoRegion( face ) tessellates a monotone region
        /// (what else would it do??)  The region must consist of a single
        /// loop of half-edges (see mesh.h) oriented CCW.  "Monotone" in this
        /// case means that any vertical line intersects the interior of the
        /// region in a single interval.  
        /// 
        /// Tessellation consists of adding interior edges (actually pairs of
        /// half-edges), to split the region into non-overlapping triangles.
        /// 
        /// The basic idea is explained in Preparata and Shamos (which I don't
        /// have handy right now), although their implementation is more
        /// complicated than this one.  The are two edge chains, an upper chain
        /// and a lower chain.  We process all vertices from both chains in order,
        /// from right to left.
        /// 
        /// The algorithm ensures that the following invariant holds after each
        /// vertex is processed: the untessellated region consists of two
        /// chains, where one chain (say the upper) is a single edge, and
        /// the other chain is concave.  The left vertex of the single edge
        /// is always to the left of all vertices in the concave chain.
        /// 
        /// Each step consists of adding the rightmost unprocessed vertex to one
        /// of the two chains, and forming a fan of triangles from the rightmost
        /// of two chain endpoints.  Determining whether we can add each triangle
        /// to the fan is a simple orientation test.  By making the fan as large
        /// as possible, we restore the invariant (check it yourself).
        /// </summary>
        private void TessellateMonoRegion(MeshUtils.Face face)
        {
            // All edges are oriented CCW around the boundary of the region.
            // First, find the half-edge whose origin vertex is rightmost.
            // Since the sweep goes from left to right, face->anEdge should
            // be close to the edge we want.
            var up = face._anEdge;
            Debug.Assert(up._Lnext != up && up._Lnext._Lnext != up);

            while (Geom.VertLeq(up._Dst, up._Org)) up = up._Lprev;
            while (Geom.VertLeq(up._Org, up._Dst)) up = up._Lnext;

            var lo = up._Lprev;

            while (up._Lnext != lo)
            {
                if (Geom.VertLeq(up._Dst, lo._Org))
                {
                    // up.Dst is on the left. It is safe to form triangles from lo.Org.
                    // The EdgeGoesLeft test guarantees progress even when some triangles
                    // are CW, given that the upper and lower chains are truly monotone.
                    while (lo._Lnext != up && (Geom.EdgeGoesLeft(lo._Lnext)
                        || Geom.EdgeSign(lo._Org, lo._Dst, lo._Lnext._Dst) <= 0.0f))
                    {
                        lo = _mesh.Connect(lo._Lnext, lo)._Sym;
                    }
                    lo = lo._Lprev;
                }
                else
                {
                    // lo.Org is on the left.  We can make CCW triangles from up.Dst.
                    while (lo._Lnext != up && (Geom.EdgeGoesRight(up._Lprev)
                        || Geom.EdgeSign(up._Dst, up._Org, up._Lprev._Org) >= 0.0f))
                    {
                        up = _mesh.Connect(up, up._Lprev)._Sym;
                    }
                    up = up._Lnext;
                }
            }

            // Now lo.Org == up.Dst == the leftmost vertex.  The remaining region
            // can be tessellated in a fan from this leftmost vertex.
            Debug.Assert(lo._Lnext != up);
            while (lo._Lnext._Lnext != up)
            {
                lo = _mesh.Connect(lo._Lnext, lo)._Sym;
            }
        }

        /// <summary>
        /// TessellateInterior( mesh ) tessellates each region of
        /// the mesh which is marked "inside" the polygon. Each such region
        /// must be monotone.
        /// </summary>
        private void TessellateInterior()
        {
            MeshUtils.Face f, next;
            for (f = _mesh._fHead._next; f != _mesh._fHead; f = next)
            {
                // Make sure we don't try to tessellate the new triangles.
                next = f._next;
                if (f._inside)
                {
                    TessellateMonoRegion(f);
                }
            }
        }

        /// <summary>
        /// DiscardExterior zaps (ie. sets to null) all faces
        /// which are not marked "inside" the polygon.  Since further mesh operations
        /// on NULL faces are not allowed, the main purpose is to clean up the
        /// mesh so that exterior loops are not represented in the data structure.
        /// </summary>
        private void DiscardExterior()
        {
            MeshUtils.Face f, next;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = next)
            {
                // Since f will be destroyed, save its next pointer.
                next = f._next;
                if( ! f._inside ) {
                    _mesh.ZapFace(f);
                }
            }
        }

        /// <summary>
        /// SetWindingNumber( value, keepOnlyBoundary ) resets the
        /// winding numbers on all edges so that regions marked "inside" the
        /// polygon have a winding number of "value", and regions outside
        /// have a winding number of 0.
        /// 
        /// If keepOnlyBoundary is TRUE, it also deletes all edges which do not
        /// separate an interior region from an exterior one.
        /// </summary>
        private void SetWindingNumber(int value, bool keepOnlyBoundary)
        {
            MeshUtils.Edge e, eNext;

            for (e = _mesh._eHead._next; e != _mesh._eHead; e = eNext)
            {
                eNext = e._next;
                if (e._Rface._inside != e._Lface._inside)
                {

                    /* This is a boundary edge (one side is interior, one is exterior). */
                    e._winding = (e._Lface._inside) ? value : -value;
                }
                else
                {

                    /* Both regions are interior, or both are exterior. */
                    if (!keepOnlyBoundary)
                    {
                        e._winding = 0;
                    }
                    else
                    {
                        _mesh.Delete(e);
                    }
                }
            }

        }

        private int GetNeighbourFace(MeshUtils.Edge edge)
        {
            if (edge._Rface == null)
                return MeshUtils.Undef;
            if (!edge._Rface._inside)
                return MeshUtils.Undef;
            return edge._Rface._n;
        }

        private void OutputPolymesh(ElementType elementType, int polySize)
        {
            MeshUtils.Vertex v;
            MeshUtils.Face f;
            MeshUtils.Edge edge;
            int maxFaceCount = 0;
            int maxVertexCount = 0;
            int faceVerts, i;

            if (polySize < 3)
            {
                polySize = 3;
            }
            // Assume that the input data is triangles now.
            // Try to merge as many polygons as possible
            if (polySize > 3)
            {
                _mesh.MergeConvexFaces(polySize);
            }

            // Mark unused
            for (v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
                v._n = MeshUtils.Undef;

            // Create unique IDs for all vertices and faces.
            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                f._n = MeshUtils.Undef;
                if (!f._inside) continue;

                if (NoEmptyPolygons)
                {
                    var area = MeshUtils.FaceArea(f);
                    if (Math.Abs(area) < float.Epsilon)
                    {
                        continue;
                    }
                }

                edge = f._anEdge;
                faceVerts = 0;
                do {
                    v = edge._Org;
                    if (v._n == MeshUtils.Undef)
                    {
                        v._n = maxVertexCount;
                        maxVertexCount++;
                    }
                    faceVerts++;
                    edge = edge._Lnext;
                }
                while (edge != f._anEdge);

                Debug.Assert(faceVerts <= polySize);

                f._n = maxFaceCount;
                ++maxFaceCount;
            }

            _elementCount = maxFaceCount;
            if (elementType == ElementType.ConnectedPolygons)
                maxFaceCount *= 2;
            _elements = new int[maxFaceCount * polySize];

            _vertexCount = maxVertexCount;
            _vertices = new ContourVertex[_vertexCount];

            // Output vertices.
            for (v = _mesh._vHead._next; v != _mesh._vHead; v = v._next)
            {
                if (v._n != MeshUtils.Undef)
                {
                    // Store coordinate
                    _vertices[v._n].Position = v._coords;
                    _vertices[v._n].Data = v._data;
                }
            }

            // Output indices.
            int elementIndex = 0;
            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (!f._inside) continue;

                if (NoEmptyPolygons)
                {
                    var area = MeshUtils.FaceArea(f);
                    if (Math.Abs(area) < float.Epsilon)
                    {
                        continue;
                    }
                }

                // Store polygon
                edge = f._anEdge;
                faceVerts = 0;
                do {
                    v = edge._Org;
                    _elements[elementIndex++] = v._n;
                    faceVerts++;
                    edge = edge._Lnext;
                } while (edge != f._anEdge);
                // Fill unused.
                for (i = faceVerts; i < polySize; ++i)
                {
                    _elements[elementIndex++] = MeshUtils.Undef;
                }

                // Store polygon connectivity
                if (elementType == ElementType.ConnectedPolygons)
                {
                    edge = f._anEdge;
                    do
                    {
                        _elements[elementIndex++] = GetNeighbourFace(edge);
                        edge = edge._Lnext;
                    } while (edge != f._anEdge);
                    // Fill unused.
                    for (i = faceVerts; i < polySize; ++i)
                    {
                        _elements[elementIndex++] = MeshUtils.Undef;
                    }
                }
            }
        }

        private void OutputContours()
        {
            MeshUtils.Face f;
            MeshUtils.Edge edge, start;
            int startVert = 0;
            int vertCount = 0;

            _vertexCount = 0;
            _elementCount = 0;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (!f._inside) continue;

                start = edge = f._anEdge;
                do
                {
                    ++_vertexCount;
                    edge = edge._Lnext;
                }
                while (edge != start);

                ++_elementCount;
            }

            _elements = new int[_elementCount * 2];
            _vertices = new ContourVertex[_vertexCount];

            int vertIndex = 0;
            int elementIndex = 0;

            startVert = 0;

            for (f = _mesh._fHead._next; f != _mesh._fHead; f = f._next)
            {
                if (!f._inside) continue;

                vertCount = 0;
                start = edge = f._anEdge;
                do {
                    _vertices[vertIndex].Position = edge._Org._coords;
                    _vertices[vertIndex].Data = edge._Org._data;
                    ++vertIndex;
                    ++vertCount;
                    edge = edge._Lnext;
                } while (edge != start);

                _elements[elementIndex++] = startVert;
                _elements[elementIndex++] = vertCount;

                startVert += vertCount;
            }
        }

        private float SignedArea(ContourVertex[] vertices)
        {
            float area = 0.0f;

            for (int i = 0; i < vertices.Length; i++)
            {
                var v0 = vertices[i];
                var v1 = vertices[(i + 1) % vertices.Length];

                area += v0.Position.X * v1.Position.Y;
                area -= v0.Position.Y * v1.Position.X;
            }

            return 0.5f * area;
        }

        public void AddContour(ContourVertex[] vertices)
        {
            AddContour(vertices, ContourOrientation.Original);
        }

        public void AddContour(ContourVertex[] vertices, ContourOrientation forceOrientation)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh();
            }

            bool reverse = false;
            if (forceOrientation != ContourOrientation.Original)
            {
                var area = SignedArea(vertices);
                reverse = (forceOrientation == ContourOrientation.Clockwise && area < 0.0f) || (forceOrientation == ContourOrientation.CounterClockwise && area > 0.0f);
            }

            MeshUtils.Edge e = null;
            for (int i = 0; i < vertices.Length; ++i)
            {
                if (e == null)
                {
                    e = _mesh.MakeEdge();
                    _mesh.Splice(e, e._Sym);
                }
                else
                {
                    // Create a new vertex and edge which immediately follow e
                    // in the ordering around the left face.
                    _mesh.SplitEdge(e);
                    e = e._Lnext;
                }

                int index = reverse ? vertices.Length - 1 - i : i;
                // The new vertex is now e._Org.
                e._Org._coords = vertices[index].Position;
                e._Org._data = vertices[index].Data;

                // The winding of an edge says how the winding number changes as we
                // cross from the edge's right face to its left face.  We add the
                // vertices in such an order that a CCW contour will add +1 to
                // the winding number of the region inside the contour.
                e._winding = 1;
                e._Sym._winding = -1;
            }
        }

        public void Tessellate(WindingRule windingRule, ElementType elementType, int polySize)
        {
            Tessellate(windingRule, elementType, polySize, null);
        }

        public void Tessellate(WindingRule windingRule, ElementType elementType, int polySize, CombineCallback combineCallback)
        {
            _normal = Vec3.Zero;
            _vertices = null;
            _elements = null;

            _windingRule = windingRule;
            _combineCallback = combineCallback;

            if (_mesh == null)
            {
                return;
            }

            // Determine the polygon normal and project vertices onto the plane
            // of the polygon.
            ProjectPolygon();

            // ComputeInterior computes the planar arrangement specified
            // by the given contours, and further subdivides this arrangement
            // into regions.  Each region is marked "inside" if it belongs
            // to the polygon, according to the rule given by windingRule.
            // Each interior region is guaranteed be monotone.
            ComputeInterior();

            // If the user wants only the boundary contours, we throw away all edges
            // except those which separate the interior from the exterior.
            // Otherwise we tessellate all the regions marked "inside".
            if (elementType == ElementType.BoundaryContours)
            {
                SetWindingNumber(1, true);
            }
            else
            {
                TessellateInterior();
            }

            _mesh.Check();

            if (elementType == ElementType.BoundaryContours)
            {
                OutputContours();
            }
            else
            {
                OutputPolymesh(elementType, polySize);
            }

            if (UsePooling)
            {
                _mesh.Free();
            }
            _mesh = null;
        }
    }
}
