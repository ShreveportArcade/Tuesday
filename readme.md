# Tuesday (Tiled Unity Editor, Serializer, Deserializer, and You)

Tuesday is a generic C# Tiled (.tmx) serializer and deserializer and a set of Unity editor scripts that allow you to drag and drop TMX files into your scene, make edits, and save back out as TMX files.

The goals of this project are full support of the TMX file format, feature parity with the standalone Tiled map editor, and minimal dependencies.


## Installation

[To add as a Unitypackage, download as from the itch.io page.](https://318arcade.itch.io/tuesday)

To add as a Git submodule:
`git submodule add https://github.com/ShreveportArcade/Tuesday.git Assets/Tuesday`

## Prefab Replacement

Tiled objects can be replaced with prefabs by adding a custom file property called "prefab" that points to a prefab in your Unity project. Prefab overrides can be set with custom properties named "ClassName.fieldName". For example, you can set the gravity scale of a prefab's Rigidbody2D by adding a float property called "Rigidbody2D.gravityScale".

## Features

 * Drag and Drop TMX files into the Scene View or Hierarchy
 * External Tile Set support (TSX files)
 * CSV, Base64, GZIP, and zLib encoding/decoding
 * Collision geometry support
 * Tile layer tint support
 * Tiled Object support
 * Paint individual tiles in Unity
 * Paint Terrains in Unity
 * Prefab replacement
 * Export your tile layer changes back out as TMX/TSX files
 * minimal component requirements

## Roadmap
 * edit/export Tiled Objects
 * Paint Wang Tiles
 * Template Group (TGX) support
