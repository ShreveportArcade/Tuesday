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
    [HideInInspector] public Material[] tileSetMaterials;

    private GameObject[][] _layerSubmeshObjects;
    private GameObject[][] layerSubmeshObjects {
        get {
            if (_layerSubmeshObjects == null) {
                _layerSubmeshObjects = new GameObject[layers.Length][];
                for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++) {
                    _layerSubmeshObjects[layerIndex] = layers[layerIndex].GetComponentsInChildren<GameObject>();
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

        if (pixelsPerUnit < 0) pixelsPerUnit = tmxFile.tileWidth;

        string[] xyDirections = tmxFile.renderOrder.Split('-');
        offset = new Vector2(
            (xyDirections[0] == "right") ? tmxFile.tileWidth : -tmxFile.tileWidth,
            (xyDirections[1] == "up") ? tmxFile.tileHeight : -tmxFile.tileHeight            
        );
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

    public void Revert () {
        for (int layerIndex = 0; layerIndex < tmxFile.layers.Length; layerIndex++) {
            for (int submeshIndex = 0; submeshIndex < meshesPerLayer; submeshIndex++) {
                UpdateMesh(layerIndex, submeshIndex);
            }
            UpdatePolygonColliders(layerIndex);
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
        
        List<int>[] matTris = new List<int>[tmxFile.tileSets.Length];
        Dictionary<int, int> firstGIDToIndex = new Dictionary<int, int>();
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            matTris[i] = new List<int>();
            firstGIDToIndex[tmxFile.tileSets[i].firstGID] = i;
        }

        List<List<IntPoint>> paths = new List<List<IntPoint>>();
        Dictionary<int, Vector3[]> idToPhysics = new Dictionary<int, Vector3[]>();
        foreach (TileSet tileSet in tmxFile.tileSets) {
            if (tileSet.tiles == null) {
                Debug.Log("Missing tiles for: " + tileSet.name);
                continue;
            }
            foreach (Tile tile in tileSet.tiles) {
                if (tile.objectGroup != null && tile.objectGroup.objects != null && tile.objectGroup.objects.Length > 0) {
                    TileObject tileObject = tile.objectGroup.objects[0];
                    float x = offset.x * (float)tileObject.x / (float)tileSet.tileWidth;
                    float y = offset.y * (float)tileObject.y / (float)tileSet.tileHeight;
                    float width = offset.x * (float)tileObject.width / (float)tileSet.tileWidth;
                    float height = offset.y * (float)tileObject.height / (float)tileSet.tileHeight;
                    idToPhysics[tile.id + tileSet.firstGID] = new Vector3[] {
                        new Vector3(x, y, 0),
                        new Vector3(x, y + height, 0),
                        new Vector3(x + width, y + height, 0),
                        new Vector3(x + width, y, 0)
                    };
                }
            }
        }

        int tilesPlaced = 0;
        for (int tileIndex = submeshIndex * 16250; tileIndex < Mathf.Min((submeshIndex+1) * 16250, layerData.tileIDs.Length); tileIndex++) {        

            int tileID = layerData.tileIDs[tileIndex];
            TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
            if (tileSet == null) continue;
            
            TileRect uvRect = tileSet.GetTileUVs(tileID);
            if (uvRect == null) continue;

            int[] tileLocation = layerData.GetTileLocation(tileIndex);
            Vector3 pos = new Vector3(tileLocation[0] * offset.x, tileLocation[1] * offset.y, 0);
            verts.AddRange(new Vector3[] {
                pos,
                pos + Vector3.up * offset.y,
                pos + (Vector3)offset,
                pos + Vector3.right * offset.x
            });

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

        paths = Clipper.SimplifyPolygons(paths);
        paths = RemoveColinnear(paths);

        layerPaths[layerIndex][submeshIndex] = paths;
    }

    List<List<IntPoint>> RemoveColinnear (List<List<IntPoint>> paths) {
        for (int i = 0; i < paths.Count; i++) {
            List<IntPoint> path = paths[i];
            List<IntPoint> newPath = new List<IntPoint>(path);
            float ang = 0;
            for (int j = 1; j < path.Count-1; j++) {
                Vector2 a = new Vector2(path[j].X - path[j-1].X, path[j].Y - path[j-1].Y);
                Vector2 b = new Vector2(path[j+1].X - path[j].X, path[j+1].Y - path[j].Y);
                ang = Vector2.Angle(a, b);
                if (Mathf.Abs(ang) < 5) {
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