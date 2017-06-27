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

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;

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

    private string path {
        get { return AssetDatabase.GetAssetPath(target); }
    }

    private bool isValid {
        get { return Path.GetExtension(path) == ".tmx"; }
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

	public override void OnInspectorGUI() {
        if (!isValid) {
            base.OnInspectorGUI();
            return;
        }

        GUI.enabled = true;

        pixelsPerUnit = EditorGUILayout.IntField("Pixels / Unit: ", pixelsPerUnit);
    }

    static TMXAssetEditor () {
        EditorApplication.hierarchyWindowItemOnGUI += HierarchyGUICallback;
        SceneView.onSceneGUIDelegate += SceneGUICallback;
    }

    private static void HierarchyGUICallback(int pID, Rect pRect) {
        DragAndDropTMXFile();
    }

    private static void SceneGUICallback (SceneView sceneView) {
        DragAndDropTMXFile(true);
    }

    private static void DragAndDropTMXFile (bool isSceneView = false) {
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
                                Event.current.Use();
                            }
                        }
                        else {
                            // place at origin relative to object dropped on in Hierarchy
                        }

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
}
}