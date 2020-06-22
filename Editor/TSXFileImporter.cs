using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.IO;

[ScriptedImporter(1, "tsx")]
public class TSXFileImporter : ScriptedImporter {

    public int pixelsPerUnit = -1;

    private static Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    public static Texture2D GetImageTexture (Tiled.Image image, string texturePath) {
        if (tileSetTextures.ContainsKey(image.source)) return tileSetTextures[image.source];

        texturePath = Path.GetFullPath(texturePath);
        string dataPath = Path.GetFullPath(Application.dataPath);
        texturePath = texturePath.Replace(dataPath, "Assets");
        Texture2D tex = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;

        if (tex != null) tileSetTextures[image.source] = tex;

        return tex;
    }

    public override void OnImportAsset(AssetImportContext ctx) {
        string name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        Tiled.TileSet tsxFile = Tiled.TileSet.Load(ctx.assetPath);
        if (pixelsPerUnit < 0) pixelsPerUnit = tsxFile.tileHeight;

        GameObject gameObject = new GameObject(name);
        Grid grid = gameObject.AddComponent<Grid>();
        Tilemap tileMap = gameObject.AddComponent<Tilemap>();
        tileMap.tileAnchor = Vector3.zero;
        gameObject.AddComponent<TilemapRenderer>();
        ctx.AddObjectToAsset(name + ".tmx", gameObject);
        ctx.SetMainObject(gameObject);

        GridPalette gridPal = ScriptableObject.CreateInstance<GridPalette>();
        gridPal.cellSizing = GridPalette.CellSizing.Manual;
        gridPal.name = "Palette Settings";
        ctx.AddObjectToAsset("Palette Settings", gridPal);

        int rows = tsxFile.rows;
        int columns = tsxFile.columns;
        if (tsxFile.image != null && !string.IsNullOrEmpty(tsxFile.image.source)) {
            string texturePath = Path.Combine(Path.GetDirectoryName(ctx.assetPath), tsxFile.image.source);
            Texture2D tex = GetImageTexture(tsxFile.image, texturePath);
            for (int y = 0; y < rows; y++) {
                for (int x = 0; x < columns; x++) {
                    int id = y * columns + x;
                    int gid = id + tsxFile.firstGID;
                    Tiled.Tile tiledTile = tsxFile.GetTile(gid);

                    Tiled.TileRect r = tsxFile.GetTileSpriteRect(gid);
                    Rect rect = new Rect(r.x, r.y, r.width, r.height);
                    Sprite sprite = Sprite.Create(tex, rect, Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = name + "_" + x + "," + y;
                    ctx.AddObjectToAsset(sprite.name,  sprite);

                    Tile unityTile = ScriptableObject.CreateInstance<Tile>();
                    unityTile.name = name + "_" + id;
                    unityTile.sprite = sprite;
                    ctx.AddObjectToAsset(unityTile.name, unityTile);
                    tileMap.SetTile(new Vector3Int(x,rows-1-y,0), unityTile); 
                }
            }
        }
    }
}