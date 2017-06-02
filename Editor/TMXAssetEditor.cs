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

    private static Material _mat;
    private static Material mat {
        get {
            if (_mat == null) _mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            return _mat;
        }
    }    

	public override void OnInspectorGUI() {
        if (!isValid) {
            base.OnInspectorGUI();
            return;
        }

        GUI.enabled = true;

        pixelsPerUnit = EditorGUILayout.IntField("Pixels / Unit: ", pixelsPerUnit);
        tmxFile.width = EditorGUILayout.IntField("Map Width: ", tmxFile.width);
        tmxFile.height = EditorGUILayout.IntField("Map Height: ", tmxFile.height);
        tmxFile.tileWidth = EditorGUILayout.IntField("Tile Width: ", tmxFile.tileWidth);
        tmxFile.tileHeight = EditorGUILayout.IntField("Tile Height: ", tmxFile.tileHeight);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Revert")) {
            tmxFiles[path] = TMXFile.Load(path);
        }
        if (GUILayout.Button("Save")) {
            tmxFiles[path].Save(path);
            AssetDatabase.ImportAsset(path);
        }
        if (GUILayout.Button("Save As")) {
            tmxFiles[path].Save(
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

    static TMXAssetEditor () {
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
                        float pixelsPerUnit = -1;
                        AssetImporter importer = AssetImporter.GetAtPath(path);
                        if (!string.IsNullOrEmpty(importer.userData)) {
                            pixelsPerUnit = float.Parse(importer.userData);
                        }

                        string name = Path.GetFileNameWithoutExtension(path);
                        GameObject map = new GameObject(name);
                        TileMap tileMap = map.AddComponent<TileMap>();
                        tileMap.Setup(path, pixelsPerUnit);
                        TMXFile tmxFile = tileMap.tmxFile;
                        foreach (MeshRenderer meshRenderer in map.GetComponentsInChildren<MeshRenderer>()) {
                            meshRenderer.sharedMaterial = mat;
                            MaterialPropertyBlock propBlock = new MaterialPropertyBlock ();
                 
                            propBlock.SetTexture("_MainTex", TileMapEditor.GetTileSetTexture(tmxFile.tileSets[0], path));
                            meshRenderer.SetPropertyBlock (propBlock);
                        }

                        Undo.RegisterCreatedObjectUndo (map, "Created '" + name + "' from TMX file.");

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
}
}