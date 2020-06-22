using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using Tiled;

[ScriptedImporter(1, "tmx")]
public class TMXFileImporter : ScriptedImporter {

    public int pixelsPerUnit = -1;

    public static Color TiledColorFromString (string colorStr) {
        Color color = Color.white;
        if (colorStr == null) return color;
        if (colorStr.Length > 8) colorStr = "#" + colorStr.Substring(3) + colorStr.Substring(1, 2);
        ColorUtility.TryParseHtmlString(colorStr, out color); 
        return color;
    }

    public static string TiledColorToString (Color color) {
        if (color == Color.white) return null;
        if (color.a == 1) return "#" + ColorUtility.ToHtmlStringRGB(color).ToLower();
        string colorStr = ColorUtility.ToHtmlStringRGBA(color).ToLower();;
        colorStr = "#" + colorStr.Substring(6, 2) + colorStr.Substring(0, 6);
        return colorStr;
    }

    TMXFile tmxFile;
    string tmxFilePath;
    public override void OnImportAsset(AssetImportContext ctx) {
        tmxFilePath = ctx.assetPath;
        tmxFile = TMXFile.Load(ctx.assetPath);
        if (pixelsPerUnit < 0) pixelsPerUnit = tmxFile.tileHeight;
        GameObject tileMap = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
        Grid grid = tileMap.AddComponent<Grid>();
        CreateLayers(tmxFile.layers, tileMap.transform);
        ctx.AddObjectToAsset(Path.GetFileName(ctx.assetPath), tileMap);
        ctx.SetMainObject(tileMap);
    }

    void CreateLayers (List<Layer> layers, Transform parent) {
        foreach (Layer layerData in layers) {
            GameObject layer = new GameObject(layerData.name);
            // SetProperties(layer, layerData.properties);
            layer.transform.SetParent(parent);
            layer.transform.localPosition = new Vector3(layerData.offsetX, -layerData.offsetY, 0) / pixelsPerUnit;

            if (layerData is TileLayer) CreateTileLayer(layerData as TileLayer, layer);
            else if (layerData is ObjectGroup) CreateObjectGroup(layerData as ObjectGroup, layer);
            else if (layerData is GroupLayer) CreateLayers((layerData as GroupLayer).layers, layer.transform);
        }
    }



    public void CreateTileLayer (TileLayer layerData, GameObject layer) {
        Tilemap tilemap = layer.AddComponent<Tilemap>();
        TilemapRenderer renderer = layer.AddComponent<TilemapRenderer>();  
        tilemap.color = TiledColorFromString(layerData.tintColor);
                   
    }

    public void CreateObjectGroup (ObjectGroup groupData, GameObject group) {
        float w = tmxFile.tileWidth / pixelsPerUnit;
        float h = tmxFile.tileHeight / pixelsPerUnit;
        foreach (TileObject tileObject in groupData.objects) {
            bool isPrefab = tileObject.properties != null && System.Array.Exists(tileObject.properties, (p) => p.name == "prefab");
            if (isPrefab) CreatePrefabTile(group, tileObject);
            else CreateSpriteTile(group, tileObject);
        }
    }

    public void CreatePrefabTile (GameObject group, TileObject tileObject) {
        int tileID = (int)tileObject.gid;
        Property prefabProp = System.Array.Find(tileObject.properties, (p) => p.name == "prefab");

        string prefabPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tmxFilePath), prefabProp.val));
        string dataPath = Path.GetFullPath(Application.dataPath);
        prefabPath = prefabPath.Replace(dataPath, "Assets");
        GameObject prefab = AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject)) as GameObject;
        GameObject g = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        g.transform.SetParent(group.transform);
        float y = tmxFile.height * tmxFile.tileHeight - tileObject.y;
        g.transform.localPosition = new Vector3(tileObject.x, y, 0) / pixelsPerUnit;
        g.transform.localEulerAngles = Vector3.forward * -tileObject.rotation;
        SetProperties(g, tileObject.properties);
        g.SetActive(tileObject.visible);
    }

    public void SetProperties (GameObject g, Property[] props) {
        if (props == null) return;
        foreach (Property prop in props) {
            if (!prop.name.Contains(".")) continue;
            string[] classVar = prop.name.Split('.');
            Object o = g as Object;
            System.Type type = System.Type.GetType(classVar[0]);
            if (classVar[0] == "GameObject") {
                if (classVar[1] == "layer" && !prop.typeSpecified) {
                    g.layer = LayerMask.NameToLayer(prop.val);
                    continue;
                }
            }
            else {
                if (type == null) type = System.Type.GetType("UnityEngine.Rendering."+classVar[0]+",UnityEngine");
                if (type == null) continue;
                Component c = g.GetComponent(type);
                if (c == null) c = g.AddComponent(type);
                o = c as Object;
            }
            FieldInfo fieldInfo = type.GetField(classVar[1], BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null) {
                switch (prop.type) {
                    case "float":
                        fieldInfo.SetValue(o, float.Parse(prop.val));
                        break;
                    case "int":
                        fieldInfo.SetValue(o, int.Parse(prop.val));
                        break;
                    case "bool":
                        fieldInfo.SetValue(o, bool.Parse(prop.val));
                        break;
                    case "color":
                        fieldInfo.SetValue(o, TiledColorFromString(prop.val));
                        break;
                    default:
                        fieldInfo.SetValue(o, prop.val);
                        break;
                }
                continue;
            }
            PropertyInfo propInfo = type.GetProperty(classVar[1], BindingFlags.Public | BindingFlags.Instance);
            if (propInfo != null) {
                switch (prop.type) {
                    case "float":
                        propInfo.SetValue(o, float.Parse(prop.val));
                        break;
                    case "int":
                        propInfo.SetValue(o, int.Parse(prop.val));
                        break;
                    case "bool":
                        propInfo.SetValue(o, bool.Parse(prop.val));
                        break;
                    case "color":
                        propInfo.SetValue(o, TiledColorFromString(prop.val));
                        break;
                    default:
                        propInfo.SetValue(o, prop.val);
                        break;
                }
                continue;
            }
        }
    }

    public void CreateSpriteTile (GameObject group, TileObject tileObject) {
        int tileID = (int)tileObject.gid;
        GameObject g = new GameObject(tileObject.name);
        g.transform.SetParent(group.transform);
        float y = tmxFile.height * tmxFile.tileHeight - tileObject.y;
        g.transform.localPosition = new Vector3(tileObject.x, y, 0) / pixelsPerUnit;
        g.transform.localEulerAngles = Vector3.forward * -tileObject.rotation;

        SpriteRenderer sprite = g.AddComponent<SpriteRenderer>();
        sprite.flipX = TMXFile.FlippedHorizontally(tileObject.gid);
        sprite.flipY = TMXFile.FlippedVertically(tileObject.gid);
        TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
        // if (tileSet != null && tileSet.image != null && !string.IsNullOrEmpty(tileSet.image.source)) {
        //     g.transform.localScale = new Vector3(tileObject.width, tileObject.height, pixelsPerUnit) / pixelsPerUnit;
        //     int tileSetIndex = System.Array.IndexOf(tmxFile.tileSets, tileSet);
        //     Material mat = tileSetMaterials[tileSetIndex];
        //     Texture2D tex = mat.mainTexture as Texture2D;
        //     TileRect r = tileSet.GetTileSpriteRect(tileID);
        //     Rect rect = new Rect(r.x, r.y, r.width, r.height);
        //     Vector2 pivot = Vector2.zero;
        //     sprite.sprite = Sprite.Create(tex, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        // }
        // else {
        //     int tileSetIndex = System.Array.IndexOf(tmxFile.tileSets, tileSet);
        //     sprite.sprite = tileSetSprites[tileSetIndex][tileID-tileSet.firstGID];
        // }

        SetProperties(g, tileObject.properties);
    }

}