using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace Tiled {
[EditorTool("Paint Tiles", typeof(TileMap))]
class TileMapPaintTool : EditorTool {

    private TileMap tileMap {
        get { return (target as TileMap); }
    }

    private TMXFile tmxFile {
        get { return tileMap.tmxFile; }
        set { tileMap.tmxFile = value; }
    }

    private string path {
        get { return tileMap.tmxFilePath; }
        set { tileMap.tmxFilePath = value; }
    }

    private Terrain[] _terrains;
    public Terrain[] terrains {
        get {
            if (_terrains == null || _terrains.Length == 0) {
                List<Terrain> terrainList = new List<Terrain>();
                foreach (TileSet tileSet in tmxFile.tileSets) {
                    if (tileSet.terrainTypes == null) continue;
                    foreach (Terrain terrain in tileSet.terrainTypes) {
                        terrainList.Add(terrain);
                    }
                }
                _terrains = terrainList.ToArray();
            }
            return _terrains;
        }
    }

    public Terrain selectedTerrain {
        get {
            if (TileMapEditor.selectedTerrainIndex >= 0 
            && TileMapEditor.selectedTerrainIndex < terrains.Length) {
                return terrains[TileMapEditor.selectedTerrainIndex];
            }
            return null;
        }
    }

    static GUIContent paintIcon;
    public override GUIContent toolbarIcon {
        get {
            if (paintIcon == null) {
                paintIcon = EditorGUIUtility.IconContent("Grid.PaintTool");
                paintIcon.tooltip = "Draw tiles.";
            }
            return paintIcon;
        }
    }

    void OnEnable() {
        // Debug.Log("PaintTool.OnEnable");
        EditorTools.activeToolChanged += ActiveToolDidChange;
    }

    void OnDisable() {
        EditorTools.activeToolChanged -= ActiveToolDidChange;
    }

    public virtual void ActiveToolDidChange() {
        if (!EditorTools.IsActiveTool(this)) return;
    }

    public override bool IsAvailable() {
        return TileMapEditor.selectedLayer is TileLayer;
    }

    public override void OnToolGUI(EditorWindow window) {
        if (!(TileMapEditor.selectedLayer is TileLayer)) return;
        Event e = Event.current;
        if (e == null) return;

        Undo.RecordObject(target, "Draw/Erase Tiles");

        if (e.type == EventType.MouseDown) {
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
            DrawTile(false);
        }
        else if (e.type == EventType.MouseDrag) {
            DrawTile(true);
        }
        else if (e.type == EventType.MouseUp) {
            TileLayer tileLayer = TileMapEditor.selectedLayer as TileLayer;
            tileLayer.Encode();
            tileMap.tmxFileString = tmxFile.Save();
            tileMap.UpdatePolygonColliders(tileLayer);

            GUIUtility.hotControl = 0;
            // Undo.FlushUndoRecordObjects();
        }
    }

    public virtual int GetTileIndex () {
        int tileIndex = TileMapEditor.selectedTileIndex;
        if (TileMapEditor.selectedTileSet == null) { 
            TileMapEditor.selectedTileSet = tmxFile.tileSets[0];
            tileIndex = TileMapEditor.selectedTileSet.firstGID;
        }
        return tileIndex;
    }

    Vector3 lastTilePos;
    Vector3 tilePos;
    void DrawTile (bool drag) {
        int tileIndex = GetTileIndex();

        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        float dist = 0;
        Plane plane = new Plane(Vector3.forward, tileMap.transform.position);
        if (plane.Raycast(ray, out dist)) {
            Vector3 p = ray.GetPoint(dist) - tileMap.transform.position;
            lastTilePos = drag ? tilePos : p;
            tilePos = p;
            TileLayer tileLayer = TileMapEditor.selectedLayer as TileLayer;
            switch (TileMapEditor.paintType) {
                case 0:
                    if ((!drag && tileMap.SetTile(tileIndex, tileLayer, tilePos)) || 
                        (drag && tileMap.SetTiles(tileIndex, tileLayer, lastTilePos, tilePos))) {
                        Event.current.Use();
                        EditorUtility.SetDirty(target);
                    }
                    break;
                case 1:
                    if (selectedTerrain != null && tileMap.SetTerrain(selectedTerrain.tile, tileLayer, tilePos)) {
                        Event.current.Use();
                        EditorUtility.SetDirty(target);
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
}