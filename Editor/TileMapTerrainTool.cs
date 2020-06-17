using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace Tiled {
[EditorTool("Draw Terrain Tiles", typeof(TileMap))]
class TileMapTerrainTool : TileMapPaintTool {

    static GUIContent terrainIcon;
    public override GUIContent toolbarIcon {
        get {
            if (terrainIcon == null) {
                terrainIcon = EditorGUIUtility.IconContent("TerrainInspector.TerrainToolSculpt");
                terrainIcon.tooltip = "Draw terrain.";
            }
            return terrainIcon;
        }
    }

    public override void ActiveToolDidChange() {
        if (!EditorTools.IsActiveTool(this)) return;

        TileMapEditor.paintType = 1;
    }
}
}