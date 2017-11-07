/*
Copyright (C) 2017 Nolan Baker

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, 
and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions 
of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
DEALINGS IN THE SOFTWARE.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ClipperLib;

namespace Tiled {
public class TileMap : MonoBehaviour {

    [Tooltip("Fixes UV bleed from unpadded tilesets by insetting the corners of the tile.")] 
    [Range(0, 0.01f)] public float uvInset = 0;
    public Vector2 pivot;

    [HideInInspector] public string tmxFilePath;
    public TMXFile tmxFile;

    [HideInInspector] public float pixelsPerUnit = -1;
    [HideInInspector] public Vector4 tileOffset;
    [HideInInspector] public Vector2 offset;
    [HideInInspector] public GameObject[] layers;
    [HideInInspector] public Material[] tileSetMaterials;

    public Bounds bounds {
        get {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) {
                b.Encapsulate(renderers[i].bounds);
            }
            b.size *= 0.5f;
            return b;
        }
    }

    private GameObject[][] _layerSubmeshObjects;
    private GameObject[][] layerSubmeshObjects {
        get {
            if (_layerSubmeshObjects == null) {
                _layerSubmeshObjects = new GameObject[layers.Length][];
                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++) {
                    MeshRenderer[] renderers = layers[layerIndex].GetComponentsInChildren<MeshRenderer>();
                    _layerSubmeshObjects[layerIndex] = System.Array.ConvertAll(renderers, (r) => r.gameObject);
                }
            }
            return _layerSubmeshObjects;
        }
    }

    private List<List<IntPoint>>[][] _layerPaths;
    private List<List<IntPoint>>[][] layerPaths {
        get {
            if (_layerPaths == null) {
                _layerPaths = new List<List<IntPoint>>[layers.Length][];
                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++) {
                    _layerPaths[layerIndex] = new List<List<IntPoint>>[meshesPerLayer];
                }
            }
            return _layerPaths;
        }
    }

    Dictionary<int, Vector3[]> _idToPhysics;
    Dictionary<int, Vector3[]> idToPhysics {
        get {
            if (_idToPhysics == null) {
                _idToPhysics = new Dictionary<int, Vector3[]>();
                foreach (TileSet tileSet in tmxFile.tileSets) {
                    if (tileSet.tiles == null) continue;
                    foreach (Tile tile in tileSet.tiles) {
                        if (tile.objectGroup != null && tile.objectGroup.objects != null && tile.objectGroup.objects.Length > 0) {
                            TileObject tileObject = tile.objectGroup.objects[0];
                            float x = tileOffset.x * (float)tileObject.x / (float)tileSet.tileWidth;
                            float y = tileOffset.y * (float)tileObject.y / (float)tileSet.tileHeight;
                            if (tileObject.polygonSpecified) {
                                idToPhysics[tile.id + tileSet.firstGID] = System.Array.ConvertAll(tileObject.polygon.path, (p) => {
                                    Vector3 v = new Vector3(x, y, 0);
                                    v.x += tileOffset.x * (float)p.x / (float)tileSet.tileWidth;
                                    v.y -= tileOffset.x * (float)p.y / (float)tileSet.tileHeight;
                                    return v;
                                });
                            }
                            else {
                                float width = tileOffset.x * (float)tileObject.width / (float)tileSet.tileWidth;
                                float height = tileOffset.y * (float)tileObject.height / (float)tileSet.tileHeight;
                                idToPhysics[tile.id + tileSet.firstGID] = new Vector3[] {
                                    new Vector3(x,         y,          0),
                                    new Vector3(x,         y + height, 0),
                                    new Vector3(x + width, y + height, 0),
                                    new Vector3(x + width, y,          0)
                                };
                            }
                        }
                    }
                }
            }
            return _idToPhysics;
        }
    }


    private int meshesPerLayer {
        get { return 1 + tmxFile.width * tmxFile.height / 16250; }
    }

    public void Setup (TMXFile tmxFile, string tmxFilePath, float pixelsPerUnit = -1) {
        this.tmxFilePath = tmxFilePath;
        this.tmxFile = tmxFile;
        this.pixelsPerUnit = pixelsPerUnit;
        Setup();
    }

    public void Setup () {
        if (pixelsPerUnit < 0) pixelsPerUnit = tmxFile.tileWidth;

        tileOffset = new Vector4(tmxFile.tileWidth, -tmxFile.tileHeight, tmxFile.tileWidth, -tmxFile.tileHeight);
        offset = new Vector2(
            pivot.x * -tmxFile.tileWidth * tmxFile.width / pixelsPerUnit,
            (1 - pivot.y) * tmxFile.tileHeight * tmxFile.height / pixelsPerUnit
        );

        if (tmxFile.orientation == "hexagonal" && tmxFile.hexSideLength != null) {
            if (tmxFile.staggerAxis == "x") tileOffset.z = tmxFile.tileWidth - tmxFile.hexSideLength.Value * 0.5f;
            else tileOffset.w = -tmxFile.tileHeight + tmxFile.hexSideLength.Value * 0.5f;
        }
        else if (tmxFile.orientation == "staggered") {
            if (tmxFile.staggerAxis == "x") tileOffset.z -= tileOffset.x * 0.5f;
            else tileOffset.w -= tileOffset.y *0.5f;
        }

        tileOffset *= 1f / pixelsPerUnit;

        if (layers != null) {
            foreach (GameObject layer in layers) {
                DestroyImmediate(layer);
            }
        }

        layers = new GameObject[tmxFile.layers.Length];
        _layerSubmeshObjects = new GameObject[tmxFile.layers.Length][];
        for (int i = 0; i < tmxFile.layers.Length; i++) {
            CreateTileLayer(i);
        }
    }

    public void CreateTileLayer (int layerIndex) {
        Layer layerData = tmxFile.layers[layerIndex];
        GameObject layer = new GameObject(layerData.name);
        layer.transform.SetParent(transform);
        layer.transform.localPosition = Vector3.zero;
        layers[layerIndex] = layer;

        _layerSubmeshObjects[layerIndex] = new GameObject[meshesPerLayer];
        for (int submeshIndex = 0; submeshIndex < meshesPerLayer; submeshIndex++) {
            GameObject meshObject = layer;
            if (meshesPerLayer > 1) {
                meshObject = new GameObject(layerData.name + "_submesh" + submeshIndex);
                meshObject.transform.SetParent(layer.transform);
            }
            meshObject.AddComponent<MeshRenderer>();
            meshObject.AddComponent<MeshFilter>();
            meshObject.AddComponent<PolygonCollider2D>();
            SortingGroup sort = meshObject.AddComponent<SortingGroup>();
            sort.sortingOrder = layerIndex;
            sort.sortingLayerName = layerData.name;

            _layerSubmeshObjects[layerIndex][submeshIndex] = meshObject;

            UpdateMesh(layerIndex, submeshIndex);
            UpdatePolygonCollider(layerIndex, submeshIndex);
        }        
    }

    public void ReloadMap () {
        for (int layerIndex = 0; layerIndex < tmxFile.layers.Length; layerIndex++) {
            for (int submeshIndex = 0; submeshIndex < meshesPerLayer; submeshIndex++) {
                UpdateMesh(layerIndex, submeshIndex);
            }
            UpdatePolygonColliders(layerIndex);
        }
    }

    private IntPoint Vector2ToIntPoint (Vector2 p) {
        int x = (int)(p.x * pixelsPerUnit);
        int y = (int)(p.y * pixelsPerUnit);
        return new IntPoint(x, y);
    }

    private Vector2 IntPointToVector2 (IntPoint p) {
        float x = (float)p.X / pixelsPerUnit;
        float y = (float)p.Y / pixelsPerUnit;
        return new Vector2(x, y);
    }

    public void UpdateMesh(int layerIndex, int submeshIndex) {
        Layer layerData = tmxFile.layers[layerIndex];

        Dictionary<int, int> firstGIDToIndex = new Dictionary<int, int>();
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            firstGIDToIndex[tmxFile.tileSets[i].firstGID] = i;
        }

        int vertCount = 0;
        int[] triIDsPerMat = new int[tmxFile.tileSets.Length];
        for (int tileIndex = submeshIndex * 16250; tileIndex < Mathf.Min((submeshIndex+1) * 16250, layerData.tileIDs.Length); tileIndex++) {
            int tileID = layerData.tileIDs[tileIndex];
            TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
            if (tileSet == null || tileID < tileSet.firstGID) continue;

            vertCount += 4;
            triIDsPerMat[firstGIDToIndex[tileSet.firstGID]] += 6;
        }


        List<List<IntPoint>> paths = new List<List<IntPoint>>();
        Vector3[] verts = new Vector3[vertCount];
        Vector3[] norms = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color[] colors = new Color[vertCount];
        
        int[][] matTris = new int[tmxFile.tileSets.Length][];
        for (int i = 0; i < triIDsPerMat.Length; i++) {
            matTris[i] = new int[triIDsPerMat[i]];
        }

        bool isHexMap = tmxFile.orientation == "hexagonal";
        bool isIsoMap = tmxFile.orientation == "isometric";
        bool isStagMap = tmxFile.orientation == "staggered";

        bool staggerX = tmxFile.staggerAxis == "x";
        int staggerIndex = (tmxFile.staggerAxis == "even") ? 0 : 1;

        Color white = Color.white;
        int vertIndex = 0;      
        int[] matTriIndices = new int[tmxFile.tileSets.Length];
        for (int tileIndex = submeshIndex * 16250; tileIndex < Mathf.Min((submeshIndex+1) * 16250, layerData.tileIDs.Length); tileIndex++) {        
            int tileID = layerData.tileIDs[tileIndex];
            TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
            if (tileSet == null || tileID < tileSet.firstGID) continue;

            if (tileSet.columns == 0 || tileSet.rows == 0) {
                if (tileSet.image.width == 0 || tileSet.image.height == 0) {
                    int tileSetIndex = System.Array.IndexOf(tmxFile.tileSets, tileSet);
                    Texture2D tex = tileSetMaterials[tileSetIndex].mainTexture as Texture2D;
                    tileSet.image.width = tex.width;
                    tileSet.image.height = tex.height;
                }
                tileSet.columns = (tileSet.image.width - 2 * tileSet.margin) / (tileSet.tileWidth + tileSet.spacing);
                tileSet.rows = (tileSet.image.width - 2 * tileSet.margin) / (tileSet.tileWidth + tileSet.spacing);
            }
            
            TileRect uvRect = tileSet.GetTileUVs(tileID, uvInset);

            bool flipX = layerData.FlippedHorizontally(tileIndex);
            bool flipY = layerData.FlippedVertically(tileIndex);
            bool flipAntiDiag = layerData.FlippedAntiDiagonally(tileIndex);
            bool rotated120 = layerData.RotatedHexagonal120(tileIndex);

            TilePoint tileLocation = layerData.GetTileLocation(tileIndex);
            Vector3 pos = new Vector3(offset.x + tileLocation.x * tileOffset.x, offset.y + tileLocation.y * tileOffset.y, 0);
            if (isHexMap || isStagMap) {
                if (staggerX) {
                    pos.x = tileLocation.x * tileOffset.z;
                    if (tileLocation.x % 2 == staggerIndex) pos.y += tileOffset.w * 0.5f;
                }
                else {
                    pos.y = tileLocation.y * tileOffset.w;
                    if (tileLocation.y % 2 == staggerIndex) pos.x += tileOffset.z * 0.5f;
                }
            }
            else if (isIsoMap) {
                pos.x = (tileLocation.x * tileOffset.x  / 2) - (tileLocation.y * tileOffset.x  / 2);
                pos.y = (tileLocation.y * tileOffset.y / 2) + (tileLocation.x * tileOffset.y / 2);
            }

            float widthMult = (float)tileSet.tileWidth / (float)tmxFile.tileWidth;
            float heightMult = (float)tileSet.tileHeight / (float)tmxFile.tileHeight;
            Vector3[] v = new Vector3[] {
                pos,
                pos + Vector3.up * tileOffset.y * heightMult,
                pos + new Vector3(tileOffset.x * widthMult, tileOffset.y * heightMult, 0),
                pos + Vector3.right * tileOffset.x * widthMult
            };

            if (rotated120 || (flipAntiDiag && isHexMap)) {
                float angle = rotated120 ? 120 : 0;
                angle += flipAntiDiag ? 60 : 0;
                Vector3 center = (v[0] + v[2]) * 0.5f;
                for (int i = 0; i < 4; i++) {
                    v[i] = Quaternion.Euler(0,0,-angle) * (v[i] - center) + center;
                }
            }

            verts[vertIndex] = v[0];
            verts[vertIndex+1] = v[1];
            verts[vertIndex+2] = v[2];
            verts[vertIndex+3] = v[3];

            if (idToPhysics.ContainsKey(tileID)) {
                Vector3[] phys = idToPhysics[tileID];
                IntPoint[] path = System.Array.ConvertAll(phys, (p) => Vector2ToIntPoint((Vector2)(p + pos)));
                paths.Add(new List<IntPoint>(path));
            }

            norms[vertIndex] = Vector3.forward;
            norms[vertIndex+1] = Vector3.forward;
            norms[vertIndex+2] = Vector3.forward;
            norms[vertIndex+3] = Vector3.forward;

            float left = uvRect.left;
            float right = uvRect.right;
            float bottom = uvRect.bottom;
            float top = uvRect.top;

            Vector2[] uvArray = new Vector2[] {
                new Vector2(left, bottom),
                new Vector2(left, top),
                new Vector2(right, top),
                new Vector2(right, bottom)
            };

            if (tileOffset.x < 0 != flipX) {
                uvArray = new Vector2[] {
                    new Vector2(right, bottom),
                    new Vector2(right, top),
                    new Vector2(left, top),
                    new Vector2(left, bottom)
                };
            }

            if (tileOffset.y < 0 != flipY) {
                uvArray = new Vector2[]{uvArray[1], uvArray[0], uvArray[3], uvArray[2]};
            }

            if (flipAntiDiag && !isHexMap) {
                Vector2 tmp = uvArray[0];
                uvArray[0] = uvArray[2];
                uvArray[2] = tmp;
            }

            uvs[vertIndex] = uvArray[0];
            uvs[vertIndex+1] = uvArray[1];
            uvs[vertIndex+2] = uvArray[2];
            uvs[vertIndex+3] = uvArray[3];

            colors[vertIndex] = white;
            colors[vertIndex+1] = white;
            colors[vertIndex+2] = white;
            colors[vertIndex+3] = white;

            int matIndex = firstGIDToIndex[tileSet.firstGID];
            matTris[matIndex][matTriIndices[matIndex]] = vertIndex;
            matTris[matIndex][matTriIndices[matIndex]+1] = vertIndex+2;
            matTris[matIndex][matTriIndices[matIndex]+2] = vertIndex+1;
            matTris[matIndex][matTriIndices[matIndex]+3] = vertIndex;
            matTris[matIndex][matTriIndices[matIndex]+4] = vertIndex+3;
            matTris[matIndex][matTriIndices[matIndex]+5] = vertIndex+2;

            matTriIndices[matIndex] += 6;
            vertIndex += 4;
        }

        GameObject obj = layerSubmeshObjects[layerIndex][submeshIndex];
        MeshFilter filter = obj.GetComponent<MeshFilter>();
        if (filter.sharedMesh == null) {
            filter.sharedMesh = new Mesh();
            filter.sharedMesh.name = layerData.name;
            if (meshesPerLayer > 1) {
                filter.sharedMesh.name += "_submesh" + submeshIndex;
            }
        }
        filter.sharedMesh.Clear();            
        filter.sharedMesh.vertices = verts;
        filter.sharedMesh.normals = norms;
        filter.sharedMesh.uv = uvs;
        filter.sharedMesh.colors = colors;
        
        List<Material> mats = new List<Material>();
        List<int[]> triLists = new List<int[]>();       
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            int[] tris = matTris[i];
            if (tris != null && tris.Length > 0) {
                mats.Add(tileSetMaterials[i]);
                triLists.Add(tris);
            }
        }
        obj.GetComponent<MeshRenderer>().sharedMaterials = mats.ToArray();

        filter.sharedMesh.subMeshCount = mats.Count;
        for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) {
            filter.sharedMesh.SetTriangles(triLists[i], i);
        }

        filter.sharedMesh.RecalculateBounds();


        paths = Clipper.SimplifyPolygons(paths, PolyFillType.pftNonZero);
        paths = RemoveColinnearAndDoubles(paths);

        layerPaths[layerIndex][submeshIndex] = paths;
    }

    List<List<IntPoint>> RemoveColinnearAndDoubles (List<List<IntPoint>> paths, int minAngle = 1, int minDist = 1) {
        for (int i = 0; i < paths.Count; i++) {
            List<IntPoint> path = paths[i];
            List<IntPoint> newPath = new List<IntPoint>(path);
            float ang = 0;
            for (int j = 1; j < path.Count-1; j++) {
                Vector2 a = new Vector2(path[j].X - path[j-1].X, path[j].Y - path[j-1].Y);
                Vector2 b = new Vector2(path[j+1].X - path[j].X, path[j+1].Y - path[j].Y);
                ang = Vector2.Angle(a, b);
                if (Mathf.Abs(ang) < 5 || a.magnitude < minDist) {
                    newPath.Remove(path[j]);
                }
            }
            paths[i] = newPath;
        }

        return paths;
    }

    public void UpdatePolygonCollider (int layerIndex, int submeshIndex) {
        if (layerPaths[layerIndex].Length <= submeshIndex) return;
        List<List<IntPoint>> paths = layerPaths[layerIndex][submeshIndex];
        if (paths == null) return;
        PolygonCollider2D poly = layerSubmeshObjects[layerIndex][submeshIndex].GetComponent<PolygonCollider2D>();
        poly.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++) {
            Vector2[] path = System.Array.ConvertAll(paths[i].ToArray(), (p) => IntPointToVector2(p));
            poly.SetPath(i, path);
        }
    }

    public void UpdatePolygonColliders (int layerIndex) {
        for (int submeshIndex = 0; submeshIndex < layers.Length; submeshIndex++) {
            UpdatePolygonCollider(layerIndex, submeshIndex);
        }
    }

    public bool SetTile (int tileID, int layerIndex, Vector3 pos, bool updateMesh = true) {
        int x = Mathf.FloorToInt((pos.x - offset.x) / tileOffset.x);
        int y = Mathf.FloorToInt((pos.y - offset.y) / tileOffset.y);
        return SetTile(tileID, layerIndex, x, y, updateMesh);
    }

    public bool SetTiles (int tileID, int layerIndex, Vector3 start, Vector3 end, bool updateMesh = true) {
        bool tileSet = false;
        float d = 0.5f * Mathf.Clamp01(tileOffset.magnitude / Vector3.Distance(start, end));
        List<int> changedSubmeshes = new List<int>();
        for (float t = 0; t <= 1; t += d) {
            Vector3 pos = Vector3.Lerp(start, end, t);
            int x = Mathf.FloorToInt((pos.x - offset.x) / tileOffset.x);
            int y = Mathf.FloorToInt((pos.y - offset.y) / tileOffset.y);
            if (SetTile(tileID, layerIndex, x, y, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
                tileSet = true;
            }
        }
        if (updateMesh) {
            foreach (int submeshIndex in changedSubmeshes) UpdateMesh(layerIndex, submeshIndex);
        }
        return tileSet;
    }

    public bool SetTerrain (int tileID, int layerIndex, Vector3 pos, bool updateMesh = true) {
        int x = Mathf.FloorToInt((pos.x - offset.x) / tileOffset.x);
        int y = Mathf.FloorToInt((pos.y - offset.y) / tileOffset.y);
        Layer layer = tmxFile.layers[layerIndex];
        TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
        int gid = tileSet.firstGID;
        List<int> changedSubmeshes = new List<int>();

        Tile center = tmxFile.GetTile(tileSet, tmxFile.GetTile(tileSet, tileID+gid).terrain);
        int[] c = center.terrain;
        if (!SetTile(center.id+gid, layerIndex, x, y, false)) return false;

        Tile topLeft = tmxFile.GetTile(layer, x-1, y-1);
        if (topLeft != null && topLeft.terrain != null) {
            int[] n = topLeft.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], n[1], n[2], c[0]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x-1, y-1, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }

        Tile top = tmxFile.GetTile(layer, x, y-1);
        if (top != null && top.terrain != null) {
            int[] n = top.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], n[1], c[0], c[1]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x, y-1, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }
        
        Tile topRight = tmxFile.GetTile(layer, x+1, y-1);
        if (topRight != null && topRight.terrain != null) {
            int[] n = topRight.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], n[1], c[1], n[3]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x+1, y-1, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }
        
        Tile right = tmxFile.GetTile(layer, x+1, y);
        if (right != null && right.terrain != null) {
            int[] n = right.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{c[1], n[1], c[3], n[3]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x+1, y, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }
        
        Tile bottomRight = tmxFile.GetTile(layer, x+1, y+1);
        if (bottomRight != null && bottomRight.terrain != null) {
            int[] n = bottomRight.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{c[3], n[1], n[2], n[3]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x+1, y+1, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }
        
        Tile bottom = tmxFile.GetTile(layer, x, y+1);
        if (bottom != null && bottom.terrain != null) {
            int[] n = bottom.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{c[2], c[3], n[2], n[3]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x, y+1, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }
        
        Tile bottomLeft = tmxFile.GetTile(layer, x-1, y+1);
        if (bottomLeft != null && bottomLeft.terrain != null) {
            int[] n = bottomLeft.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], c[2], n[2], n[3]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x-1, y+1, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }
        
        Tile left = tmxFile.GetTile(layer, x-1, y);
        if (left != null && left.terrain != null) {
            int[] n = left.terrain;
            Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], c[0], n[2], c[2]});
            if (tile != null && SetTile(tile.id+gid, layerIndex, x-1, y, false)) {
                if (updateMesh) {
                    int index = x + y * tmxFile.width;
                    int submeshIndex = index / 16250;
                    if (!changedSubmeshes.Contains(submeshIndex)) changedSubmeshes.Add(submeshIndex);
                }
            }
        }

        if (updateMesh) {
            foreach (int submeshIndex in changedSubmeshes) UpdateMesh(layerIndex, submeshIndex);
        }
        return true;
    }

    private int lastTileX = -1;
    private int lastTileY = -1;
    private int lastTileID = -1;
    public bool SetTile (int tileID, int layerIndex, int x, int y, bool updateMesh = true) {
        if (lastTileID == tileID && lastTileX == x && lastTileY == y) return true;
        if (x < 0 || x >= tmxFile.width || y < 0 || y >= tmxFile.height) return false;

        tmxFile.layers[layerIndex].SetTileID(tileID, x, y);
        lastTileX = x;
        lastTileY = y;
        lastTileID = tileID;

        if (updateMesh) {
            int index = x + y * tmxFile.width;
            int submeshIndex = index / 16250;
            UpdateMesh(layerIndex, submeshIndex);
        }
        return true;
    }
}
}