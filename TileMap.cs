using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tiled {
public class TileMap : MonoBehaviour {

	public string tmxFilePath;
	public TMXFile tmxFile;

    [HideInInspector] [SerializeField] private MeshFilter[][] layerMeshFilters;
    [HideInInspector] [SerializeField] private Vector2 offset;

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

        layerMeshFilters = new MeshFilter[tmxFile.layers.Length][];
        for (int i = 0; i < tmxFile.layers.Length; i++) {
            CreateTileLayer(i);
        }
    }

    public void CreateTileLayer (int layerIndex) {
        Layer layerData = tmxFile.layers[layerIndex];
        GameObject layer = new GameObject(layerData.name);
        layer.transform.SetParent(transform);

        layerMeshFilters[layerIndex] = new MeshFilter[meshesPerLayer];
        for (int submeshIndex = 0; submeshIndex < meshesPerLayer; submeshIndex++) {
            GameObject meshObject = layer;
            if (meshesPerLayer > 1) {
                meshObject = new GameObject(layerData.name + "_submesh" + submeshIndex);
                meshObject.transform.SetParent(layer.transform);
            }

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            layerMeshFilters[layerIndex][submeshIndex] = meshFilter;
            UpdateMesh(layerIndex, submeshIndex);

            meshObject.AddComponent<MeshRenderer>();
        }
    }

    public void UpdateMesh(int layerIndex, int submeshIndex) {
        Layer layerData = tmxFile.layers[layerIndex];

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();
        List<int> tris = new List<int>();

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
    }

    public void SetTile (int tileID, int layerIndex, Vector3 pos) {
        int x = Mathf.FloorToInt(pos.x / offset.x);
        int y = Mathf.FloorToInt(pos.y / offset.y);
        SetTile(tileID, layerIndex, x, y);
    }

    public void SetTile (int tileID, int layerIndex, int x, int y) {
        tmxFile.layers[layerIndex].SetTileID(tileID, x, y);

        int index = x + y * tmxFile.width;
        int submeshIndex = index / 16250;
        
        UpdateMesh(layerIndex, submeshIndex);
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