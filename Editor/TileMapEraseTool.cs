using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace Tiled {
[EditorTool("Erase Tiles", typeof(TileMap))]
class TileMapEraseTool : TileMapPaintTool {

    static GUIContent eraseIcon;
    public override GUIContent toolbarIcon {
        get {
            if (eraseIcon == null) {
                eraseIcon = EditorGUIUtility.IconContent("Grid.EraserTool");
                eraseIcon.tooltip = "Erase tiles.";
            }
            return eraseIcon;
        }
    }

    public override int GetTileIndex() {
        return TileMapEditor.selectedTileSet.firstGID - 1;
    }

    public override void OnToolGUI(EditorWindow window) {
        base.OnToolGUI(window);
    }
}
}