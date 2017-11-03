# Tuesday (Tiled Unity Editor, Serializer, Deserializer, and You)

Tuesday is a generic C# Tiled (.tmx) serializer and deserializer and a set of Unity editor scripts that allow you to drag and drop TMX files into your scene, make edits, and save back out as TMX files.

The goals of this project are full support of the TMX file format, feature parity with the standalone Tiled map editor, and minimal dependencies.

## Features

 * Drag and Drop TMX files into the Scene View or Hierarchy
 * External Tile Set support (TSX files)
 * CSV, Base64, GZIP, and zLib encoding/decoding
 * Collision geometry support
 * Paint individual tiles in Unity
 * Export your changes back out as TMX/TSX files
 * lightweight code, only requires one component per map

## Roadmap
 * Compartmentalize TileMapEditor code
 * Paint Terrains
 * Tiled Object support
 * Wang Tile support
 * Template Group (TGX) support
 * feature parity with Tiled standalone