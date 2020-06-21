using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using System.IO;
using Tiled;

[ScriptedImporter(1, "tmx")]
public class TMXFileImporter : ScriptedImporter {

    public override void OnImportAsset(AssetImportContext ctx) {
        TMXFile tmxFile = TMXFile.Load(ctx.assetPath);
        GameObject tileMap = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
        Grid grid = tileMap.AddComponent<Grid>();
        ctx.AddObjectToAsset(Path.GetFileName(ctx.assetPath), tileMap);
        ctx.SetMainObject(tileMap);
    }
}