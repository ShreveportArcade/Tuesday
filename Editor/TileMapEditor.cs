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
        }

        if (tex == null) {
        	tex = EditorGUIUtility.FindTexture(Path.GetFileNameWithoutExtension(path));
        }

        if (tex != null) {
        	tileSetTextures[tileSet.image.source] = tex;
        }

        return tex;
    }

	private static TileSet selectedTileSet;
    private static int selectedTileIndex;
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

                if (Event.current.isMouse && 
                	Event.current.button == 0 && 
                	r.Contains(Event.current.mousePosition)) {
                    selectedTileSet = tileSet;
                    selectedTileIndex = GetTileIndex(tileSet, r, Event.current.mousePosition);
                }

                if (selectedTileSet != null && selectedTileSet == tileSet && selectedTileIndex > 0) {
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

    private static int editState = 0;
    public override void OnInspectorGUI() {		
		base.OnInspectorGUI();

    	EditorGUIUtility.hierarchyMode = true;
        showTileSets = EditorGUILayout.Foldout(showTileSets, "Tile Sets:");
        EditorGUIUtility.hierarchyMode = false;
        if (showTileSets) {
	    	editState = GUILayout.Toolbar(editState, new string[] {"Move", "Paint", "Erase", "Select"});
            foreach (TileSet tileSet in tmxFile.tileSets){
                TileSetField(tileSet); 
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Revert")) {
            tmxFile = TMXFile.Load(path);
			tileMap.Revert();
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

    public override GUIContent GetPreviewTitle() {
    	return new GUIContent(selectedTileSet.name + " - Tile: " + selectedTileIndex);
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background) {
        if (Event.current.type != EventType.Repaint) return;

        Texture2D tex = GetTileSetTexture(tmxFile.tileSets[0], path);
        TileRect uvTileRect = selectedTileSet.GetTileUVs(selectedTileIndex);
        if (uvTileRect == null) return;
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

    Vector3 selectionStart;
    Vector3 selectionEnd;
    int[] selectedTileIndices = null;
    void OnSceneGUI () {
    	if (editState == 0) return;

    	int controlId = GUIUtility.GetControlID(FocusType.Passive);

    	Event e = Event.current;
    	if (e == null) return;

    	if (e.type == EventType.MouseDown) {
    		selectedTileIndices = null;
    		if (editState == 3) selectionStart = Event.current.mousePosition;
    		else DrawTile();
    	}
    	else if (e.type == EventType.MouseUp) {
            if (editState == 3) {
    			selectionEnd = Event.current.mousePosition;
    			SelectTiles();
            }
            else {
                tileMap.UpdatePolygonColliders(0);
            }
    	}
    	else if (e.type == EventType.MouseDrag) {
    		if (editState != 3) DrawTile();	    

    		Handles.DrawSolidRectangleWithOutline(new Vector3[] {
    				new Vector3(selectionStart.x, selectionEnd.y, 0),
    				selectionStart,
    				selectionEnd,
					new Vector3(selectionEnd.x, selectionStart.y, 0)
				},
				new Color(1,1,1,0.1f),
				new Color(1,1,1,0.5f)
			);      
	    }

    	GUIUtility.hotControl = controlId;
    }

    void DrawTile () {
    	int tileIndex = selectedTileIndex;
    	if (selectedTileSet == null) { 
    		selectedTileSet = tmxFile.tileSets[0];
    		tileIndex = selectedTileSet.firstGID;
    	}

    	if (editState == 2) {
    		tileIndex = selectedTileSet.firstGID - 1;
    	}

    	Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        float dist = 0;
        Plane plane = new Plane(Vector3.forward, tileMap.transform.position);
        if (plane.Raycast(ray, out dist)) {
        	Vector3 p = ray.GetPoint(dist);
        	if (tileMap.SetTile(tileIndex, 0, p - tileMap.transform.position)) {
		        Event.current.Use();
	        }
        }
    }

    void SelectTiles () {
		Ray startRay = HandleUtility.GUIPointToWorldRay(selectionStart);
		Ray endRay = HandleUtility.GUIPointToWorldRay(selectionEnd);
        float startDist = 0;
        float endDist = 0;
        Plane plane = new Plane(Vector3.forward, tileMap.transform.position);
        if (plane.Raycast(startRay, out startDist) && plane.Raycast(endRay, out endDist)) {
        	Vector3 startPos = startRay.GetPoint(startDist) - tileMap.transform.position;
        	Vector3 endPos = endRay.GetPoint(endDist) - tileMap.transform.position;

			int startX = Mathf.Clamp(Mathf.FloorToInt(startPos.x / tileMap.offset.x), 0, tmxFile.width);
			int startY = Mathf.Clamp(Mathf.FloorToInt(startPos.y / tileMap.offset.y), 0, tmxFile.height);
        
			int endX = Mathf.Clamp(Mathf.FloorToInt(endPos.x / tileMap.offset.x), 0, tmxFile.width);
			int endY = Mathf.Clamp(Mathf.FloorToInt(endPos.y / tileMap.offset.y), 0, tmxFile.height);
        
        	int width = Mathf.Abs(endX - startX);
        	int height = Mathf.Abs(endY - startY);
        	int a = Mathf.Min(startX, endX);
        	int b = Mathf.Min(startY, endY);
			selectedTileIndices = new int[width * height];
			int i = 0;
			for (int x = a; x < a + width; x++) {
				for (int y = b; y < b + width; y++) {
					selectedTileIndices[i] = x + y * tmxFile.width;
				}
			}
        }

        if (selectedTileIndices != null) {
        	Debug.Log(selectedTileIndices.Length);
	        Event.current.Use();
        }
    }
}
}