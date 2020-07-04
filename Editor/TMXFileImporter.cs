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

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

[ScriptedImporter(1, "tmx", 3)]
[CanEditMultipleObjects]
public class TMXFileImporter : ScriptedImporter {

    public int pixelsPerUnit = -1;
    public Tiled.TMXFile tmxFile;

    static Texture2D _icon;
    static Texture2D icon { 
        get { 
            if (_icon == null) _icon = EditorGUIUtility.IconContent("Tilemap Icon").image as Texture2D;
            return _icon;
        }
    }  

    Dictionary<int, Tile> _tiles;
    Dictionary<int, Tile> tiles {
        get {
            if (_tiles == null) {
                _tiles = new Dictionary<int, Tile>();
                foreach (Tiled.TileSet tileSet in tmxFile.tileSets) {
                    if (tileSet.hasSource) {
                        string tsxPath = tileSet.source;
                        tsxPath = Path.Combine(Path.GetDirectoryName(tmxFilePath), tsxPath);
                        tsxPath = Path.GetFullPath(tsxPath);
                        GetTileAssetsAtPath(tileSet, FileUtil.GetProjectRelativePath(tsxPath));
                    }
                    else {
                        string dir = Path.GetFullPath(Path.GetDirectoryName(tmxFilePath));
                        string name = Path.GetFileNameWithoutExtension(tmxFilePath);
                        string tsxPath = Path.Combine(dir, name + "." + tileSet.name + ".tsx");
                        GetTileAssetsAtPath(tileSet, FileUtil.GetProjectRelativePath(tsxPath));
                    }
                }
            }
            return _tiles;
        }
    }

    string tmxFilePath;
    int layerIndex;
    public override void OnImportAsset(AssetImportContext ctx) {
        tmxFilePath = ctx.assetPath;
        tmxFile = Tiled.TMXFile.Load(ctx.assetPath);
        if (pixelsPerUnit < 0) pixelsPerUnit = tmxFile.tileHeight;
        GameObject gameObject = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));

        Grid grid = gameObject.AddComponent<Grid>();
        grid.cellLayout = GetCellLayout(tmxFile);
        grid.cellSize = GetCellSize();
        grid.cellSwizzle = GetCellSwizzle(tmxFile);

        string dir = Path.GetFullPath(Path.GetDirectoryName(tmxFilePath));
        bool hasEmbeddedTiles = false;
        foreach (Tiled.TileSet tileSet in tmxFile.tileSets) {
            if (tileSet.sourceSpecified) continue;
            hasEmbeddedTiles = true;
            string name = Path.GetFileNameWithoutExtension(tmxFilePath);
            string path = Path.Combine(dir, name + "." + tileSet.name + ".tsx");
            tileSet.Save(path);
            path = FileUtil.GetProjectRelativePath(path);
            AssetDatabase.ImportAsset(path);
        }

        layerIndex = 0;
        CreateLayers(ctx, tmxFile.layers, gameObject.transform);
        ctx.AddObjectToAsset(Path.GetFileName(ctx.assetPath), gameObject, icon);
        ctx.SetMainObject(gameObject);

        if (hasEmbeddedTiles) AssetDatabase.ImportAsset(tmxFilePath);
    }

    public override bool SupportsRemappedAssetType (System.Type type) {
        return (type == typeof(GridPalette));
    }

    public static GridLayout.CellLayout GetCellLayout (Tiled.TMXFile tmxFile) {
        if (tmxFile.orientation == "hexagonal") return GridLayout.CellLayout.Hexagon;
        else if (tmxFile.orientation == "isometric") return GridLayout.CellLayout.Isometric;
        else if (tmxFile.orientation == "staggered") return GridLayout.CellLayout.IsometricZAsY;
        return GridLayout.CellLayout.Rectangle;
    }

    Vector3 GetCellSize() {
        Vector3 size = new Vector3(tmxFile.tileWidth,tmxFile.tileHeight,0) / pixelsPerUnit;
        if (tmxFile.orientation == "hexagonal") {
            if (tmxFile.hexSideLength > 0) {
                size.x = (float)tmxFile.tileWidth/(float)(tmxFile.tileHeight+tmxFile.hexSideLength);
                size.y = 1-(float)tmxFile.hexSideLength/(float)(tmxFile.hexSideLength+tmxFile.tileHeight);
            }
        }
        return size;
    }

    public static GridLayout.CellSwizzle GetCellSwizzle(Tiled.TMXFile tmxFile) {
        if (tmxFile.orientation == "hexagonal" && tmxFile.staggerAxis == "x") {
            return GridLayout.CellSwizzle.YXZ;
        }
        return GridLayout.CellSwizzle.XYZ;
    }

    void CreateLayers (AssetImportContext ctx, List<Tiled.Layer> layers, Transform parent) {
        Dictionary<string, int> names = new Dictionary<string, int>();
        foreach (Tiled.Layer layerData in layers) {
            string name = layerData.name;
            if (name == null) name = "";
            if (!names.ContainsKey(name)) names[name] = 0;
            else name += (++names[name]).ToString();

            GameObject layer = new GameObject(name);
            if (layerData is Tiled.TileLayer) CreateTileLayer(layerData as Tiled.TileLayer, layer);
            else if (layerData is Tiled.ObjectGroup) CreateObjectGroup(layerData as Tiled.ObjectGroup, layer);
            else if (layerData is Tiled.GroupLayer) CreateLayers(ctx, (layerData as Tiled.GroupLayer).layers, layer.transform);
            TMXFileUtils.SetProperties(layer, layerData.properties);
            
            layer.transform.SetParent(parent);
            layer.transform.localPosition = new Vector3(layerData.offsetX, -layerData.offsetY, 0) / pixelsPerUnit;
            layer.SetActive(layerData.visible);
            layerIndex++;
            ctx.AddObjectToAsset(name, layer);
        }
    }

    void GetTileAssetsAtPath (Tiled.TileSet tileSet, string path) {
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        foreach (Object asset in assets) {
            if (asset is Tile) {
                Tile tile = asset as Tile;
                string[] splitName = tile.name.Split('_');
                int gid = tileSet.firstGID + int.Parse(splitName[splitName.Length-1]);
                _tiles[gid] = tile;
            }
        }
    }

    TilemapRenderer.SortOrder GetSortOrder() {
        if (tmxFile.orientation == "isometric" || tmxFile.orientation == "staggered") {
            if (tmxFile.renderOrder == "left-up") return TilemapRenderer.SortOrder.BottomLeft;
            else if (tmxFile.renderOrder == "right-down") return TilemapRenderer.SortOrder.TopRight;
            else if (tmxFile.renderOrder == "right-up") return TilemapRenderer.SortOrder.BottomRight;
        }
        else {
            if (tmxFile.renderOrder == "right-up") return TilemapRenderer.SortOrder.BottomLeft;
            else if (tmxFile.renderOrder == "left-down") return TilemapRenderer.SortOrder.TopRight;
            else if (tmxFile.renderOrder == "left-up") return TilemapRenderer.SortOrder.BottomRight;
        }
        return TilemapRenderer.SortOrder.TopLeft;
    }

    GameObject temp;
    public void CreateTileLayer (Tiled.TileLayer layerData, GameObject layer) {
        temp = new GameObject("TEMP");

        Tilemap tilemap = layer.AddComponent<Tilemap>();
        tilemap.tileAnchor = new Vector3(0,0,0);
        if (tmxFile.orientation == "hexagonal") {
            tilemap.tileAnchor = new Vector3(
                -(float)tmxFile.hexSideLength/(float)tmxFile.tileHeight,
                -(float)tmxFile.tileWidth/(float)(tmxFile.tileHeight+tmxFile.hexSideLength),
                0
            );
        }

        TilemapRenderer renderer = layer.AddComponent<TilemapRenderer>();
        renderer.sortOrder = GetSortOrder();
        renderer.sortingOrder = layerIndex;
        Color c = TMXFileUtils.TiledColorFromString(layerData.tintColor);
        c.a *= layerData.opacity;
        tilemap.color = c;

        if (tmxFile.infinite && layerData.tileData.chunks != null) {
            int right = int.MinValue;
            foreach (Tiled.Chunk chunk in layerData.tileData.chunks) {
                if (chunk.x + chunk.width > right) right = chunk.x + chunk.width;
            }
            int top = tmxFile.height;
            right += tmxFile.width;

            foreach (Tiled.Chunk chunk in layerData.tileData.chunks) {
                for (int y = 0; y < chunk.height; y++) {
                    for (int x = 0; x < chunk.width; x++) {
                        SetTile(layerData, tilemap, x + chunk.x, y + chunk.y, right, top);
                    }
                }
            }
        }
        else {
            for (int y = 0; y < tmxFile.height; y++) {
                for (int x = 0; x < tmxFile.width; x++) {
                    SetTile(layerData, tilemap, x, y, tmxFile.width, tmxFile.height);
                }
            }
        }
        GameObject.DestroyImmediate(temp);
        temp = null;
    }

    public void SetTile (Tiled.TileLayer layerData, Tilemap tilemap, int x, int y, int right, int top) {
        Vector3 size = GetCellSize();
        float h = size.magnitude * 0.5f;
        bool staggerX = tmxFile.staggerAxis == "x";
        int staggerIndex = (tmxFile.staggerAxis == "even") ? 0 : 1;
        
        int tileID = layerData.GetTileID(x, y);
        if (tileID == 0 || !tiles.ContainsKey(tileID)) return;
        Tile tile = tiles[tileID];
        Vector3Int pos = new Vector3Int(x, top-1-y, 0);
        if (tmxFile.orientation == "isometric") {
            pos = new Vector3Int(top-1-y, right-1-x, 0);
        }
        else if (tmxFile.orientation == "staggered") {
            if (staggerX) {
                pos.x -= y;
                if (x % 2 == staggerIndex) pos.x++;
            }
            else {
                pos.y = (top-1-y)/2-x;
                pos.x = 2*x+pos.y;
                if (y % 2 == staggerIndex) pos.y--;
            }
        }
        else if (tmxFile.orientation == "hexagonal") {
            if (staggerX) {//x % 2 != staggerIndex) {
                pos = new Vector3Int(top-1-y, x, 0);
                if (x % 2 != staggerIndex) pos.x++;
            }
            else if (y % 2 != staggerIndex) pos.x--;
        }
        tilemap.SetTile(pos, tile);

        Transform t = temp.transform;
        t.position = Vector3.zero;
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one;

        bool flipAntiDiag = layerData.FlippedAntiDiagonally(x,y);
        bool rotated120 = layerData.RotatedHexagonal120(x,y);
        Vector3 center = size * 0.5f;
        if (layerData.FlippedHorizontally(x,y)) {
            t.RotateAround(center, Vector3.up, 180); 
        }
        
        if (layerData.FlippedVertically(x,y)) {
            t.RotateAround(center, Vector3.right, 180); 
        }

        if (layerData.FlippedAntiDiagonally(x,y) && tmxFile.orientation != "hexagonal") {
            t.RotateAround(center, Vector2.one, 180); 
        }

        if (rotated120 || (flipAntiDiag && tmxFile.orientation == "hexagonal")) {
            float angle = rotated120 ? 120 : 0;
            angle += flipAntiDiag ? 60 : 0;
            t.RotateAround(center, Vector3.forward, -angle);
        }

        tilemap.SetTransformMatrix(pos, t.localToWorldMatrix);
    }

    public void CreateObjectGroup (Tiled.ObjectGroup groupData, GameObject layer) {
        if (groupData.objects == null) return;
        bool isIndexOrdered = (groupData.drawOrder == "index");

        if (!isIndexOrdered) {
            SortingGroup sortingGroup = layer.AddComponent<SortingGroup>();
            sortingGroup.sortingOrder = layerIndex;
        }

        float w = tmxFile.tileWidth / pixelsPerUnit;
        float h = tmxFile.tileHeight / pixelsPerUnit;
        Dictionary<string, int> names = new Dictionary<string, int>();
        for (int i = 0; i < groupData.objects.Length; i++) {
            Tiled.TileObject tileObject = groupData.objects[i];
            string name = tileObject.name;
            if (name == null) name = "";
            if (!names.ContainsKey(name)) names[name] = 0;
            else name += (++names[name]).ToString();
            
            int index = isIndexOrdered ? layerIndex++ : (int)(tileObject.y * 10);
            CreateTileObject(name, tileObject, layer, groupData.opacity, index);
        }
    }

    public void CreateTileObject (string name, Tiled.TileObject tileObject, GameObject layer, float opacity, int index) {
        int tileID = tileObject.tileID;
        GameObject g = null;
        if (tileObject.templateSpecified) {
            string templatePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tmxFilePath), tileObject.template));
            Tiled.Template template = Tiled.Template.Load(templatePath);

            string dataPath = Path.GetFullPath(Application.dataPath);
            templatePath = templatePath.Replace(dataPath, "Assets");

            AssetImporter importer = AssetImporter.GetAtPath(templatePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath(templatePath, typeof(GameObject)) as GameObject;
            AssetImporter.SourceAssetIdentifier id = new AssetImporter.SourceAssetIdentifier(prefab);
            Dictionary<SourceAssetIdentifier,Object> external = importer.GetExternalObjectMap(); 
            if (external.ContainsKey(id)) prefab = external[id] as GameObject;
            
            g = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            g.name = name;
            TMXFileUtils.SetProperties(g, template.tileObject.properties);
        }
        else {
            g = new GameObject(name);
            g.AddComponent<SpriteRenderer>();
        }
        g.transform.SetParent(layer.transform);
        float y = tmxFile.height * tmxFile.tileHeight - tileObject.y;
        g.transform.localPosition = new Vector3(tileObject.x, y, 0) / pixelsPerUnit;

        SpriteRenderer sprite = g.GetComponent<SpriteRenderer>();
        sprite.sortingOrder = index;
        Color c = Color.white;
        c.a = opacity;
        sprite.color = c;

        Tiled.TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
        if (tiles.ContainsKey(tileID)) {
            sprite.sprite = tiles[tileID].sprite;
            g.transform.localScale = new Vector3(
                tileObject.width / sprite.sprite.rect.width, 
                tileObject.height / sprite.sprite.rect.height, 
                1
            );
        }
        else if (tileID == 0) Debug.LogWarning("TileObjects without Tiles not yet supported");
        else Debug.LogWarning("Tile" + tileID + " not found at " + tmxFilePath);

        Vector3 origin = g.transform.position;
        Vector3 center = origin + new Vector3(tileObject.width, tileObject.height, 0) * 0.5f / (float)pixelsPerUnit;
        if (Tiled.TMXFile.FlippedHorizontally(tileObject.gid)) {
            g.transform.RotateAround(center, Vector3.up, 180); 
        }
        
        if (Tiled.TMXFile.FlippedVertically(tileObject.gid)) {
            g.transform.RotateAround(center, Vector3.right, 180); 
        }

        if (Tiled.TMXFile.FlippedAntiDiagonally(tileObject.gid)) {
            g.transform.RotateAround(center, Vector2.one, 180); 
        }

        if (tileObject.rotation != 0) {
            g.transform.RotateAround(origin, Vector3.forward, -tileObject.rotation);
        }

        TMXFileUtils.SetProperties(g, tileObject.properties);
    }
}