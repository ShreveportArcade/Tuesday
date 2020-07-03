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

    int pixelsPerUnit = -1;

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

        GUI.enabled = false;
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
        }

        EditorGUILayout.EndHorizontal();
    }

    Dictionary<TileBase, int> tileGIDs = new Dictionary<TileBase, int>();
    void GetTileAssetsAtPath (Tiled.TileSet tileSet, string path) {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        foreach (UnityEngine.Object asset in assets) {
            if (asset is Tile) {
                Tile tile = asset as Tile;
                string[] splitName = tile.name.Split('_');
                int gid = tileSet.firstGID + int.Parse(splitName[splitName.Length-1]);
                tileGIDs[tile] = gid;
            }
        }
    }

    int globalLayerID;
    Tiled.TMXFile tmxFile;
    Dictionary<string, Tiled.TileSet> tileSetCache = new Dictionary<string, Tiled.TileSet>();
    void SaveTMX (string tmxFilePath) {
        tmxFile = Tiled.TMXFile.Load(this.tmxFilePath);
        if (tmxFile == null) {
            tmxFile = new Tiled.TMXFile();
            Debug.Log("TODO: calculate pixelsPerUnit from referenced textures");
            tmxFile.tileHeight = 32;
        }
        if (pixelsPerUnit < 0) pixelsPerUnit = tmxFile.tileHeight;
        tmxFile.orientation = GetOrientation();
        tmxFile.renderOrder = GetRenderOrder(tmxFile.orientation);
        BoundsInt bounds = GetBounds();
        if (!tmxFile.infinite && bounds.size.x != 0 && bounds.size.y != 0) {
            tmxFile.width = bounds.size.x;
            tmxFile.height = bounds.size.y;
        }

        Vector3Int size = GetTileSize(tmxFile.orientation);
        if (size.x != 0 && size.y != 0) {
            tmxFile.tileWidth = size.x;
            tmxFile.tileHeight = size.y;
            tmxFile.hexSideLength = size.z;
        }
        // tmxFile.staggerAxis = null;
        // tmxFile.staggerIndex = null;
        // tmxFile.backgroundColor = null;
        // tmxFile.nextObjectID = 0;
        tmxFile.tileSets = GetTileSets(bounds, tmxFilePath);
        globalLayerID = 0;
        tmxFile.layers = CreateLayers(grid.transform);
        
        tmxFile.Save(tmxFilePath);
        AssetDatabase.ImportAsset(FileUtil.GetProjectRelativePath(tmxFilePath));
    }

    List<Tiled.Layer> CreateLayers (Transform root) {
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
        Tilemap tilemap = t.GetComponent<Tilemap>();
        Tiled.TileLayer layer = new Tiled.TileLayer();
        BoundsInt bounds = tilemap.cellBounds;
        layer.tileData = new Tiled.Data();
        if (tmxFile.infinite) {
            Tiled.Chunk chunk = new Tiled.Chunk();
            chunk.width = 16;
            chunk.height = 16;
            if (tmxFile.editorSettingsSpecified && tmxFile.editorSettings.chunkSizeSpecified) {
                chunk.width = tmxFile.editorSettings.chunkSize.width;
                chunk.height = tmxFile.editorSettings.chunkSize.height;
            }
            chunk.contentData = new uint[chunk.width * chunk.height];
            layer.tileData.chunks = new Tiled.Chunk[]{chunk};
        }
        else {
            layer.width = bounds.size.x;
            layer.height = bounds.size.y;
            layer.tileData.contentData = new uint[layer.width * layer.height];
        }

        //TODO: iterate over chunks when infinite
        TileBase[] tiles = tilemap.GetTilesBlock(bounds);
        for (int j = 0; j < bounds.size.y; j++) {
            for (int i = 0; i < bounds.size.x; i++) {
                TileBase tile = tiles[j*bounds.size.x+i];
                int id = tile ? tileGIDs[tile] : 0;

                int x = i+bounds.x;
                int y = tmxFile.height-1-(j+bounds.y);
                layer.SetTileID(id, x, y);//TODO: export hex and iso maps
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
            // if (PrefabUtility.IsPartOfPrefabInstance(t)) {
                
            // }
            // else {
                objects.Add(GetTileObject(t, layer));
            // }
        }
        layer.objects = objects.ToArray();
        return layer;
    }

    public Tiled.TileObject GetTileObject (Transform t, Tiled.ObjectGroup group) {
        Tiled.TileObject tileObject = new Tiled.TileObject();
        tileObject.name = t.name;
        
        SpriteRenderer sprite = t.GetComponent<SpriteRenderer>();
        if (sprite != null && sprite.sprite != null) {
            string spritePath = AssetDatabase.GetAssetPath(sprite.sprite);
            if (tileSetCache.ContainsKey(spritePath)) {
                Tiled.TileSet tileSet = tileSetCache[spritePath];
                string[] splitName = sprite.sprite.name.Split('_');
                int gid = tileSet.firstGID + int.Parse(splitName[splitName.Length-1]);
                tileObject.gid = (uint)gid;
                // index = sprite.sortingOrder
                // calc group opacity from each sprite.color
                tileObject.width = t.localScale.x * sprite.sprite.rect.width;
                tileObject.height = t.localScale.y * sprite.sprite.rect.height;
            }
            else {
                Debug.LogWarning("Sprites not in a TSX file not supported yet. " + spritePath);
            }
        }

        Vector3 center = t.right * tileObject.width - t.up * tileObject.height;
        center *= 0.5f / pixelsPerUnit;
        center += t.position;

        Vector3 rot = t.transform.localEulerAngles;
        tileObject.rotation = rot.z;
        if (Mathf.Abs(rot.x) == 180) tileObject.gid |= Tiled.TMXFile.FlippedVerticallyFlag;
        if (Mathf.Abs(rot.y) == 180) tileObject.gid |= Tiled.TMXFile.FlippedHorizontallyFlag;

        //TODO: reverse engineer position from rotations

        tileObject.y = tmxFile.height * tmxFile.tileHeight - t.localPosition.y * pixelsPerUnit;
        tileObject.x = t.localPosition.x * pixelsPerUnit;

        tileObject.properties = GetProperties(t.gameObject);
        return tileObject;
    }

    Tiled.Property[] GetProperties(GameObject g) {
        Debug.Log("TODO: get props");
        return null;
    }

    Tiled.ImageLayer CreateImageLayer (Transform t) {
        Tiled.ImageLayer layer = new Tiled.ImageLayer();
        // get sprite
        return layer;
    }

    Tiled.GroupLayer CreateGroupLayer (Transform t) {
        Tiled.GroupLayer layer = new Tiled.GroupLayer();
        layer.layers = CreateLayers(t);
        return layer;
    }

    Tiled.TileSet[] GetTileSets(BoundsInt bounds, string tmxFilePath) {
        string dir = Path.GetDirectoryName(tmxFilePath);
        List<Tiled.TileSet> tileSets = new List<Tiled.TileSet>();
        string[] paths = GetTileSetPaths(bounds, grid.transform);
        int firstGID = 1;
        foreach (string path in paths) {
            if (Path.GetExtension(path) != ".tsx") {
                Debug.LogWarning(path + " is not a Tile Set (.tsx).");
                continue;
            }
            
            Uri tsxFileURI = new Uri(Path.GetFullPath(path));
            Uri tmxFileURI = new Uri(Path.GetDirectoryName(Path.GetFullPath(tmxFilePath)));
            Uri relativeURI = tmxFileURI.MakeRelativeUri(tsxFileURI);
            Tiled.TileSet tileSet = Tiled.TileSet.Load(path);
            tileSet.firstGID = firstGID;
            firstGID += tileSet.tileCount;
            tileSet.source = "../" + Uri.UnescapeDataString(relativeURI.ToString());
            tileSets.Add(tileSet);
            tileSetCache[path] = tileSet;
            GetTileAssetsAtPath(tileSet, path);
        }
        return tileSets.ToArray();
    }

    string[] GetTileSetPaths(BoundsInt bounds, Transform root) {
        List<string> tileSetPaths = new List<string>();
        for (int i = 0; i < root.childCount; i++) {
            Transform t = root.GetChild(i);
            SpriteRenderer sprite = t.GetComponent<SpriteRenderer>();
            Tilemap tilemap = t.GetComponent<Tilemap>();
            if (tilemap != null) {
                TileBase[] tiles = tilemap.GetTilesBlock(bounds);
                List<string> paths = new List<string>();
                foreach (TileBase tile in tiles) {
                    string path = AssetDatabase.GetAssetPath(tile);
                    if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path);
                }
                foreach (string path in paths) {
                    GridPalette gridPalette = AssetDatabase.LoadAssetAtPath(path, typeof(GridPalette)) as GridPalette;
                    if (gridPalette != null && !tileSetPaths.Contains(path)) tileSetPaths.Add(path);
                }
            }
            if (t.childCount > 0) {
                string[] paths = GetTileSetPaths(bounds, t);
                foreach (string path in paths) {
                    if (!tileSetPaths.Contains(path)) tileSetPaths.Add(path);
                }
            }
            else if (sprite != null && sprite.sprite != null) {
                string path = AssetDatabase.GetAssetPath(sprite.sprite);
                GridPalette gridPalette = AssetDatabase.LoadAssetAtPath(path, typeof(GridPalette)) as GridPalette;
                if (gridPalette != null && !tileSetPaths.Contains(path)) tileSetPaths.Add(path);
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
        if (renderer == null) return "right-down";
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
        if (tilemaps.Length == 0) return new BoundsInt();
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
        if (tilemap == null) return Vector3Int.zero;
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