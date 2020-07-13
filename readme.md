# Tuesday (Tiled Unity Editor, Serializer, Deserializer, and You)

Tuesday is a generic C# Tiled (.tmx) serializer and deserializer and a set of Unity editor scripts that allow you to drag and drop TMX files into your scene, make edits, and save back out as TMX files.

The goals of this project are full support of the TMX file format, feature parity with the standalone Tiled map editor, and minimal dependencies.


## Installation

To add as a Unitypackage, [download it from the itch.io page.](https://318arcade.itch.io/tuesday)

To add as a Git submodule: 
 `git submodule add https://github.com/ShreveportArcade/Tuesday.git Assets/Tuesday`

To add with Unity's package manager, put the following in your `manifest.json` file:
 `"com.shreveportarcade.tuesday": "https://github.com/ShreveportArcade/Tuesday.git"`


## Features

 * Compatible with Tiled files made with any version of Tiled.
 * Supports all features of Tiled 1.4 file format:
   * Orthographic, Hexagonal, and Isometric Maps
   * Tile Layers, Object Groups, Group Layers, and Image Layers
   * Infinite and Fixed Maps
   * Single image and image collection tile sets
   * Embedded and external tile sets
   * Layer tint, opacity, and visibility
   * Object reference properties
   * Object alignment
 * Drag and Drop TMX files into the Scene View or Hierarchy
 * Imports TMX/TSX/TX files as native Unity Tilemaps/Tiles/Sprites
 * Exports native Unity Tilemaps/Tiles/Sprites as TMX/TSX/TX files
 * CSV, Base64, GZIP, and zLib encoding/decoding
 * Collision geometry support
 * Template Group (.tx) prefab replacement
 * Edit your tilemaps in Unity using the standard Unity tools


## Prefab Replacement

Template Group files can be remapped as prefabs in the inspector by selecting the `.tx` file in the project view and dragging a prefab asset to the prefab slot in the inspector.


## Custom Properties

Components can be added and their fields/properties modified using the format "ClassName.fieldName". For example, you can set the gravity scale of a prefab's Rigidbody2D by adding a float property called "Rigidbody2D.gravityScale".
