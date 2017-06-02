using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ClipperLib;

namespace Tiled {
public class TileMap : MonoBehaviour {

	public string tmxFilePath;
	public TMXFile tmxFile;

    [HideInInspector] public Vector2 offset;
    [HideInInspector] public GameObject[] layers;
    private MeshFilter[][] _layerMeshFilters;
    private MeshFilter[][] layerMeshFilters {
        get {
            if (_layerMeshFilters == null) {
                _layerMeshFilters = new MeshFilter[layers.Length][];
                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++) {
                    _layerMeshFilters[layerIndex] = layers[layerIndex].GetComponentsInChildren<MeshFilter>();
                }
            }
            return _layerMeshFilters;
        }
    }

    private PolyTree[][] _layerPolyTrees;
    private PolyTree[][] layerPolyTrees {
        get {
            if (_layerPolyTrees == null) {
                _layerPolyTrees = new PolyTree[layers.Length][];
                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++) {
                    _layerPolyTrees[layerIndex] = new PolyTree[meshesPerLayer];
                }
            }
            return _layerPolyTrees;
        }
    }

    private int meshesPerLayer {
        get { return 1 + tmxFile.width * tmxFile.height / 16250; }
    }

	public void Setup (string tmxFilePath, float pixelsPerUnit = -1) {
		this.tmxFilePath = tmxFilePath;
        this.tmxFile = TMXFile.Load(tmxFilePath);

        if (pixelsPerUnit < 0) pixelsPerUnit = tmxFile.tileWidth;

        string[] xyDirections = tmxFile.renderOrder.Split('-');
        offset = new Vector2(
            (xyDirections[0] == "right") ? tmxFile.tileWidth : -tmxFile.tileWidth,
            (xyDirections[1] == "up") ? tmxFile.tileHeight : -tmxFile.tileHeight            
        );
        offset *= 1f / pixelsPerUnit;

        layers = new GameObject[tmxFile.layers.Length];
        _layerMeshFilters = new MeshFilter[tmxFile.layers.Length][];
        for (int i = 0; i < tmxFile.layers.Length; i++) {
            CreateTileLayer(i);
        }
    }

    public void CreateTileLayer (int layerIndex) {
        Layer layerData = tmxFile.layers[layerIndex];
        GameObject layer = new GameObject(layerData.name);
        layer.AddComponent<PolygonCollider2D>();
        layer.transform.SetParent(transform);
        layers[layerIndex] = layer;

        _layerMeshFilters[layerIndex] = new MeshFilter[meshesPerLayer];
        for (int submeshIndex = 0; submeshIndex < meshesPerLayer; submeshIndex++) {
            GameObject meshObject = layer;
            if (meshesPerLayer > 1) {
                meshObject = new GameObject(layerData.name + "_submesh" + submeshIndex);
                meshObject.transform.SetParent(layer.transform);
            }

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            _layerMeshFilters[layerIndex][submeshIndex] = meshFilter;
            UpdateMesh(layerIndex, submeshIndex);

            meshObject.AddComponent<MeshRenderer>();
        }

        UpdatePolygonColliders(layerIndex);
    }

    public void Revert () {
        for (int layerIndex = 0; layerIndex < tmxFile.layers.Length; layerIndex++) {
            for (int submeshIndex = 0; submeshIndex < meshesPerLayer; submeshIndex++) {
                UpdateMesh(layerIndex, submeshIndex);
            }
        }
    }

    private IntPoint Vector2ToIntPoint (Vector2 p) {
        int x = (int)(p.x * 100);
        int y = (int)(p.y * 100);
        return new IntPoint(x, y);
    }

    private Vector2 IntPointToVector2 (IntPoint p) {
        float x = (float)p.X / 100f;
        float y = (float)p.Y / 100f;
        return new Vector2(x, y);
    }

    public void UpdateMesh(int layerIndex, int submeshIndex) {
        Layer layerData = tmxFile.layers[layerIndex];

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();
        List<int> tris = new List<int>();

        Clipper clipper = new Clipper();

        int tilesPlaced = 0;
        for (int tileIndex = submeshIndex * 16250; tileIndex < Mathf.Min((submeshIndex+1) * 16250, layerData.tileIDs.Length); tileIndex++) {        

            int tileID = layerData.tileIDs[tileIndex];
            TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
            if (tileSet == null) continue;
            
            TileRect uvRect = tileSet.GetTileUVs(tileID);
            if (uvRect == null) continue;

            int[] tileLocation = layerData.GetTileLocation(tileIndex);
            Vector3 pos = new Vector3(tileLocation[0] * offset.x, tileLocation[1] * offset.y, 0);
            Vector3[] quad = new Vector3[] {
                pos,
                pos + Vector3.up * offset.y,
                pos + (Vector3)offset,
                pos + Vector3.right * offset.x
            };
            verts.AddRange(quad);

            List<IntPoint> path = new List<IntPoint>(System.Array.ConvertAll(quad, (p) => Vector2ToIntPoint((Vector2)p)));
            clipper.AddPath(path, PolyType.ptSubject, true);

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

            if (offset.x < 0) {
                uvArray = new Vector2[] {
                    new Vector2(right, bottom),
                    new Vector2(right, top),
                    new Vector2(left, top),
                    new Vector2(left, bottom)
                };
            }

            if (offset.y < 0) {
                uvArray = new Vector2[]{uvArray[1], uvArray[0], uvArray[3], uvArray[2]};
            }

            uvs.AddRange(uvArray);

            colors.AddRange(new Color[] {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            });

            tris.AddRange(new int[] {
                tilesPlaced * 4, tilesPlaced * 4 + 1, tilesPlaced * 4 + 2,
                tilesPlaced * 4, tilesPlaced * 4 + 2, tilesPlaced * 4 + 3
            });
            tilesPlaced++;
        }

        Mesh mesh = new Mesh();
        mesh.name = layerData.name;
        if (meshesPerLayer > 1) mesh.name += "_submesh" + submeshIndex;
        mesh.vertices = verts.ToArray();
        mesh.normals = norms.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();

        layerMeshFilters[layerIndex][submeshIndex].sharedMesh = mesh;

        PolyTree tree = new PolyTree();
        clipper.Execute(ClipType.ctUnion, tree);
        layerPolyTrees[layerIndex][submeshIndex] = tree;
    }

    void UpdatePolygonColliders (int layerIndex) {
        Clipper clipper = new Clipper();
        for (int submeshIndex = 0; submeshIndex < layers.Length; submeshIndex++) {
            PolyTree branch = layerPolyTrees[layerIndex][submeshIndex];
            clipper.AddPaths(Clipper.ClosedPathsFromPolyTree(branch), PolyType.ptSubject, true);
        }
        
        PolyTree tree = new PolyTree();
        clipper.Execute(ClipType.ctUnion, tree);
        List<List<IntPoint>> paths = Clipper.ClosedPathsFromPolyTree(tree);

        PolygonCollider2D poly = layers[layerIndex].GetComponent<PolygonCollider2D>();
        poly.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++) {
            Vector2[] path = System.Array.ConvertAll(paths[i].ToArray(), (p) => IntPointToVector2(p));
            poly.SetPath(i, path);
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

    public void OnDrawGizmos () {
        if (tmxFile == null) return;

        Gizmos.color = new Color(1,1,1,0.1f);
        for (int x = 1; x < tmxFile.width; x++) {
            Gizmos.DrawLine(
                new Vector3(x * offset.x, 0, 0),
                new Vector3(x * offset.x, tmxFile.height * offset.y, 0)
            );
        }

        for (int y = 1; y < tmxFile.height; y++) {
             Gizmos.DrawLine(
                new Vector3(0, y * offset.y, 0),
                new Vector3(tmxFile.width * offset.x, y * offset.y, 0)
            );   
        }
    }
}
}