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
    private static bool showGrid = true;
    private static Color gridColor = new Color(0,0,0,0.25f);
    

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

    void OnEnable () {
        Undo.undoRedoPerformed += UndoRedo;
    }
    void OnDisable () {
        Undo.undoRedoPerformed -= UndoRedo;
    }

    void UndoRedo () {
        tileMap.ReloadMap();
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
    private static Dictionary<string, Material> tileSetMaterials = new Dictionary<string, Material>();
    public static Material[] GetMaterials (TMXFile tmxFile, string path) {
        Material[] materials = new Material[tmxFile.tileSets.Length];
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            TileSet tileSet = tmxFile.tileSets[i];
            if (tileSet == null || tileSet.image == null || string.IsNullOrEmpty(tileSet.image.source)) continue;

            Material mat = null;
            if (tileSetMaterials.ContainsKey(tileSet.image.source)) {
                mat = tileSetMaterials[tileSet.image.source];
            }
            else {
                string materialPath = Path.Combine(Path.GetDirectoryName(tileSet.image.source), "Materials");
                materialPath = Path.Combine(materialPath, Path.GetFileNameWithoutExtension(tileSet.image.source) + ".mat");
                materialPath = Path.Combine(Path.GetDirectoryName(path), materialPath);
                materialPath = Path.GetFullPath(materialPath);
                string materialDir = Path.GetDirectoryName(materialPath);                
                Directory.CreateDirectory(materialDir);
                string dataPath = Path.GetFullPath(Application.dataPath);
                materialPath = materialPath.Replace(dataPath, "Assets");
                mat = AssetDatabase.LoadAssetAtPath(materialPath, typeof(Material)) as Material;
                if (mat == null) {
                    mat = new Material(Shader.Find("Unlit/Transparent"));
                    mat.mainTexture = GetTileSetTexture(tileSet, path);
                    AssetDatabase.CreateAsset(mat, materialPath);
                }
            }
            if (mat != null) tileSetMaterials[tileSet.image.source] = mat;
            
            materials[i] = mat;
        }
        return materials;
    }

    public static Texture2D GetTileSetTexture (TileSet tileSet, string path) {
        if (tileSet.image == null || tileSet.image.source == null) return null;

        Texture2D tex = null;
        if (tileSetTextures.ContainsKey(tileSet.image.source)) {
            tex = tileSetTextures[tileSet.image.source];
        }
        else {
            string texturePath = tileSet.image.source;
            texturePath = Path.Combine(Path.GetDirectoryName(path), texturePath);
            texturePath = Path.GetFullPath(texturePath);
            string dataPath = Path.GetFullPath(Application.dataPath);
            texturePath = texturePath.Replace(dataPath, "Assets");
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

    Rect tileRect;
    private void TileSetField (TileSet tileSet) {
        int id = tileSet.firstGID;
        Rect r = GUILayoutUtility.GetRect(Screen.width - 40, EditorGUIUtility.singleLineHeight);
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

        if (selectedTileSet == null) tileRect = new Rect(-1,-1,0,0);

        if (tileSetFoldoutStates[tileSet.firstGID]) {
            Texture2D currentTexture = GetTileSetTexture(tileSet, path);
            Texture2D tex = EditorGUILayout.ObjectField(currentTexture, typeof(Texture2D), false) as Texture2D;
            if (currentTexture != tex) {
                Uri textureURI = new Uri("/" + AssetDatabase.GetAssetPath(tex));
                Uri tmxFileURI = new Uri("/" + Path.GetDirectoryName(path));
                tileSet.image.source = "../" + tmxFileURI.MakeRelativeUri(textureURI);
            }

            if (tex != null) {
                float x = Screen.width - 40;
                float y = tex.height * x / (float)tex.width;
                if (x > tex.width) {
                    x = tex.width;
                    y = tex.height;
                }
                r = GUILayoutUtility.GetRect(x, y);
                r.width = r.height * (float)tex.width / (float)tex.height;
                r.height = r.width * (float)tex.height / (float)tex.width;
                r.x = (Screen.width - r.width) * 0.5f;
                EditorGUI.DrawPreviewTexture(r, tex, mat);

                if (selectedTileSet != null && selectedTileSet == tileSet && selectedTileIndex > 0) {
                    TileRect uvTileRect = selectedTileSet.GetTileUVs(selectedTileIndex);
                    tileRect = r;
                    tileRect.x += uvTileRect.x * r.width;
                    tileRect.y += (1 - (uvTileRect.y + uvTileRect.height)) * r.height;
                    tileRect.width *= uvTileRect.width;
                    tileRect.height *= uvTileRect.height;
                }

                Handles.DrawSolidRectangleWithOutline(tileRect, Color.clear, Color.white);
                HandleUtility.Repaint();

                if (Event.current.type == EventType.MouseDown && 
                    Event.current.button == 0 && 
                    r.Contains(Event.current.mousePosition)) {
                    selectedTileSet = tileSet;
                    selectedTileIndex = GetTileIndex(tileSet, r, Event.current.mousePosition);
                    Event.current.Use();
                }
            }
            EditorGUILayout.Space();
        }
    }

    private static int editState = 0;
    public override void OnInspectorGUI() {	
        showGrid = EditorGUILayout.Toggle("Show Grid?", showGrid);
        if (showGrid) gridColor = EditorGUILayout.ColorField("Grid Color", gridColor);

		base.OnInspectorGUI();

    	EditorGUIUtility.hierarchyMode = true;
        showTileSets = EditorGUILayout.Foldout(showTileSets, "Tile Sets:");
        EditorGUIUtility.hierarchyMode = false;
        if (showTileSets && tmxFile.tileSets != null) {
	    	editState = GUILayout.Toolbar(editState, new string[] {"Move", "Paint", "Erase", "Select"});
            foreach (TileSet tileSet in tmxFile.tileSets){
                TileSetField(tileSet); 
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Revert")) {
            tmxFile = TMXFile.Load(path);
			tileMap.ReloadMap();
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

        Texture2D tex = GetTileSetTexture(selectedTileSet, path);
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

        Tile t = selectedTileSet.GetTile(selectedTileIndex);
        if (t != null && t.objectGroup != null && t.objectGroup.objects.Length > 0) {
            foreach (TileObject obj in t.objectGroup.objects) {
                if (obj.polygonSpecified) {
                    TilePoint[] path = obj.polygon.path;
                    Vector3[] poly = System.Array.ConvertAll(path, (p) => {
                        Vector3 v = new Vector3(p.x + obj.x, p.y + obj.y, 0);
                        v.x *= r.width / (float)tmxFile.tileWidth;
                        v.y *= r.height / (float)tmxFile.tileHeight;
                        v.x += r.x;
                        v.y += r.y;
                        return v;
                    });
                    Handles.color = new Color(1,1,1,0.1f);
                    Handles.DrawAAConvexPolygon(poly);
                }
            }
        }
    }

    Vector3 selectionStart;
    Vector3 selectionEnd;
    int[] selectedTileIndices = null;
    void OnSceneGUI () {
        DrawGrid();

    	if (editState == 0) return;
        else if (editState == 3) DrawSelection();
        else Undo.RecordObject(target, "Draw/Erase Tiles");

    	Event e = Event.current;
    	if (e == null) return;

    	if (e.type == EventType.MouseDown) {
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
    		selectedTileIndices = null;
    		if (editState == 3) selectionStart = MouseToWorldPoint();
    		else DrawTile();
    	}
        else if (e.type == EventType.MouseDrag) {
            if (editState != 3) DrawTile(); 
            else {
                selectionEnd = MouseToWorldPoint();
                HandleUtility.Repaint();
            }
        }
    	else if (e.type == EventType.MouseUp) {
            if (editState == 3) SelectTiles();
            else tileMap.UpdatePolygonColliders(0);
            GUIUtility.hotControl = 0;
            Undo.FlushUndoRecordObjects();
    	}

    	
    }

    Vector3 MouseToWorldPoint () {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        float dist = 0;
        Plane plane = new Plane(Vector3.forward, tileMap.transform.position);
        if (plane.Raycast(ray, out dist)) {
            return ray.GetPoint(dist) - tileMap.transform.position;
        }
        return Vector3.zero;
    }

    void DrawSelection () {
        Handles.DrawSolidRectangleWithOutline(new Vector3[] {
                selectionStart,
                new Vector3(selectionStart.x, selectionEnd.y, 0),
                selectionEnd,
                new Vector3(selectionEnd.x, selectionStart.y, 0)
            },
            new Color(1,1,1,0.1f),
            new Color(1,1,1,0.5f)
        );  
    }

    
    public void DrawGrid () {
        if (!showGrid || tmxFile == null) return;

        Handles.color = gridColor;
        Handles.matrix = tileMap.gameObject.transform.localToWorldMatrix;
        if (tmxFile.orientation == "orthogonal") {
            for (int x = 1; x < tmxFile.width; x++) {
                Handles.DrawLine(
                    new Vector3(x * tileMap.offset.x, 0, 0),
                    new Vector3(x * tileMap.offset.x, tmxFile.height * tileMap.offset.y, 0)
                );
            }

            for (int y = 1; y < tmxFile.height; y++) {
                Handles.DrawLine(
                    new Vector3(0, y * tileMap.offset.y, 0),
                    new Vector3(tmxFile.width * tileMap.offset.x, y * tileMap.offset.y, 0)
                );   
            }
        }
        HandleUtility.Repaint();
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
                EditorUtility.SetDirty(target);
	        }
        }
    }

    void SelectTiles () {
        Vector3 startPos = selectionStart - tileMap.transform.position;
        Vector3 endPos = selectionEnd - tileMap.transform.position;

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

        if (selectedTileIndices != null) {
	        Event.current.Use();
        }
    }
}
}