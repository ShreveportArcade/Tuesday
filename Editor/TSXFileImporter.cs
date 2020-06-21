using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor.Experimental.AssetImporters;
using System.IO;

[ScriptedImporter(1, "tsx")]
public class TSXFileImporter : ScriptedImporter {

    public override void OnImportAsset(AssetImportContext ctx) {
        string name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        Tiled.TileSet tsxFile = Tiled.TileSet.Load(ctx.assetPath);
        for (int i = 0; i < tsxFile.tiles.Length; i++) {
            Tiled.Tile tiledTile = tsxFile.tiles[i];
            Tile unityTile = ScriptableObject.CreateInstance<Tile>();
            unityTile.name = name + tiledTile.id;
            ctx.AddObjectToAsset(unityTile.name, unityTile);
        }

        // ctx.SetMainObject(tileMap);
    }
}