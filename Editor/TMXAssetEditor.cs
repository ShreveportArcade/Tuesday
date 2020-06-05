/*
Copyright (C) 2018 Nolan Baker

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

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;

namespace Tiled {
[InitializeOnLoad]
[CustomEditor(typeof(DefaultAsset))]
class TMXAssetEditor : Editor {

    private AssetImporter importer;  
    private int _pixelsPerUnit = -1;  
    private int pixelsPerUnit {
        get {
            if (_pixelsPerUnit < 0) {
                if (importer == null) importer = AssetImporter.GetAtPath(path);
                string userData = importer.userData;
                if (string.IsNullOrEmpty(userData)) _pixelsPerUnit = tmxFile.tileWidth;
                else _pixelsPerUnit = Int32.Parse(userData);
            }
            return _pixelsPerUnit;
        }
        set {
            if (_pixelsPerUnit != value) {
                _pixelsPerUnit = value;
                if (importer == null) importer = AssetImporter.GetAtPath(path);
                importer.userData = "" + _pixelsPerUnit;
            }
        }
    }

    private int spacing = -1;
    private int margin = -1;

    private string path {
        get { return AssetDatabase.GetAssetPath(target); }
    }

    private bool isTMX {
        get { return Path.GetExtension(path) == ".tmx"; }
    } 

    private bool isTSX {
        get { return Path.GetExtension(path) == ".tsx"; }
    }

    private static Dictionary<string, TMXFile> tmxFiles = new Dictionary<string, TMXFile>();
    private TMXFile tmxFile {
        get {
            if (!tmxFiles.ContainsKey(path)) {
                tmxFiles[path] = TMXFile.Load(path);
            } 
            return tmxFiles[path];
        }
    }

    private static Dictionary<string, TileSet> tileSets = new Dictionary<string, TileSet>();
    private TileSet tileSet {
        get {
            if (!tileSets.ContainsKey(path)) {
                tileSets[path] = TileSet.Load(path);
            } 
            return tileSets[path];
        }
    }

    private static readonly Type hierarchyType;
    static TMXAssetEditor () {
        Assembly editorAssembly = typeof(EditorWindow).Assembly;
        hierarchyType = editorAssembly.GetType("UnityEditor.SceneHierarchyWindow");

        EditorApplication.update += EditorUpdate;
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyGUICallback;
        SceneView.duringSceneGui += SceneGUICallback;
    }

    public override void OnInspectorGUI() {
        GUI.enabled = true;
        if (isTMX) {
            pixelsPerUnit = EditorGUILayout.IntField("Pixels / Unit: ", pixelsPerUnit);
        }
        else if (isTSX) {
            if (spacing < 0 || margin < 0) {
                spacing = tileSet.spacing;
                margin = tileSet.margin;
            }

            bool padChange = (spacing != tileSet.spacing || margin != tileSet.margin);
            GUI.backgroundColor = padChange ? Color.red : Color.white;
            spacing = Mathf.Clamp(EditorGUILayout.IntField("Spacing", spacing), 0, 8);
            margin = Mathf.Clamp(EditorGUILayout.IntField("Margin", margin), 0, 8);
            GUI.backgroundColor = Color.white;
            GUI.enabled = padChange;
            if (GUILayout.Button("Update Texture Padding")) UpdateTexturePadding();
            if (GUI.changed) {
                Undo.FlushUndoRecordObjects();
            }
            GUI.enabled = false;
        }
        else base.OnInspectorGUI();
    }

    private static void EditorUpdate() {
        if (Application.isPlaying) return;
        EditorWindow window = EditorWindow.mouseOverWindow;
        if (window && window.GetType() == hierarchyType) {
            if (!window.wantsMouseMove) window.wantsMouseMove = true;
        }   
    }

    static Transform dropTarget;
    private static void HierarchyGUICallback(int pID, Rect pRect) {
        Event e = Event.current;
        if (e.type == EventType.DragUpdated) {
            if (pRect.Contains(e.mousePosition)) {
                UnityEngine.Object obj = EditorUtility.InstanceIDToObject(pID);
                if (obj is GameObject) dropTarget = (obj as GameObject).transform;
            }
            else dropTarget = null;
        }
        
        DragAndDropTMXFile(parent: dropTarget);
    }

    private static void SceneGUICallback (SceneView sceneView) {
        DragAndDropTMXFile(true);
    }

    private static void DragAndDropTMXFile (bool isSceneView = false, Transform parent = null) {
        EventType eventType = Event.current.type;
        if (eventType == EventType.DragUpdated || eventType == EventType.DragPerform) {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (eventType == EventType.DragPerform) {
                bool foundTMX = false;
                foreach (UnityEngine.Object o in DragAndDrop.objectReferences) {
                    string tmxFilePath = AssetDatabase.GetAssetPath(o);
                    if (Path.GetExtension(tmxFilePath) == ".tmx") {
                        float pixelsPerUnit = -1;
                        AssetImporter importer = AssetImporter.GetAtPath(tmxFilePath);
                        if (!string.IsNullOrEmpty(importer.userData)) {
                            pixelsPerUnit = float.Parse(importer.userData);
                        }

                        string name = Path.GetFileNameWithoutExtension(tmxFilePath);
                        GameObject map = new GameObject(name);
                        TileMap tileMap = map.AddComponent<TileMap>();
                        TMXFile tmxFile = TMXFile.Load(tmxFilePath);
                        tileMap.tileSetMaterials = TileMapEditor.GetMaterials(tmxFile, tmxFilePath);
                        tileMap.Setup(tmxFile, tmxFilePath, pixelsPerUnit);

                        if (isSceneView) {
                            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                            float dist = 0;
                            Plane plane = new Plane(Vector3.forward, tileMap.transform.position);
                            if (plane.Raycast(ray, out dist)) {
                                map.transform.position = ray.GetPoint(dist);
                            }
                        }
                        
                        if (parent != null) map.transform.SetParent(parent);

                        Event.current.Use();
                        Selection.activeGameObject = map;
                        Undo.RegisterCreatedObjectUndo (map, "Created '" + name + "' from TMX file.");
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

    private void UpdateTexturePadding () {
        tileSets[path] = TileSet.Load(path); // DELETE ME
        TileSet ts = tileSet;
        Texture2D texture = TileMapEditor.GetTileSetTexture(ts, path);
        string texturePath = AssetDatabase.GetAssetPath(texture);

        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(texturePath);
        importer.isReadable = true;
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        int inWidth = ts.tileWidth * ts.columns + ts.margin * 2 + ts.spacing * (ts.columns - 1);
        int outWidth = ts.tileWidth * ts.columns + margin * 2 + spacing * (ts.columns - 1);
        int outHeight = ts.tileHeight * ts.rows + margin * 2 + spacing * (ts.rows - 1);

        Color[] inColors = texture.GetPixels();
        Color[] outColors = new Color[outWidth * outHeight];

        for (int column = 0; column < ts.columns; column++) {
            for (int row = 0; row < ts.rows; row++) {
                for (int i = 0; i < ts.tileWidth; i++) {
                    for (int j = 0; j < ts.tileHeight; j++) {
                        int inX = i + (ts.tileWidth + ts.spacing) * column + ts.margin;
                        int inY = j + (ts.tileHeight + ts.spacing) * row + ts.margin;
                        Color c = inColors[inY * inWidth + inX]; 

                        int left = (i == 0) ? ((column==0) ? -margin : -spacing/2) : 0;
                        int right = (i == ts.tileWidth-1) ? ((column==ts.columns-1) ? margin : spacing/2) : 0;
                        int down = (j == 0) ? ((row==0) ? -margin : -spacing/2) : 0;
                        int up = (j == ts.tileHeight-1) ? ((row==ts.rows-1) ? margin : spacing/2) : 0;
                        for (int x = i + left; x <= i + right; x++) {
                            for (int y = j + down; y <= j + up; y++) {
                                int outX = x + (ts.tileWidth + spacing) * column + margin;
                                int outY = y + (ts.tileHeight + spacing) * row + margin;                                
                                outColors[outY * outWidth + outX] = c;
                            }
                        }
                    }
                }
            }
        }

        Texture2D t = new Texture2D(outWidth, outHeight);
        t.SetPixels(outColors);
        byte[] bytes = t.EncodeToPNG();
        File.WriteAllBytes(texturePath, bytes);

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        tileSet.margin = margin;
        tileSet.spacing = spacing;
        tileSet.image.width = outWidth;
        tileSet.image.height = outHeight;
        tileSet.Save(path);
    }
}
}