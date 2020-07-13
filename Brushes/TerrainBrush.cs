using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CustomGridBrush(false, false, false, "Terrain Brush")]
[CreateAssetMenu(fileName = "New Terrain Brush", menuName = "Brushes/Terrain Brush")]
public class TerrainBrush : UnityEditor.Tilemaps.GridBrush {

    Dictionary<int, TileBase> tiles;
    Tiled.TMXFile tmxFile;
    GridLayout _grid;
    GridLayout grid {
        get {
            return _grid;
        }
        set {
            if (_grid == value) return;
            _grid = value;
            if (_grid == null) return;
#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(value);
            tmxFile = Tiled.TMXFile.Load(path);
            tiles = TMXFileUtils.GetTiles(tmxFile, path);
#endif
        }
    }

    public override void Pick(GridLayout grid, GameObject target, BoundsInt bounds, Vector3Int pivot) {
        Tilemap tilemap = target.GetComponent<Tilemap>();
        TileBase[] tiles = tilemap.GetTilesBlock(bounds); 
    }

    public override void Paint(GridLayout grid, GameObject target, Vector3Int pos) {
        this.grid = grid;
        Debug.Log("PAINT: " + tmxFile);
        Tilemap tilemap = target.GetComponent<Tilemap>();
    }

    public Tiled.Tile GetTile (Tilemap tilemap, int x, int y) {
        TileBase unityTile = tilemap.GetTile(new Vector3Int(x,y,0));
        Tiled.Tile tile = null;
#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(unityTile);

#endif
        return tile;
    }

    public void SetTerrain (int tileID, Tilemap tilemap, Vector3Int pos) {
        int x = pos.x;
        int y = pos.x;
        Tiled.TileSet tileSet = tmxFile.GetTileSetByTileID(tileID);
        int gid = tileSet.firstGID;

        Tiled.Tile center = tmxFile.GetTile(tileSet, tmxFile.GetTile(tileSet, tileID+gid).terrain);
        int[] c = center.terrain;
        if (c == null) {
            Debug.LogError(tileID + " is not a terrain tile");
        }

        Tiled.Tile topLeft = GetTile(tilemap, x-1, y-1);
        if (topLeft != null && topLeft.terrain != null) {
            int[] n = topLeft.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], n[1], n[2], c[0]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x-1, y-1, 0), unityTile);
            }
        }

        Tiled.Tile top = GetTile(tilemap, x, y-1);
        if (top != null && top.terrain != null) {
            int[] n = top.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], n[1], c[0], c[1]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x, y-1, 0), unityTile);
            }
        }
        
        Tiled.Tile topRight = GetTile(tilemap, x+1, y-1);
        if (topRight != null && topRight.terrain != null) {
            int[] n = topRight.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], n[1], c[1], n[3]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x+1, y-1, 0), unityTile);
            }
        }
        
        Tiled.Tile right = GetTile(tilemap, x+1, y);
        if (right != null && right.terrain != null) {
            int[] n = right.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{c[1], n[1], c[3], n[3]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x+1, y, 0), unityTile);
            }
        }
        
        Tiled.Tile bottomRight = GetTile(tilemap, x+1, y+1);
        if (bottomRight != null && bottomRight.terrain != null) {
            int[] n = bottomRight.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{c[3], n[1], n[2], n[3]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x+1, y+1, 0), unityTile);
            }
        }
        
        Tiled.Tile bottom = GetTile(tilemap, x, y+1);
        if (bottom != null && bottom.terrain != null) {
            int[] n = bottom.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{c[2], c[3], n[2], n[3]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x, y+1, 0), unityTile);
            }
        }
        
        Tiled.Tile bottomLeft = GetTile(tilemap, x-1, y+1);
        if (bottomLeft != null && bottomLeft.terrain != null) {
            int[] n = bottomLeft.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], c[2], n[2], n[3]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x-1, y+1, 0), unityTile);
            }
        }
        
        Tiled.Tile left = GetTile(tilemap, x-1, y);
        if (left != null && left.terrain != null) {
            int[] n = left.terrain;
            Tiled.Tile tile = tmxFile.GetTile(tileSet, new int[]{n[0], c[0], n[2], c[2]});
            if (tile != null) {
                TileBase unityTile = tiles[gid];
                tilemap.SetTile(new Vector3Int(x-1, y, 0), unityTile);
            }
        }
    }
}