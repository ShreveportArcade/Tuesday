using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using ClipperLib;

[InitializeOnLoad]
[CustomEditor(typeof(DefaultAsset))]
class TMXAssetEditor : Editor {

    public string path {
        get { return AssetDatabase.GetAssetPath(target); }
    }

    public bool isValid {
        get { return Path.GetExtension(path) == ".tmx"; }
    } 

    private XmlDocument _document;
    public XmlDocument document {
        get {
            if (_document == null) {
                _document = new XmlDocument();
                _document.Load(path);
            }
            return _document;
        }
    }

	public override void OnInspectorGUI() {
        if (isValid) TMXInspectorGUI();
        else base.OnInspectorGUI();
    }
 
    private void TMXInspectorGUI () {  
        XmlNode map = document.SelectSingleNode("map");

        // separators, drop downs 
        EditorGUILayout.LabelField("Map Properties:");
        foreach (XmlAttribute attribute in map.Attributes) {
            EditorGUILayout.LabelField(attribute.Name + ": " + attribute.Value);
        }

        EditorGUILayout.LabelField("Tiles:");
    }

    void OnEnable () {
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyGUICallback;
        SceneView.onSceneGUIDelegate += SceneGUICallback;
    }

    void OnDisable () {
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyGUICallback;
        SceneView.onSceneGUIDelegate += SceneGUICallback;
    }

    static void HierarchyGUICallback(int pID, Rect pRect) {
        DragAndDropTMXFile();
    }

    public void SceneGUICallback (SceneView sceneView) {
        DragAndDropTMXFile();
    }

    private static void DragAndDropTMXFile () {
        EventType eventType = Event.current.type;
        if (eventType == EventType.DragUpdated || eventType == EventType.DragPerform) {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (eventType == EventType.DragPerform)
                foreach (Object o in DragAndDrop.objectReferences) {
                    string path = AssetDatabase.GetAssetPath(o);
                    if (Path.GetExtension(path) == ".tmx") {
                        CreateTileMap(path);
                    }
                }
                DragAndDrop.AcceptDrag();

            Event.current.Use();
        }
    }

    public static GameObject CreateTileMap (string tileMapPath) {
        XmlDocument document = new XmlDocument();
        document.Load(tileMapPath);

        XmlNode map = document.SelectSingleNode("map");

        int tileWidth = 0;
        int tileHeight = 0;
        int.TryParse(map.Attributes["tilewidth"].Value, out tileWidth);
        int.TryParse(map.Attributes["tileheight"].Value, out tileHeight);

        string renderOrder = map.Attributes["renderorder"].Value;
        string[] xyDirections = renderOrder.Split('-');
        Vector2 offset = new Vector2(
            (xyDirections[0] == "right") ? tileWidth : -tileWidth,
            (xyDirections[1] == "up") ? tileHeight : -tileHeight
            );

        string name = Path.GetFileNameWithoutExtension(tileMapPath);
        GameObject root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo (root, "Created '" + name + "' from TMX file.");

        XmlNode tileSetData = map.SelectSingleNode("tileset");
        foreach (XmlNode layerData in map.SelectNodes("layer")) {
            GameObject layer = CreateTileLayer(layerData, tileSetData, offset);
            layer.transform.SetParent(root.transform);
        }

        return root;
    }

    public static GameObject CreateTileLayer (XmlNode layerData, XmlNode tileSetData, Vector2 offset) {
        string layerName = layerData.Attributes["name"].Value;

        GameObject layer = new GameObject(layerName);
        MeshFilter meshFilter = layer.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = layer.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        Mesh mesh = new Mesh();
        mesh.name = layerName;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color> colors = new List<Color>();
        List<int> tris = new List<int>();

        int width = 0;
        int height = 0;
        int.TryParse(layerData.Attributes["width"].Value, out width);
        int.TryParse(layerData.Attributes["height"].Value, out height);

        int tileWidth = 0;
        int tileHeight = 0;
        int spacing = 0;
        int margin = 0;
        int tileCount = 0;
        int tileSetColumns = 0;
        int.TryParse(tileSetData.Attributes["tilewidth"].Value, out tileWidth);
        int.TryParse(tileSetData.Attributes["tileheight"].Value, out tileHeight);
        int.TryParse(tileSetData.Attributes["spacing"].Value, out spacing);
        int.TryParse(tileSetData.Attributes["margin"].Value, out margin);
        int.TryParse(tileSetData.Attributes["tilecount"].Value, out tileCount);
        int.TryParse(tileSetData.Attributes["columns"].Value, out tileSetColumns);
        int textureWidth = tileWidth * tileSetColumns + spacing * (tileSetColumns - 1) + 2 * margin;
        int tileSetRows = tileCount / tileSetColumns;
        int textureHeight = tileHeight * tileSetRows + spacing * (tileSetRows - 1) + 2 * margin;

        int tilesPlaced = 0;
        string[] rows = layerData.InnerText.Trim().Split('\n');
        for (int y = 0; y < rows.Length; y++) {
            string[] tiles = rows[y].Trim(new char[] {',','\n',' '}).Split(',');
            for (int x = 0; x < tiles.Length; x++) {
                int tile = 0;
                int.TryParse(tiles[x], out tile);
                tile -= 1;

                if (tile >= 0) {
                    Vector3 pos = new Vector3(x * offset.x, y * offset.y, 0);
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

                    int i = tile % tileSetColumns;
                    int j = (tileSetRows - 1) - tile / tileSetColumns;
                    float left = margin + i * (tileWidth + spacing);
                    float right = left + tileWidth;
                    float bottom = margin + j * (tileHeight + spacing);
                    float top = bottom + tileHeight;

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
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.normals = norms.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors = colors.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        return layer;
    }
}