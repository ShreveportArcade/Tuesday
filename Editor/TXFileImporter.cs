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

[ScriptedImporter(1, "tx", 2)]
[CanEditMultipleObjects]
public class TXFileImporter : ScriptedImporter {

    public GameObject prefab;
    public Tiled.Template template;
    string txFilePath;
    public override void OnImportAsset(AssetImportContext ctx) {
        txFilePath = ctx.assetPath;
        template = Tiled.Template.Load(ctx.assetPath);
        Tiled.TileObject tileObject = template.tileObject;
        GameObject g = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));

        if (template.tileSet != null && template.tileSet.hasSource) {
            string path = template.tileSet.source;
            path = Path.Combine(Path.GetDirectoryName(ctx.assetPath), path);
            Tile tile = GetTileAtPath(tileObject.id, path);
            if (tile != null) {
                SpriteRenderer sprite = g.AddComponent<SpriteRenderer>();
                sprite.sprite = tile.sprite;
                g.transform.localScale = new Vector3(
                    tileObject.width / sprite.sprite.rect.width, 
                    tileObject.height / sprite.sprite.rect.height, 
                    1
                );
            }
        }

        TMXFileUtils.SetProperties(g, tileObject.properties);
        ctx.AddObjectToAsset(Path.GetFileName(ctx.assetPath), g);
        ctx.SetMainObject(g);
    }

    public override bool SupportsRemappedAssetType (System.Type type) {
        return (type == typeof(GameObject));
    }

    Tile GetTileAtPath (int id, string path) {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        foreach (UnityEngine.Object asset in assets) {
            if (asset is Tile) {
                Tile tile = asset as Tile;
                string[] splitName = tile.name.Split('_');
                if (id == int.Parse(splitName[splitName.Length-1])) return tile;
            }
        }
        Debug.LogWarning("Tile " + id + " not found at " + path);
        return null;
    }
}