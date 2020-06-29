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

using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;

namespace Tiled {
class TSXFileUtils {
    private static Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    public static Texture2D GetTileSetTexture (TileSet tileSet, string path) {
        if (tileSet.image == null || tileSet.image.source == null) return null;

        string texturePath = tileSet.image.source;
        if (tileSet.source == null) {
            texturePath = Path.Combine(Path.GetDirectoryName(path), texturePath);
        }
        else {
            string tileSetPath = Path.Combine(Path.GetDirectoryName(path), tileSet.source);
            texturePath = Path.Combine(Path.GetDirectoryName(tileSetPath), texturePath);
        }
        return GetImageTexture(tileSet.image, texturePath);
    }

    public static Texture2D GetImageTexture (Image image, string texturePath) {
        if (tileSetTextures.ContainsKey(image.source)) return tileSetTextures[image.source];

        texturePath = Path.GetFullPath(texturePath);
        string dataPath = Path.GetFullPath(Application.dataPath);
        texturePath = texturePath.Replace(dataPath, "Assets");
        Texture2D tex = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;

        if (tex != null) tileSetTextures[image.source] = tex;

        return tex;
    }
}
}
