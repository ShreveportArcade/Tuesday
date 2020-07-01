using UnityEngine;
using UnityEngine.Tilemaps;

// Tile that plays an animated loops of sprites
[CreateAssetMenu]
public class AnimatedTile : Tile {

    public Sprite[] sprites;
    public float[] durations;

    public override void GetTileData(Vector3Int pos, ITilemap map, ref TileData tileData) {
        if (sprites != null && sprites.Length > 0) tileData.sprite = sprites[sprites.Length - 1];
    }

    public override bool GetTileAnimationData(Vector3Int pos, ITilemap map, ref TileAnimationData animData) {
        if (sprites != null && sprites.Length > 0) {
            animData.animatedSprites = sprites;
            animData.animationSpeed = 1f;
            animData.animationStartTime = 0;
            return true;
        }
        return false;
    }
}