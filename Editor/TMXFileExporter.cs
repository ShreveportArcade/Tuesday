/*
Copyright (C) 2020 Nolan Baker

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
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Linq;

namespace Tiled {
[CustomEditor(typeof(Grid))]
public class TMXEditor : Editor {

    Grid grid { get { return target as Grid; } }

    string tmxFilePath;
    public override void OnInspectorGUI() {
        Tilemap tilemap = grid.GetComponent<Tilemap>();
        if (tilemap == null) {
            GetFilePath();
            DrawFilePanel();
            EditorGUILayout.Space();
        }
        base.OnInspectorGUI();
    }

    void GetFilePath() {
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(tmxFilePath, typeof(UnityEngine.Object)) as UnityEngine.Object;
        asset = EditorGUILayout.ObjectField("TMX File", asset, typeof(UnityEngine.Object), false) as UnityEngine.Object;
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (tmxFilePath != assetPath && Path.GetExtension(assetPath) == ".tmx") {
            // Undo.RecordObject(target, "Assign new TMX file");
            tmxFilePath = assetPath;
            // tmxFile = TMXFile.Load(assetPath);
            // TODO: prefab replace?
            Debug.Log("NEW TMX FILE ASSIGNED!!!");
        }
        if (tmxFilePath == null) {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(grid);
            if (!string.IsNullOrEmpty(path) && Path.GetExtension(path) == "tmx") {
                tmxFilePath = path;
                Debug.Log("SETTING PATH FROM PREFAB!!!");
            }
        }
    }

    void DrawFilePanel () {        
        GUI.enabled = (tmxFilePath != null);
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = (tmxFilePath != null);        
        if (GUILayout.Button("Reload")) {
            Debug.Log("TODO: prefab replace from file");
        }
        if (GUILayout.Button("Save")) {
            SaveTMX(tmxFilePath);
            AssetDatabase.ImportAsset(tmxFilePath);
        }
        GUI.enabled = true;
        if (GUILayout.Button("Save As")) {
            string path = EditorUtility.SaveFilePanel(
                "Save as TMX",
                Path.GetFullPath(Application.dataPath),
                grid.name,
                "tmx"
            );
            SaveTMX(path);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(path);
        }
        EditorGUILayout.EndHorizontal();
    }

    void SaveTMX (string path) {
        TMXFile tmxFile = new TMXFile();
        if (File.Exists(path)) {
            TMXFile oldTMXFile = TMXFile.Load(path);
            tmxFile.version = oldTMXFile.version;
            tmxFile.tiledVersion = oldTMXFile.tiledVersion;
            tmxFile.orientation = oldTMXFile.orientation;
            tmxFile.renderOrder = oldTMXFile.renderOrder;
            tmxFile.width = oldTMXFile.width;
            tmxFile.height = oldTMXFile.height;
            tmxFile.tileWidth = oldTMXFile.tileWidth;
            tmxFile.tileHeight = oldTMXFile.tileHeight;
            tmxFile.hexSideLength = oldTMXFile.hexSideLength;
            tmxFile.staggerAxis = oldTMXFile.staggerAxis;
            tmxFile.staggerIndex = oldTMXFile.staggerIndex;
            tmxFile.backgroundColor = oldTMXFile.backgroundColor;
            tmxFile.nextObjectID = oldTMXFile.nextObjectID;
        }
        else {
            // tmxFile.orientation = "orthogonal";
            // tmxFile.renderOrder = "right-down";
            // tmxFile.width = 0;
            // tmxFile.height = 0;
            // tmxFile.tileWidth = 0;
            // tmxFile.tileHeight = 0;
            // tmxFile.hexSideLength;
            // tmxFile.staggerAxis;
            // tmxFile.staggerIndex;
            // tmxFile.backgroundColor;
            // tmxFile.nextObjectID = 0;
        }

        List<TileSet> tileSets = new List<TileSet>();

        Debug.Log("find all tilesets");

        Debug.Log("iterate through transform hierarchy");

            Debug.Log("if layer is Tilemap, TileLayer");
            Debug.Log("if layer has any Tilemap children, GroupLayer");
            Debug.Log("if layer has no Tilemap children, ObjectGroup");
            Debug.Log("else ImageLayer");

    }
}
}