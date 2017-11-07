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
                    mat = new Material(Shader.Find("Sprites/Default"));
                    mat.EnableKeyword("PIXELSNAP_ON");
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
            if (tileSet.source == null) {
                texturePath = Path.Combine(Path.GetDirectoryName(path), texturePath);
            }
            else {
                string tileSetPath = Path.Combine(Path.GetDirectoryName(path), tileSet.source);
                texturePath = Path.Combine(Path.GetDirectoryName(tileSetPath), texturePath);
            }
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

    private static int selectedLayer = 0;
    private static int selectedTerrainIndex = 0;
    private Terrain[] _terrains;
    private Terrain[] terrains {
        get {
            if (_terrains == null || _terrains.Length == 0) {
                List<Terrain> terrainList = new List<Terrain>();
                foreach (TileSet tileSet in tmxFile.tileSets) {
                    if (tileSet.terrainTypes == null) continue;
                    foreach (Terrain terrain in tileSet.terrainTypes) {
                        terrainList.Add(terrain);
                    }
                }
                _terrains = terrainList.ToArray();
            }
            return _terrains;
        }
    }
    private Terrain selectedTerrain {
        get {
            if (selectedTerrainIndex >= 0 && selectedTerrainIndex < terrains.Length) {
                return terrains[selectedTerrainIndex];
            }
            return null;
        }
    }

    Rect tileRect;
    private void TileSetField (TileSet tileSet) {
        int id = tileSet.firstGID;

        EditorGUIUtility.hierarchyMode = false;
        Rect r = GUILayoutUtility.GetRect(Screen.width - 40, EditorGUIUtility.singleLineHeight);
        float w = r.width;
        r.width = 20;
        bool show = !tileSetFoldoutStates.ContainsKey(id) || tileSetFoldoutStates[id];
        tileSetFoldoutStates[id] = EditorGUI.Foldout(r, show, tileSet.name);
        
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
    private static int paintType = 0;
    public override void OnInspectorGUI() {	
        editState = GUILayout.Toolbar(editState, new string[] {"Move", "Paint", "Erase", "Select"});
            
        DefaultAsset asset = AssetDatabase.LoadAssetAtPath(path, typeof(DefaultAsset)) as DefaultAsset;
        asset = EditorGUILayout.ObjectField("TMX File", asset, typeof(DefaultAsset), false) as DefaultAsset;
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (path != assetPath && Path.GetExtension(assetPath) == ".tmx") {
            Undo.RecordObject(target, "Assign new TMX file");
            path = assetPath;
            tmxFile = TMXFile.Load(assetPath);
            tileMap.Setup();
        }
        
        base.OnInspectorGUI();
        EditorGUILayout.Separator();

        if (tmxFile.layers != null) {
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
            string[] layerNames = Array.ConvertAll(tmxFile.layers, (layer) => layer.name);
            selectedLayer = GUILayout.SelectionGrid(selectedLayer, layerNames, 1);
            EditorGUILayout.Separator();
        }
        
        if (tmxFile.tileSets != null) {
            EditorGUILayout.LabelField("Tile Sets", EditorStyles.boldLabel);
            paintType = GUILayout.Toolbar(paintType, new string[] {"Tiles", "Terrains"});
            switch (paintType) {
                case 0:
                    foreach (TileSet tileSet in tmxFile.tileSets) {
                        TileSetField(tileSet);
                    }
                    break;
                case 1:
                    string[] terrainNames = Array.ConvertAll(terrains, (terrain) => terrain.name);
                    selectedTerrainIndex = GUILayout.SelectionGrid(selectedTerrainIndex, terrainNames, 1);
                    break;
                default:
                    break;
            }
            EditorGUILayout.Separator();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Revert")) {
            tmxFile = TMXFile.Load(path);
            tileMap.Setup();
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
            AssetDatabase.Refresh();
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
        if (t != null && t.objectGroup != null && t.objectGroup.objects != null && t.objectGroup.objects.Length > 0) {
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
        Event e = Event.current;
        if (e == null) return;

        #if UNITY_2017_1_OR_NEWER
        if (e.isKey && e.modifiers == EventModifiers.None && e.keyCode == KeyCode.F) {
            SceneView.lastActiveSceneView.Frame(tileMap.bounds, false);
            e.Use();
            return;
        }
        #endif

        if (editState == 0 || e.modifiers != EventModifiers.None) return;
        else if (editState == 3) DrawSelection();
        else Undo.RecordObject(target, "Draw/Erase Tiles");

        if (e.type == EventType.MouseDown) {
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
            selectedTileIndices = null;
            if (editState == 3) selectionStart = MouseToWorldPoint();
            else DrawTile(false);
        }
        else if (e.type == EventType.MouseDrag) {
            if (editState != 3) DrawTile(true); 
            else {
                selectionEnd = MouseToWorldPoint();
            }
        }
        else if (e.type == EventType.MouseUp) {
            if (editState == 3) SelectTiles();
            else {
                tileMap.tmxFile.layers[selectedLayer].Encode();
                tileMap.UpdatePolygonColliders(selectedLayer);
            }
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

    Vector3 lastTilePos;
    Vector3 tilePos;
    void DrawTile (bool drag) {
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
            Vector3 p = ray.GetPoint(dist) - tileMap.transform.position;
            lastTilePos = drag ? tilePos : p;
            tilePos = p;
            switch (paintType) {
                case 0:
                    if ((!drag && tileMap.SetTile(tileIndex, selectedLayer, tilePos)) || 
                        (drag && tileMap.SetTiles(tileIndex, selectedLayer, lastTilePos, tilePos))) {
                        Event.current.Use();
                        EditorUtility.SetDirty(target);
                    }
                    break;
                case 1:
                    if (selectedTerrain != null && tileMap.SetTerrain(selectedTerrain.tile, selectedLayer, tilePos)) {
                        Event.current.Use();
                        EditorUtility.SetDirty(target);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    void SelectTiles () {
        Vector3 startPos = selectionStart - tileMap.transform.position;
        Vector3 endPos = selectionEnd - tileMap.transform.position;

        int startX = Mathf.Clamp(Mathf.FloorToInt(startPos.x / tileMap.tileOffset.x), 0, tmxFile.width);
        int startY = Mathf.Clamp(Mathf.FloorToInt(startPos.y / tileMap.tileOffset.y), 0, tmxFile.height);
    
        int endX = Mathf.Clamp(Mathf.FloorToInt(endPos.x / tileMap.tileOffset.x), 0, tmxFile.width);
        int endY = Mathf.Clamp(Mathf.FloorToInt(endPos.y / tileMap.tileOffset.y), 0, tmxFile.height);
    
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