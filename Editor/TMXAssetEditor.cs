using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using TiledSharp;

[InitializeOnLoad]
[CustomEditor(typeof(DefaultAsset))]
class TMXAssetEditor : Editor {

    public string path {
        get { return AssetDatabase.GetAssetPath(target); }
    }

    public bool isValid {
        get { return Path.GetExtension(path) == ".tmx"; }
    } 

    private TmxMap _tmxMap;
    public TmxMap tmxMap {
        get {
            if (_tmxMap == null) _tmxMap = new TmxMap(path);
            return _tmxMap;
        }
    }

    private static Material _mat;
    public static Material mat {
        get {
            if (_mat == null) _mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            return _mat;
        }
    }

    private Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    private Texture2D GetTileSetTexture (TmxTileset tileSet) {
        if (tileSetTextures.ContainsKey(tileSet.Image.Source)) {
            return tileSetTextures[tileSet.Image.Source];
        }
        else {
            string texturePath = Path.GetFullPath(tileSet.Image.Source);
            texturePath = texturePath.Replace(Application.dataPath, "Assets");
            Texture2D tex = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;
            tileSetTextures[tileSet.Image.Source] = tex;
            return tex;
        }
    }

	public override void OnInspectorGUI() {
        if (isValid) TMXInspectorGUI();
        else base.OnInspectorGUI();
    }
 
    private void TMXInspectorGUI () {  
        EditorGUILayout.LabelField("Version: " + tmxMap.Version);
        EditorGUILayout.LabelField("Width: " + tmxMap.Width);
        EditorGUILayout.LabelField("Height: " + tmxMap.Height);
        EditorGUILayout.LabelField("TileWidth: " + tmxMap.TileWidth);
        EditorGUILayout.LabelField("TileHeight: " + tmxMap.TileHeight);

        EditorGUILayout.LabelField("TileSets:");
        foreach (TmxTileset tileSet in tmxMap.Tilesets){
            EditorGUILayout.LabelField(tileSet.Name);
            Texture2D tex = GetTileSetTexture(tileSet);
            if (tex == null) continue; // TODO: Fix GetTileSetTexture missing textures
            Rect r = GUILayoutUtility.GetRect(tex.width, tex.height);
            EditorGUI.DrawPreviewTexture(r, tex, mat);
            foreach (int tileIndex in tileSet.Tiles.Keys) {
                TmxTilesetTile tile = tileSet.Tiles[tileIndex];

            }
        }
    }

    void OnEnable () {
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyGUICallback;
        SceneView.onSceneGUIDelegate += SceneGUICallback;
    }

    void OnDisable () {
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyGUICallback;
        SceneView.onSceneGUIDelegate += SceneGUICallback;
    }

    private static void HierarchyGUICallback(int pID, Rect pRect) {
        DragAndDropTMXFile();
    }

    private static void SceneGUICallback (SceneView sceneView) {
        DragAndDropTMXFile();
    }

    private static void DragAndDropTMXFile () {
        EventType eventType = Event.current.type;
        if (eventType == EventType.DragUpdated || eventType == EventType.DragPerform) {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (eventType == EventType.DragPerform) {
                bool foundTMX = false;
                foreach (Object o in DragAndDrop.objectReferences) {
                    string path = AssetDatabase.GetAssetPath(o);
                    if (Path.GetExtension(path) == ".tmx") {
                        CreateTileMap(path);
                        // GameObject map = CreateTileMap(path);
                        // place map at mouse position in scene view
                        // place at origin relative to object dropped on in Hierarchy
                        foundTMX = true;
                    }
                }
                if (foundTMX) {
                    DragAndDrop.AcceptDrag();
                    Event.current.Use();
                }
            }
        }
    }

    private static GameObject CreateTileMap (string tmxFilePath) {
        TmxMap tmxMap = new TmxMap(tmxFilePath);

        bool right = (tmxMap.RenderOrder == RenderOrderType.RightDown || tmxMap.RenderOrder == RenderOrderType.RightUp);
        bool up = (tmxMap.RenderOrder == RenderOrderType.RightUp || tmxMap.RenderOrder == RenderOrderType.LeftUp);
        Vector2 offset = new Vector2(
            right ? tmxMap.TileWidth : -tmxMap.TileWidth,
            up ? tmxMap.TileHeight : -tmxMap.TileHeight
        );

        string name = Path.GetFileNameWithoutExtension(tmxFilePath);
        GameObject root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo (root, "Created '" + name + "' from TMX file.");

        for (int i = 0; i < tmxMap.Layers.Count; i++) {
            GameObject layer = CreateTileLayer(tmxMap, i, offset);
            if (layer != null) layer.transform.SetParent(root.transform);
        }

        return root;
    }

    public static GameObject CreateTileLayer (TmxMap tmxMap, int layerIndex, Vector2 offset) {
        TmxLayer layerData = tmxMap.Layers[layerIndex];
        GameObject layer = new GameObject(layerData.Name);
        
        TmxTileset tileSet = tmxMap.Tilesets[0];
        int columns = tileSet.Columns == null ? 1 : tileSet.Columns.Value;
        int tileCount = tileSet.TileCount == null ? 1 : tileSet.TileCount.Value;
        int textureWidth = tileSet.TileWidth * columns + tileSet.Spacing * (columns - 1) + 2 * tileSet.Margin;
        int tileSetRows = tileCount / columns;
        int textureHeight = tileSet.TileHeight * tileSetRows + tileSet.Spacing * (tileSetRows - 1) + 2 * tileSet.Margin;

        int submeshCount = 1 + layerData.Tiles.Count * 4 / 65000;
        for (int submesh = 0; submesh < submeshCount; submesh++) {
            GameObject meshObject = layer;
            if (submeshCount > 1) {
                meshObject = new GameObject(layerData.Name + "_submesh" + submesh);
                meshObject.transform.SetParent(layer.transform);
            }

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            int tilesPlaced = 0;
            for (int tile = submesh * 16250; tile < Mathf.Min((submesh+1) * 16250, layerData.Tiles.Count); tile++) {        
                TmxLayerTile tileData = layerData.Tiles[tile];
                int tileIndex = tileData.Gid - tileSet.FirstGid;
                if (tileIndex < 0) continue;

                Vector3 pos = new Vector3(tileData.X * offset.x, tileData.Y * offset.y, 0);
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

                int i = tileIndex % columns;
                int j = (tileSetRows - 1) - tileIndex / columns;
                float left = tileSet.Margin + i * (tileSet.TileWidth + tileSet.Spacing);
                float right = left + tileSet.TileWidth;
                float bottom = tileSet.Margin + j * (tileSet.TileHeight + tileSet.Spacing);
                float top = bottom + tileSet.TileHeight;

                left /= (float)textureWidth;
                right /= (float)textureWidth;
                bottom /= (float)textureHeight;
                top /= (float)textureHeight;

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
            mesh.name = layerData.Name;
            if (submeshCount > 1) mesh.name += "_submesh" + submesh;
            mesh.vertices = verts.ToArray();
            mesh.normals = norms.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = mat;
            //set material property block?
        }

        return layer;
    }
}