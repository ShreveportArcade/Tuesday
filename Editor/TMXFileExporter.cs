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

[CustomEditor(typeof(Grid))]
public class TMXFileExporter : Editor {

    Grid grid { get { return target as Grid; } }
    int pixelsPerUnit {
        get { return 32; }
    }

    string _tmxFilepath;
    string tmxFilePath {
        get {
            if (string.IsNullOrEmpty(_tmxFilepath)) {
                string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(grid.gameObject));
                if (Path.GetExtension(path) == ".tmx") _tmxFilepath = path;
            }
            return _tmxFilepath;
        }
        set {
            if (value != _tmxFilepath && Path.GetExtension(value) == ".tmx") {
                _tmxFilepath = value;
            }
        }
    }
    
    public override void OnInspectorGUI() {
        Tilemap tilemap = grid.GetComponent<Tilemap>();
        if (tilemap == null) {// only TSX files have tilemap on root
            GetFilePath();
            DrawFilePanel();
            EditorGUILayout.Space();
        }
        base.OnInspectorGUI();
    }

    void GetFilePath() {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(tmxFilePath);        
        asset = EditorGUILayout.ObjectField("TMX File", asset, typeof(UnityEngine.Object), false) as UnityEngine.Object;
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (tmxFilePath != assetPath && Path.GetExtension(assetPath) == ".tmx") {
            // Undo.RecordObject(target, "Assign new TMX file");
            tmxFilePath = assetPath;
            // tmxFile = TMXFile.Load(assetPath);
            // TODO: prefab replace?
            Debug.Log("NEW TMX FILE ASSIGNED!!!");
        }
    }

    void DrawFilePanel () {        
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = (tmxFilePath != null) && grid.gameObject.scene != null;        
        if (GUILayout.Button("Reload")) {
            Debug.Log("TODO: asset replace from file");
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

    int globalLayerID;
    void SaveTMX (string tmxFilePath) {
        Tiled.TMXFile tmxFile = new Tiled.TMXFile();
        tmxFile.orientation = GetOrientation();
        tmxFile.renderOrder = GetRenderOrder(tmxFile.orientation);
        BoundsInt bounds = GetBounds();
        tmxFile.width = bounds.size.x;
        tmxFile.height = bounds.size.y;
        Vector3Int size = GetTileSize(tmxFile.orientation);
        tmxFile.tileWidth = size.x;
        tmxFile.tileHeight = size.y;
        tmxFile.hexSideLength = size.z;
        tmxFile.staggerAxis = null;
        tmxFile.staggerIndex = null;
        tmxFile.backgroundColor = null;
        tmxFile.nextObjectID = 0;
        tmxFile.tileSets = GetTileSets(bounds, tmxFilePath);
        globalLayerID = 0;
        tmxFile.layers = GetLayers(grid.transform);
        
        string path = Path.Combine("Assets/TEST_EXPORT.tmx");
        tmxFile.Save(path);
        AssetDatabase.ImportAsset(path);
    }

    List<Tiled.Layer> GetLayers (Transform root) {
        List<Tiled.Layer> layers = new List<Tiled.Layer>();
        for (int i = 0; i < root.childCount; i++) {
            Transform t = root.GetChild(i);
            Tilemap tilemap = t.GetComponent<Tilemap>();

            Tiled.Layer layer = null;
            
            int id = globalLayerID;
            globalLayerID++;
            if (tilemap != null) layer = CreateTileLayer(t);                
            else if (t.childCount > 0) {
                if (t.GetComponentInChildren<Tilemap>()) layer = CreateGroupLayer(t);
                else layer = CreateObjectGroup(t);
            }
            else if (t.GetComponent<SpriteRenderer>()) layer = CreateImageLayer(t);
            else layer = CreateGroupLayer(t);

            layer.id = id;
            layer.name = t.name;
            layer.visible = t.gameObject.activeSelf;
            layer.offsetX = Mathf.RoundToInt(t.localPosition.x * pixelsPerUnit);
            layer.offsetY = Mathf.RoundToInt(-t.localPosition.y * pixelsPerUnit);
            layer.properties = GetProperties(t);

            layers.Add(layer);
        }
        return layers;
    }

    Tiled.Property[] GetProperties (Transform t) {
        return null;
    }

    Tiled.TileLayer CreateTileLayer (Transform t) {
        Tiled.TileLayer layer = new Tiled.TileLayer();
        layer.tileData = new Tiled.Data();
        Tilemap tilemap = t.GetComponent<Tilemap>();
        BoundsInt bounds = tilemap.cellBounds;
        TileBase[] tiles = tilemap.GetTilesBlock(bounds);
        layer.width = bounds.size.x;
        layer.height = bounds.size.y;
        for (int j = 0; j < layer.height; j++) {
            for (int i = 0; i < layer.width; i++) {
                int id = 0;
                int x = i + bounds.xMin;
                int y = i + bounds.yMin;
                layer.SetTileID(id, x, y);
            }
        }

        layer.Encode();
        return layer;
    }

    Tiled.ObjectGroup CreateObjectGroup (Transform root) {
        Tiled.ObjectGroup layer = new Tiled.ObjectGroup();
        List<Tiled.TileObject> objects = new List<Tiled.TileObject>();
        for (int i = 0; i < root.childCount; i++) {
            Transform t = root.GetChild(i);
            if (PrefabUtility.IsPartOfPrefabInstance(t)) {
                
            }
            else {

            }
        }
        layer.objects = objects.ToArray();
        return layer;
    }

    Tiled.ImageLayer CreateImageLayer (Transform t) {
        Tiled.ImageLayer layer = new Tiled.ImageLayer();
        // get sprite
        return layer;
    }

    Tiled.GroupLayer CreateGroupLayer (Transform t) {
        Tiled.GroupLayer layer = new Tiled.GroupLayer();
        layer.layers = GetLayers(t);
        return layer;
    }

    Tiled.TileSet[] GetTileSets(BoundsInt bounds, string tmxFilePath) {
        string dir = Path.GetDirectoryName(tmxFilePath);
        List<Tiled.TileSet> tileSets = new List<Tiled.TileSet>();
        string[] paths = GetTileSetPaths(bounds, grid.transform);
        foreach (string path in paths) {
            if (Path.GetExtension(path) != ".tsx") {
                Debug.Log("TODO: implement non-TSX tile sets... " + path);
                continue;
            }
            Tiled.TileSet tileSet = Tiled.TileSet.Load(path);
            Debug.Log("TODO: set tileset source and firstGID");
            
            tileSets.Add(tileSet);
        }
        return tileSets.ToArray();
    }

    string[] GetTileSetPaths(BoundsInt bounds, Transform root) {
        HashSet<string> tileSetPaths = new HashSet<string>();
        for (int i = 0; i < root.childCount; i++) {
            Transform t = root.GetChild(i);
            Tilemap tilemap = t.GetComponent<Tilemap>();
            if (tilemap != null) {
                TileBase[] tiles = tilemap.GetTilesBlock(bounds);
                List<string> paths = new List<string>();
                foreach (TileBase tile in tiles) {
                    string path = AssetDatabase.GetAssetPath(tile);
                    if (string.IsNullOrEmpty(path) || paths.Contains(path)) continue;
                    paths.Add(path);
                }
                foreach (string path in paths) {
                    GridPalette gridPalette = AssetDatabase.LoadAssetAtPath(path, typeof(GridPalette)) as GridPalette;
                    if (gridPalette != null) tileSetPaths.Add(path);
                }
            }
            if (t.childCount > 0) {
                string[] paths = GetTileSetPaths(bounds, t);
                if (paths != null) tileSetPaths.UnionWith(tileSetPaths);
            }
        }
        return tileSetPaths.ToArray();
    }

    //TODO: find better way of distinguishing between iso and stag
    public string GetOrientation () {
        if (grid.cellLayout == GridLayout.CellLayout.Hexagon) return "hexagonal";
        else if (grid.cellLayout == GridLayout.CellLayout.Isometric) return "isometric";
        else if (grid.cellLayout == GridLayout.CellLayout.IsometricZAsY) return "staggered";
        return "orthogonal";
    }

    public string GetRenderOrder (string orientation) {
        TilemapRenderer renderer = grid.GetComponentInChildren<TilemapRenderer>();//assumes all render orders are the same
        if (orientation == "isometric" || orientation == "staggered") {
            if (renderer.sortOrder == TilemapRenderer.SortOrder.BottomLeft) return "left-up";
            else if (renderer.sortOrder == TilemapRenderer.SortOrder.TopRight) return "right-down";
            else if (renderer.sortOrder == TilemapRenderer.SortOrder.BottomRight) return "right-up";
        }
        else {
            if (renderer.sortOrder == TilemapRenderer.SortOrder.BottomLeft) return "right-up";
            else if (renderer.sortOrder == TilemapRenderer.SortOrder.TopRight) return "left-down";
            else if (renderer.sortOrder == TilemapRenderer.SortOrder.BottomRight) return "left-up";
        }
        return "right-down";
    }

    public BoundsInt GetBounds() {
        Tilemap[] tilemaps = grid.GetComponentsInChildren<Tilemap>();
        BoundsInt bounds = tilemaps[0].cellBounds;
        foreach (Tilemap tilemap in tilemaps) {
            if (bounds.xMin > tilemap.cellBounds.xMin) bounds.xMin = tilemap.cellBounds.xMin;
            if (bounds.xMax < tilemap.cellBounds.xMax) bounds.xMax = tilemap.cellBounds.xMax;
            if (bounds.yMax < tilemap.cellBounds.yMax) bounds.yMax = tilemap.cellBounds.yMax;
            if (bounds.yMin > tilemap.cellBounds.yMin) bounds.yMin = tilemap.cellBounds.yMin;            
        }
        return bounds;
    }

    Vector3Int GetTileSize(string orientation) {
        Tilemap tilemap = grid.GetComponentInChildren<Tilemap>();
        Vector3 size = tilemap.cellSize * pixelsPerUnit;//assumes all other tilemaps are the same
        if (orientation == "hexagonal") {
            // if (tmxFile.hexSideLength > 0) {
            //     size.x = (float)tmxFile.tileWidth/(float)(tmxFile.tileHeight+tmxFile.hexSideLength);
            //     size.y = 1-(float)tmxFile.hexSideLength/(float)(tmxFile.hexSideLength+tmxFile.tileHeight);
            // }
        }
        return new Vector3Int(
            Mathf.RoundToInt(size.x), 
            Mathf.RoundToInt(size.y), 
            Mathf.RoundToInt(size.z)
        );
    }
}