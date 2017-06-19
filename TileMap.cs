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
using ClipperLib;

namespace Tiled {
public class TileMap : MonoBehaviour {

    [Range(0, 0.01f)]public float uvInset = 0;

	public string tmxFilePath;
	public TMXFile tmxFile;

    [HideInInspector] public float pixelsPerUnit = -1;
    [HideInInspector] public Vector4 offset;
    [HideInInspector] public GameObject[] layers;
    [HideInInspector] public Material[] tileSetMaterials;

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

        string[] xyDirections = tmxFile.renderOrder.Split('-');
        offset = new Vector4(
            (xyDirections[0] == "right") ? tmxFile.tileWidth : -tmxFile.tileWidth,
            (xyDirections[1] == "up") ? tmxFile.tileHeight : -tmxFile.tileHeight,
            (xyDirections[0] == "right") ? tmxFile.tileWidth : -tmxFile.tileWidth,
            (xyDirections[1] == "up") ? tmxFile.tileHeight : -tmxFile.tileHeight
        );

        if (tmxFile.orientation == "hexagonal" && tmxFile.hexSideLength != null) {
            if (tmxFile.staggerAxis == "x") offset.z = Mathf.Sign(offset.z) * (tmxFile.tileWidth - tmxFile.hexSideLength.Value * 0.5f);
            else offset.w = Mathf.Sign(offset.w) * (tmxFile.tileHeight - tmxFile.hexSideLength.Value * 0.5f);
        }
        else if (tmxFile.orientation == "staggered") {
            if (tmxFile.staggerAxis == "x") offset.z -= offset.x * 0.5f;
            else offset.w -= offset.y *0.5f;
        }

        offset *= 1f / pixelsPerUnit;

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

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();
        
        List<int>[] matTris = new List<int>[tmxFile.tileSets.Length];
        Dictionary<int, int> firstGIDToIndex = new Dictionary<int, int>();
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            matTris[i] = new List<int>();
            firstGIDToIndex[tmxFile.tileSets[i].firstGID] = i;
        }

        List<List<IntPoint>> paths = new List<List<IntPoint>>();
        Dictionary<int, Vector3[]> idToPhysics = new Dictionary<int, Vector3[]>();
        foreach (TileSet tileSet in tmxFile.tileSets) {
            if (tileSet.tiles == null) continue;
            foreach (Tile tile in tileSet.tiles) {
                if (tile.objectGroup != null && tile.objectGroup.objects != null && tile.objectGroup.objects.Length > 0) {
                    TileObject tileObject = tile.objectGroup.objects[0];
                    float x = offset.x * (float)tileObject.x / (float)tileSet.tileWidth;
                    float y = offset.y * (float)tileObject.y / (float)tileSet.tileHeight;
                    if (tileObject.polygonSpecified) {
                        idToPhysics[tile.id + tileSet.firstGID] = System.Array.ConvertAll(tileObject.polygon.path, (p) => {
                            Vector3 v = new Vector3(x, y, 0);
                            v.x += offset.x * (float)p.x / (float)tileSet.tileWidth;
                            v.y -= offset.x * (float)p.y / (float)tileSet.tileHeight;
                            return v;
                        });
                    }
                    else {
                        float width = offset.x * (float)tileObject.width / (float)tileSet.tileWidth;
                        float height = offset.y * (float)tileObject.height / (float)tileSet.tileHeight;
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

        bool isHexMap = tmxFile.orientation == "hexagonal";
        bool isIsoMap = tmxFile.orientation == "isometric";
        bool isStagMap = tmxFile.orientation == "staggered";

        bool staggerX = tmxFile.staggerAxis == "x";
        int staggerIndex = (tmxFile.staggerAxis == "even") ? 0 : 1;

        int tilesPlaced = 0;        
        for (int tileIndex = submeshIndex * 16250; tileIndex < Mathf.Min((submeshIndex+1) * 16250, layerData.tileIDs.Length); tileIndex++) {        

            int tileID = layerData.tileIDs[tileIndex];
            TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
            if (tileSet == null) continue;

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
            if (uvRect == null) continue;

            bool flipX = layerData.FlippedHorizontally(tileIndex);
            bool flipY = layerData.FlippedVertically(tileIndex);
            bool flipAntiDiag = layerData.FlippedAntiDiagonally(tileIndex);
            bool rotated120 = layerData.RotatedHexagonal120(tileIndex);

            TilePoint tileLocation = layerData.GetTileLocation(tileIndex);
            Vector3 pos = new Vector3(tileLocation.x * offset.x, tileLocation.y * offset.y, 0);
            if (isHexMap || isStagMap) {
                if (staggerX) {
                    pos.x = tileLocation.x * offset.z;
                    if (tileLocation.x % 2 == staggerIndex) pos.y += offset.w * 0.5f;
                }
                else {
                    pos.y = tileLocation.y * offset.w;
                    if (tileLocation.y % 2 == staggerIndex) pos.x += offset.z * 0.5f;
                }
            }
            else if (isIsoMap) {
                pos.x = (tileLocation.x * offset.x  / 2) - (tileLocation.y * offset.x  / 2);
                pos.y = (tileLocation.y * offset.y / 2) + (tileLocation.x * offset.y / 2);
            }

            float widthMult = (float)tileSet.tileWidth / (float)tmxFile.tileWidth;
            float heightMult = (float)tileSet.tileHeight / (float)tmxFile.tileHeight;
            Vector3[] v = new Vector3[] {
                pos,
                pos + Vector3.up * offset.y * heightMult,
                pos + new Vector3(offset.x * widthMult, offset.y * heightMult, 0),
                pos + Vector3.right * offset.x * widthMult
            };

            if (rotated120 || (flipAntiDiag && isHexMap)) {
                float angle = rotated120 ? 120 : 0;
                angle += flipAntiDiag ? 60 : 0;
                Vector3 center = (v[0] + v[2]) * 0.5f;
                for (int i = 0; i < 4; i++) {
                    v[i] = Quaternion.Euler(0,0,-angle) * (v[i] - center) + center;
                }
            }

            verts.AddRange(v);

            if (idToPhysics.ContainsKey(tileID)) {
                Vector3[] phys = idToPhysics[tileID];
                IntPoint[] path = System.Array.ConvertAll(phys, (p) => Vector2ToIntPoint((Vector2)(p + pos)));
                paths.Add(new List<IntPoint>(path));
            }

            norms.AddRange(new Vector3[] {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward
            });

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

            if (offset.x < 0 != flipX) {
                uvArray = new Vector2[] {
                    new Vector2(right, bottom),
                    new Vector2(right, top),
                    new Vector2(left, top),
                    new Vector2(left, bottom)
                };
            }

            if (offset.y < 0 != flipY) {
                uvArray = new Vector2[]{uvArray[1], uvArray[0], uvArray[3], uvArray[2]};
            }

            if (flipAntiDiag && !isHexMap) {
                Vector2 tmp = uvArray[0];
                uvArray[0] = uvArray[2];
                uvArray[2] = tmp;
            }

            uvs.AddRange(uvArray);

            colors.AddRange(new Color[] {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            });

            matTris[firstGIDToIndex[tileSet.firstGID]].AddRange(new int[] {
                tilesPlaced * 4, tilesPlaced * 4 + 2, tilesPlaced * 4 + 1,
                tilesPlaced * 4, tilesPlaced * 4 + 3, tilesPlaced * 4 + 2
            });
            tilesPlaced++;
        }

        GameObject obj = layerSubmeshObjects[layerIndex][submeshIndex];
        
        Mesh mesh = new Mesh();
        
        mesh.name = layerData.name;
        if (meshesPerLayer > 1) mesh.name += "_submesh" + submeshIndex;
        
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        
        List<Material> mats = new List<Material>();
        List<List<int>> triLists = new List<List<int>>();       
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            List<int> tris = matTris[i];
            if (tris != null && tris.Count > 0) {
                mats.Add(tileSetMaterials[i]);
                triLists.Add(tris);
            }
        }
        obj.GetComponent<MeshRenderer>().sharedMaterials = mats.ToArray();

        mesh.subMeshCount = mats.Count;
        for (int i = 0; i < mesh.subMeshCount; i++) {
            mesh.SetTriangles(triLists[i], i);
        }

        mesh.RecalculateBounds();

        obj.GetComponent<MeshFilter>().sharedMesh = mesh;

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
        List<List<IntPoint>> paths = layerPaths[layerIndex][submeshIndex];
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
        int x = Mathf.FloorToInt(pos.x / offset.x);
        int y = Mathf.FloorToInt(pos.y / offset.y);
        return SetTile(tileID, layerIndex, x, y, updateMesh);
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

        int index = x + y * tmxFile.width;
        int submeshIndex = index / 16250;
        
        if (updateMesh) UpdateMesh(layerIndex, submeshIndex);
        return true;
    }
}
}