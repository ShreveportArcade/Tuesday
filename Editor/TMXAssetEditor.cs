using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Tiled;

[InitializeOnLoad]
[CustomEditor(typeof(DefaultAsset))]
class TMXAssetEditor : Editor {

    private static bool showTileSets = true;

    public string path {
        get { return AssetDatabase.GetAssetPath(target); }
    }

    public bool isValid {
        get { return Path.GetExtension(path) == ".tmx"; }
    } 

    private static Dictionary<string, bool> tileMapChanges = new Dictionary<string, bool>();
    private static Dictionary<string, TileMap> tileMaps = new Dictionary<string, TileMap>();
    public TileMap tileMap {
        get {
            if (!tileMaps.ContainsKey(path)) {
                tileMaps[path] = TileMap.LoadTMX(path);
                tileMapChanges[path] = false;
            } 
            return tileMaps[path];
        }
    }

    private static Material _mat;
    public static Material mat {
        get {
            if (_mat == null) _mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            return _mat;
        }
    }

    private static Dictionary<int, bool> tileSetFoldoutStates = new Dictionary<int, bool>();
    private static Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    private Texture2D GetTileSetTexture (TileSet tileSet) {
        Texture2D tex = null;
        if (tileSetTextures.ContainsKey(tileSet.image.source)) {
            tex = tileSetTextures[tileSet.image.source];
        }
        else {
            string texturePath = tileSet.image.source;
            if (texturePath.StartsWith("..")) {
                texturePath = Path.Combine(Path.GetDirectoryName(path), texturePath);
                texturePath = Path.GetFullPath(texturePath);
                texturePath = texturePath.Replace(Application.dataPath, "Assets");
            }
            tex = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;
            tileSetTextures[tileSet.image.source] = tex;
        }

        return tex;
    }

    private void TileSetField (TileSet tileSet) {
        EditorGUILayout.BeginHorizontal();

        int id = tileSet.firstGID;
        bool show = !tileSetFoldoutStates.ContainsKey(id) || tileSetFoldoutStates[id];
        tileSetFoldoutStates[id] = EditorGUILayout.Foldout(show, "");

        if (EditorGUILayout.DropdownButton(new GUIContent(""), FocusType.Passive)) {
            GenericMenu menu = new GenericMenu();
            int index = tileMap.GetIndexOfTileSet(tileSet);
            if (index > 0) {
                menu.AddItem(new GUIContent("Move Up"), false, () => {

                });
            }
            if (index < (tileMap.tileSets.Length - 1)) {
                menu.AddItem(new GUIContent("Move Down"), false, () => {

                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => {

            });
            menu.ShowAsContext();
            Event.current.Use();
        }

        tileSet.name = EditorGUILayout.TextField("", tileSet.name);

        EditorGUILayout.EndHorizontal();   

        if (tileSetFoldoutStates[tileSet.firstGID]) {
            tileSet.firstGID = EditorGUILayout.IntField("First Tile ID", tileSet.firstGID);
            tileSet.tileWidth = EditorGUILayout.IntField("Width", tileSet.tileWidth);
            tileSet.tileHeight = EditorGUILayout.IntField("Height", tileSet.tileHeight);
            tileSet.spacing = EditorGUILayout.IntField("Spacing", tileSet.spacing);
            tileSet.margin = EditorGUILayout.IntField("Margin", tileSet.margin);
            tileSet.rows = EditorGUILayout.IntField("Rows", tileSet.rows);
            tileSet.columns = EditorGUILayout.IntField("Columns", tileSet.columns);

            Texture2D currentTexture = GetTileSetTexture(tileSet);
            Texture2D tex = EditorGUILayout.ObjectField(currentTexture, typeof(Texture2D), false) as Texture2D;
            if (currentTexture != tex) {
                Uri textureURI = new Uri("/" + AssetDatabase.GetAssetPath(tex));
                Uri tmxFileURI = new Uri("/" + Path.GetDirectoryName(path));
                tileSet.image.source = "../" + tmxFileURI.MakeRelativeUri(textureURI);
            }

            if (tex != null) {
                Rect r = GUILayoutUtility.GetRect(tex.width, tex.height);
                r.width = r.height * (float)tex.width / (float)tex.height;
                r.width -= 20;
                EditorGUI.DrawPreviewTexture(r, tex, mat);

                if (Event.current != null && 
                    Event.current.type == EventType.MouseDown && 
                    r.Contains(Event.current.mousePosition)) {
                    selectedTileSet = tileSet;
                    selectedTileIndex = GetTileIndex(tileSet, r, Event.current.mousePosition);
                }
            }
        }
    }

    private TileSet selectedTileSet;
    private int selectedTileIndex;
    private int GetTileIndex (TileSet tileSet, Rect rect, Vector2 pos) {
        pos -= rect.min;
        pos.x /= rect.width;
        pos.y /= rect.height;
        int i = tileSet.firstGID;
        i += Mathf.FloorToInt(pos.y * tileSet.rows) * tileSet.columns + Mathf.FloorToInt(pos.x * tileSet.columns);
        return i;
    }

	public override void OnInspectorGUI() {
        if (!isValid) {
            base.OnInspectorGUI();
            return;
        }

        GUI.enabled = true;

        EditorGUILayout.LabelField("Version: " + tileMap.version);
        EditorGUILayout.LabelField("Width: " + tileMap.width);
        EditorGUILayout.LabelField("Height: " + tileMap.height);
        EditorGUILayout.LabelField("TileWidth: " + tileMap.tileWidth);
        EditorGUILayout.LabelField("TileHeight: " + tileMap.tileHeight);

        EditorGUIUtility.hierarchyMode = true;
        showTileSets = EditorGUILayout.Foldout(showTileSets, "Tile Sets:");
        EditorGUIUtility.hierarchyMode = false;
        if (showTileSets) {
            foreach (TileSet tileSet in tileMap.tileSets){
                TileSetField(tileSet); 
            }
        }

        if (GUI.changed) {
            tileMapChanges[path] = true;
        }

        if (tileMapChanges[path]) {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Revert")) {
                tileMaps[path] = TileMap.LoadTMX(path);
                tileMapChanges[path] = false;
            }
            if (GUILayout.Button("Save")) {
                tileMaps[path].SaveTMX(
                    EditorUtility.SaveFilePanel(
                        "Save as TMX",
                        Path.GetDirectoryName(path),
                        Path.GetFileNameWithoutExtension(path),
                        Path.GetExtension(path).TrimStart(new char[]{'.'})
                    )
                );
                AssetDatabase.ImportAsset(path);
                tileMapChanges[path] = false;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    public override bool HasPreviewGUI() {
        return selectedTileSet != null;
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background) {
        if (!isValid) {
            base.OnPreviewGUI(r, background);
            return;
        }

        if (Event.current.type != EventType.Repaint) return;

        Texture2D tex = GetTileSetTexture(tileMap.tileSets[0]);
        float[] uvArray = selectedTileSet.GetTileUVs(selectedTileIndex);
        Rect uvRect = new Rect(uvArray[0], uvArray[1], uvArray[2], uvArray[3]);
        GUI.DrawTextureWithTexCoords(r, tex, uvRect, true);
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
                foreach (UnityEngine.Object o in DragAndDrop.objectReferences) {
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
        TileMap tileMap = TileMap.LoadTMX(tmxFilePath);

        string[] xyDirections = tileMap.renderOrder.Split('-');
        Vector2 offset = new Vector2(
            (xyDirections[0] == "right") ? tileMap.tileWidth : -tileMap.tileWidth,
            (xyDirections[1] == "up") ? tileMap.tileHeight : -tileMap.tileHeight            
        );

        string name = Path.GetFileNameWithoutExtension(tmxFilePath);
        GameObject root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo (root, "Created '" + name + "' from TMX file.");

        for (int i = 0; i < tileMap.layers.Length; i++) {
            GameObject layer = CreateTileLayer(tileMap, i, offset);
            if (layer != null) layer.transform.SetParent(root.transform);
        }

        return root;
    }

    public static GameObject CreateTileLayer (TileMap tileMap, int layerIndex, Vector2 offset) {
        Layer layerData = tileMap.layers[layerIndex];
        GameObject layer = new GameObject(layerData.name);
        
        int submeshCount = 1 + layerData.tileIDs.Length * 4 / 65000;
        for (int submesh = 0; submesh < submeshCount; submesh++) {
            GameObject meshObject = layer;
            if (submeshCount > 1) {
                meshObject = new GameObject(layerData.name + "_submesh" + submesh);
                meshObject.transform.SetParent(layer.transform);
            }

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<int> tris = new List<int>();

            int tilesPlaced = 0;
            for (int tileIndex = submesh * 16250; tileIndex < Mathf.Min((submesh+1) * 16250, layerData.tileIDs.Length); tileIndex++) {        
                int tileID = layerData.tileIDs[tileIndex];
                TileSet tileSet = tileMap.GetTileSetByTileID(tileID);
                if (tileSet == null) continue;
                
                float[] uvRect = tileSet.GetTileUVs(tileID);
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

                float left = uvRect[0];
                float right = uvRect[0] + uvRect[2];
                float bottom = uvRect[1];
                float top = uvRect[1] + uvRect[3];

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