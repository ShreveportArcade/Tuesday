using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Tiled {
[CustomEditor(typeof(TileMap))]
public class TileMapEditor : Editor {

    private static bool showTileSets = true;

    private TileMap tileMap {
    	get { return (target as TileMap); }
    }

    private TMXFile tmxFile {
    	get { return tileMap.tmxFile; }
    	set { tileMap.tmxFile = value; }
    }

    private string path {
    	get { return tileMap.tmxFilePath; }
    	set { tileMap.tmxFilePath = value; }
    }

    private static Material _mat;
    private static Material mat {
        get {
            if (_mat == null) _mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            return _mat;
        }
    }    

	private static Dictionary<int, bool> tileSetFoldoutStates = new Dictionary<int, bool>();
    private static Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    public static Texture2D GetTileSetTexture (TileSet tileSet, string path) {
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

    private void TileSetField (TileSet tileSet) {
        int id = tileSet.firstGID;
        Rect r = GUILayoutUtility.GetRect(Screen.width, EditorGUIUtility.singleLineHeight);
        float w = r.width;
        r.width = 20;
        bool show = !tileSetFoldoutStates.ContainsKey(id) || tileSetFoldoutStates[id];
        tileSetFoldoutStates[id] = EditorGUI.Foldout(r, show, "");

        r.x += 20;
        r.width = w - 40;
        tileSet.name = EditorGUI.TextField(r, "", tileSet.name);

        r.x += r.width;
        r.width = 20;
        if (EditorGUI.DropdownButton(r, new GUIContent(""), FocusType.Passive)) {
            GenericMenu menu = new GenericMenu();
            int index = tmxFile.GetIndexOfTileSet(tileSet);
            if (index > 0) {
                menu.AddItem(new GUIContent("Move Up"), false, () => {

                });
            }
            if (index < (tmxFile.tileSets.Length - 1)) {
                menu.AddItem(new GUIContent("Move Down"), false, () => {

                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => {
                TileSet[] newTileSets = new TileSet[tmxFile.tileSets.Length - 1];
                for (int i = 0; i < newTileSets.Length; i++) {
                    if (i < index) newTileSets[i] = tmxFile.tileSets[i];
                    else if (i > index) newTileSets[i] = tmxFile.tileSets[i+1];
                }
                tmxFile.tileSets = newTileSets;
            });
            menu.ShowAsContext();
            Event.current.Use();
        }

        if (tileSetFoldoutStates[tileSet.firstGID]) {
            tileSet.firstGID = EditorGUILayout.IntField("First Tile ID", tileSet.firstGID);
            tileSet.tileWidth = EditorGUILayout.IntField("Width", tileSet.tileWidth);
            tileSet.tileHeight = EditorGUILayout.IntField("Height", tileSet.tileHeight);
            tileSet.spacing = EditorGUILayout.IntField("Spacing", tileSet.spacing);
            tileSet.margin = EditorGUILayout.IntField("Margin", tileSet.margin);
            tileSet.rows = EditorGUILayout.IntField("Rows", tileSet.rows);
            tileSet.columns = EditorGUILayout.IntField("Columns", tileSet.columns);

            Texture2D currentTexture = GetTileSetTexture(tileSet, path);
            Texture2D tex = EditorGUILayout.ObjectField(currentTexture, typeof(Texture2D), false) as Texture2D;
            if (currentTexture != tex) {
                Uri textureURI = new Uri("/" + AssetDatabase.GetAssetPath(tex));
                Uri tmxFileURI = new Uri("/" + Path.GetDirectoryName(path));
                tileSet.image.source = "../" + tmxFileURI.MakeRelativeUri(textureURI);
            }

            if (tex != null) {
                r = GUILayoutUtility.GetRect(Screen.width, tex.height);
                r.width = r.height * (float)tex.width / (float)tex.height - 20;
                r.height = r.width * (float)tex.height / (float)tex.width;
                r.x = (Screen.width - r.width) * 0.5f;
                EditorGUI.DrawPreviewTexture(r, tex, mat);

                // FIXME: Mouse events throw exception here
                /*
                ArgumentException: GUILayout: Mismatched LayoutGroup.MouseDown
                UnityEngine.GUILayoutUtility.BeginLayoutGroup (UnityEngine.GUIStyle style, UnityEngine.GUILayoutOption[] options, System.Type layoutType) (at /Users/builduser/buildslave/unity/build/Runtime/IMGUI/Managed/GUILayoutUtility.cs:301)
                UnityEditor.EditorGUILayout.BeginHorizontal (UnityEngine.GUIContent content, UnityEngine.GUIStyle style, UnityEngine.GUILayoutOption[] options) (at /Users/builduser/buildslave/unity/build/Editor/Mono/EditorGUI.cs:7234)
                UnityEditor.EditorGUILayout.BeginHorizontal (UnityEngine.GUILayoutOption[] options) (at /Users/builduser/buildslave/unity/build/Editor/Mono/EditorGUI.cs:7214)
                UnityEditor.LabelGUI.OnLabelGUI (UnityEngine.Object[] assets) (at /Users/builduser/buildslave/unity/build/Editor/Mono/Inspector/LabelGUI.cs:171)
                UnityEditor.InspectorWindow.DrawPreviewAndLabels () (at /Users/builduser/buildslave/unity/build/Editor/Mono/Inspector/InspectorWin
                */
                if (Event.current != null && 
                    Event.current.type == EventType.MouseDown && 
                    r.Contains(Event.current.mousePosition)) {
                    selectedTileSet = tileSet;
                    selectedTileIndex = GetTileIndex(tileSet, r, Event.current.mousePosition);
                }

                if (selectedTileSet != null && selectedTileSet == tileSet) {
                    TileRect uvTileRect = selectedTileSet.GetTileUVs(selectedTileIndex);
                    float left = r.x + r.width * uvTileRect.left;
                    float right = r.x + r.width * uvTileRect.right;
                    float bottom = r.y + r.height * (1 - uvTileRect.bottom);
                    float top = r.y + r.height * (1 - uvTileRect.top);

                    Vector3 center = new Vector3(
                        (left + right) * 0.5f,
                        (bottom + top) * 0.5f,
                        0);
                    Vector3 size = new Vector3(
                        (right - left),
                        (top - bottom),
                        0);
                    Handles.DrawWireCube(center, size);
                }
            }
            EditorGUILayout.Space();
        }
    }

	public override void OnInspectorGUI() {
		
		base.OnInspectorGUI();

    	EditorGUIUtility.hierarchyMode = true;
        showTileSets = EditorGUILayout.Foldout(showTileSets, "Tile Sets:");
        EditorGUIUtility.hierarchyMode = false;
        if (showTileSets) {
            foreach (TileSet tileSet in tmxFile.tileSets){
                TileSetField(tileSet); 
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Revert")) {
            tmxFile = TMXFile.Load(path);
        }
        if (GUILayout.Button("Save")) {
            tmxFile.Save(path);
            AssetDatabase.ImportAsset(path);
        }
        if (GUILayout.Button("Save As")) {
            tmxFile.Save(
                EditorUtility.SaveFilePanel(
                    "Save as TMX",
                    Path.GetDirectoryName(path),
                    Path.GetFileNameWithoutExtension(path),
                    Path.GetExtension(path).TrimStart(new char[]{'.'})
                )
            );
            AssetDatabase.ImportAsset(path);
        }
        EditorGUILayout.EndHorizontal();
	}

    public override bool HasPreviewGUI() {
        return selectedTileSet != null;
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background) {
        if (Event.current.type != EventType.Repaint) return;

        Texture2D tex = GetTileSetTexture(tmxFile.tileSets[0], path);
        TileRect uvTileRect = selectedTileSet.GetTileUVs(selectedTileIndex);
        Rect uvRect = new Rect(uvTileRect.x, uvTileRect.y, uvTileRect.width, uvTileRect.height);
        if (r.height > r.width) {
            r.height = r.width;
            r.x += (r.height - r.width) * 0.5f;
        }
        else if (r.width > r.height) {
            r.width = r.height;
            r.y += (r.width - r.height) * 0.5f;
        }
        r.x = (Screen.width - r.width) * 0.5f;
        GUI.DrawTextureWithTexCoords(r, tex, uvRect, true);
    }

    void OnSceneGUI () {
    	if (selectedTileSet == null) return;

    	Camera cam = SceneView.lastActiveSceneView.camera;
    	if (cam != null && Event.current != null && Event.current.type == EventType.MouseDown) {
	        Ray ray = cam.ScreenPointToRay(Event.current.mousePosition);
	        float dist = 0;
	        Plane plane = new Plane(Vector3.back, tileMap.transform.position);
	        if (plane.Raycast(ray, out dist)) {
	        	Vector3 p = ray.GetPoint(dist);
	        	tileMap.SetTile(selectedTileIndex, 0, p);
	        }
	    }
    }
}
}