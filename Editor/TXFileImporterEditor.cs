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

[CustomEditor(typeof(TXFileImporter))]
[CanEditMultipleObjects]
class TXFileImporterEditor : ScriptedImporterEditor {

    TXFileImporter importer {
        get { return target as TXFileImporter; }
    }

    string path;
    Tiled.Template _txFile;
    Tiled.Template txFile {
        get { 
            if (_txFile == null) {
                path = AssetDatabase.GetAssetPath(importer);
                _txFile = Tiled.Template.Load(path);
            }
            return _txFile;
        }
    }

    protected override void Apply() {
        for (int i = 0; i < targets.Length; i++) {
            TXFileImporter importer = (TXFileImporter)targets[i];
            if (importer.template != null) {
                string path = importer.assetPath;
                importer.template.Save(path);     
            }
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(importer.assetPath);
            AssetImporter.SourceAssetIdentifier id = new AssetImporter.SourceAssetIdentifier(asset);
            if (importer.prefab == null) {
                importer.RemoveRemap(id);
            }
            else {
                importer.AddRemap(id, importer.prefab);
            }
            AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
        }

        base.Apply();
    }
}
