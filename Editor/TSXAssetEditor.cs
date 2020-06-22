/*
Copyright (C) 2018 Nolan Baker

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

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;

namespace Tiled {
[CustomEditor(typeof(TSXFileImporter))]
class TSXAssetEditor : Editor {

    TSXFileImporter importer {
        get { return target as TSXFileImporter; }
    }

    string path;
    TileSet _tsxFile;
    TileSet tsxFile {
        get { 
            if (_tsxFile == null) {
                path = AssetDatabase.GetAssetPath(importer);
                _tsxFile = TileSet.Load(path);
            }
            return _tsxFile;
        }
    }

    int spacing = -1;
    int margin = -1;
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        if (spacing < 0 || margin < 0) {
            spacing = tsxFile.spacing;
            margin = tsxFile.margin;
        }

        bool padChange = (spacing != tsxFile.spacing || margin != tsxFile.margin);
        GUI.backgroundColor = padChange ? Color.red : Color.white;
        spacing = Mathf.Clamp(EditorGUILayout.IntField("Spacing", spacing), 0, 8);
        margin = Mathf.Clamp(EditorGUILayout.IntField("Margin", margin), 0, 8);
        GUI.backgroundColor = Color.white;
        GUI.enabled = padChange;
        if (GUILayout.Button("Update Texture Padding")) UpdateTexturePadding();        
    }

    void UpdateTexturePadding () {
        TileSet ts = tsxFile;
        Texture2D texture = TileMapEditor.GetTileSetTexture(ts, path);
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
}
}