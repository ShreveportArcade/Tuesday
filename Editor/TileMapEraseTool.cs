using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace Tiled {
[EditorTool("Erase Tiles", typeof(TileMap))]
class TileMapEraseTool : EditorTool {

    static GUIContent _toolbarIcon;
    public override GUIContent toolbarIcon {
        get {
            if (_toolbarIcon == null) {
                _toolbarIcon = EditorGUIUtility.IconContent("Grid.EraserTool");
                _toolbarIcon.tooltip = "Erase tiles.";
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