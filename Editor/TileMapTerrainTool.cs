using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace Tiled {
[EditorTool("Draw Terrain Tiles", typeof(TileMap))]
class TileMapTerrainTool : EditorTool {

    static GUIContent _toolbarIcon;
    public override GUIContent toolbarIcon {
        get {
            if (_toolbarIcon == null) {
                _toolbarIcon = EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSculpt");
                _toolbarIcon.tooltip = "Draw terrain.";
            }
            return _toolbarIcon;
        }
    }

    void OnEnable() {
        EditorTools.activeToolChanged += ActiveToolDidChange;
    }

    void OnDisable() {
        EditorTools.activeToolChanged -= ActiveToolDidChange;
    }

    void ActiveToolDidChange() {
        if (!EditorTools.IsActiveTool(this)) return;
    }

    public override void OnToolGUI(EditorWindow window) {
        Event e = Event.current;

        
    }
}
}