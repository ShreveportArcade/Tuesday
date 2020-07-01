using UnityEngine;
using UnityEngine.Tilemaps;

public class AnimatedTile : Tile {

    public Sprite[] sprites;
    public float[] durations;

    public override bool GetTileAnimationData(Vector3Int pos, ITilemap map, ref TileAnimationData animData) {
        if (sprites == null || sprites.Length == 0) return false;

        animData.animatedSprites = sprites;
        animData.animationSpeed = 1f;
        animData.animationStartTime = 0;
        return true;        
    }
}
