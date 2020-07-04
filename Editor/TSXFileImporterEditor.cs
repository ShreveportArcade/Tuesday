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

[CustomEditor(typeof(TSXFileImporter))]
[CanEditMultipleObjects]
class TSXFileImporterEditor : ScriptedImporterEditor {

    TSXFileImporter importer {
        get { return target as TSXFileImporter; }
    }

    string path;
    Tiled.TileSet _tsxFile;
    Tiled.TileSet tsxFile {
        get { 
            if (_tsxFile == null) {
                path = AssetDatabase.GetAssetPath(importer);
                _tsxFile = Tiled.TileSet.Load(path);
            }
            return _tsxFile;
        }
    }

    int spacing = -1;
    int margin = -1;
    Color transparentColor;
    public override void OnEnable() {
        base.OnEnable();
        spacing = tsxFile.spacing;
        margin = tsxFile.margin;
        if (tsxFile.imageSpecified && tsxFile.image != null && tsxFile.image.transSpecified) {
            transparentColor = TMXFileUtils.TiledColorFromString(tsxFile.image.trans);
        }
    }

    protected override void Apply() {
        base.Apply();
        
        for (int i = 0; i < targets.Length; i++) {
            TSXFileImporter importer = (TSXFileImporter)targets[i];
            if (importer.tileSet == null) continue;
            string path = importer.assetPath;
            importer.tileSet.Save(path);        
        }
    }

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        if (!tsxFile.imageSpecified || tsxFile.image == null) return;

        bool padChange = (spacing != tsxFile.spacing || margin != tsxFile.margin);
        GUI.backgroundColor = padChange ? Color.red : Color.white;
        spacing = Mathf.Clamp(EditorGUILayout.IntField("Spacing", spacing), 0, 8);
        margin = Mathf.Clamp(EditorGUILayout.IntField("Margin", margin), 0, 8);
        GUI.backgroundColor = Color.white;
        GUI.enabled = padChange;
        if (GUILayout.Button("Update Texture Padding")) UpdateTexturePadding();   

        GUI.enabled = true;
        
        bool hasTransparency = EditorGUILayout.Toggle("Set Color to Transparent", tsxFile.image.transSpecified);
        if (!hasTransparency && tsxFile.image.transSpecified) {
            tsxFile.image.trans = null;
        }
        else if (hasTransparency && !tsxFile.image.transSpecified) {
            tsxFile.image.trans = TMXFileUtils.TiledColorToString(transparentColor, true);
        }
        if (!hasTransparency) return;

        transparentColor = EditorGUILayout.ColorField("Transparent Color", transparentColor);
        tsxFile.image.trans = TMXFileUtils.TiledColorToString(transparentColor);
        if (GUILayout.Button("Write Texture Alpha")) WriteTextureAlpha();
    }

    private static Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    public static Texture2D GetTileSetTexture (Tiled.TileSet tileSet, string path) {
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

    public static Texture2D GetImageTexture (Tiled.Image image, string texturePath) {
        if (tileSetTextures.ContainsKey(image.source)) return tileSetTextures[image.source];

        texturePath = Path.GetFullPath(texturePath);
        string dataPath = Path.GetFullPath(Application.dataPath);
        texturePath = texturePath.Replace(dataPath, "Assets");
        Texture2D tex = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;

        if (tex != null) tileSetTextures[image.source] = tex;

        return tex;
    }

    void UpdateTexturePadding () {
        Tiled.TileSet ts = tsxFile;
        Texture2D texture = GetTileSetTexture(ts, path);
        string texturePath = AssetDatabase.GetAssetPath(texture);

        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(texturePath);
        importer.isReadable = true;
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        int inWidth = ts.tileWidth * ts.columns + ts.margin * 2 + ts.spacing * (ts.columns - 1);
        int outWidth = ts.tileWidth * ts.columns + margin * 2 + spacing * (ts.columns - 1);
        int outHeight = ts.tileHeight * ts.rows + margin * 2 + spacing * (ts.rows - 1);

        Color[] inColors = texture.GetPixels();
        Color[] outColors = new Color[outWidth * outHeight];

        for (int column = 0; column < ts.columns; column++) {
            for (int row = 0; row < ts.rows; row++) {
                for (int i = 0; i < ts.tileWidth; i++) {
                    for (int j = 0; j < ts.tileHeight; j++) {
                        int inX = i + (ts.tileWidth + ts.spacing) * column + ts.margin;
                        int inY = j + (ts.tileHeight + ts.spacing) * row + ts.margin;
                        Color c = inColors[inY * inWidth + inX]; 

                        int left = (i == 0) ? ((column==0) ? -margin : -spacing/2) : 0;
                        int right = (i == ts.tileWidth-1) ? ((column==ts.columns-1) ? margin : spacing/2) : 0;
                        int down = (j == 0) ? ((row==0) ? -margin : -spacing/2) : 0;
                        int up = (j == ts.tileHeight-1) ? ((row==ts.rows-1) ? margin : spacing/2) : 0;
                        for (int x = i + left; x <= i + right; x++) {
                            for (int y = j + down; y <= j + up; y++) {
                                int outX = x + (ts.tileWidth + spacing) * column + margin;
                                int outY = y + (ts.tileHeight + spacing) * row + margin;                                
                                outColors[outY * outWidth + outX] = c;
                            }
                        }
                    }
                }
            }
        }

        Texture2D t = new Texture2D(outWidth, outHeight);
        t.SetPixels(outColors);
        byte[] bytes = t.EncodeToPNG();
        File.WriteAllBytes(texturePath, bytes);

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        tsxFile.margin = margin;
        tsxFile.spacing = spacing;
        tsxFile.image.width = outWidth;
        tsxFile.image.height = outHeight;
        tsxFile.Save(path);
    }

    void WriteTextureAlpha () {
        Tiled.TileSet ts = tsxFile;
        Texture2D texture = GetTileSetTexture(ts, path);
        string texturePath = AssetDatabase.GetAssetPath(texture);

        TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(texturePath);
        importer.isReadable = true;
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        Color[] inColors = texture.GetPixels();
        int count = inColors.Length;
        Color[] outColors = new Color[count];

        Color trans = transparentColor;
        for (int i = 0; i < count; i++) {
            Color c = inColors[i];
            if (c.r == trans.r && c.g == trans.g && c.b == trans.b) c.a = 0;
            else c.a = 1;
            outColors[i] = c;
        }

        Texture2D t = new Texture2D(texture.width, texture.height);
        t.SetPixels(outColors);
        byte[] bytes = t.EncodeToPNG();
        File.WriteAllBytes(texturePath, bytes);

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
    }
}
