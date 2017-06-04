using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Tiled {
[XmlRoot("map")]
[System.Serializable]
public class TMXFile {
	[XmlAttribute("version")] public string version = "1.0";
	[XmlAttribute("orientation")] public string orientation = "orthogonal";
	[XmlAttribute("renderorder")] public string renderOrder = "right-down";
	[XmlAttribute("width")] public int width = 0;
	[XmlAttribute("height")] public int height = 0;
	[XmlAttribute("tilewidth")] public int tileWidth = 0;
	[XmlAttribute("tileheight")] public int tileHeight = 0;
	[XmlAttribute("hexsidelength")] public int? hexSideLength;
	[XmlAttribute("staggeraxis")] public string staggerAxis;
	[XmlAttribute("staggerindex")] public string staggerIndex;
	[XmlAttribute("backgroundcolor")] public string backgroundColor;
	[XmlAttribute("nextobjectid")] public int nextObjectID = 0;	

	[XmlElement("tileset", typeof(TileSet))] public TileSet[] tileSets;
	[XmlElement("layer", typeof(Layer))] public Layer[] layers;

	public static TMXFile Load (string path) {
		XmlSerializer deserializer = new XmlSerializer(typeof(TMXFile));
		TextReader textReader = new StreamReader(path);
		TMXFile map = (TMXFile)deserializer.Deserialize(textReader);
		textReader.Close();

		for (int i = 0; i < map.tileSets.Length; i++) {
			TileSet tileSet = map.tileSets[i];
			if (!string.IsNullOrEmpty(tileSet.source)) {
				string tsxPath = tileSet.source;
				tsxPath = Path.Combine(Path.GetDirectoryName(path), tsxPath);
                tsxPath = Path.GetFullPath(tsxPath);
				TileSet tsxFile = TileSet.Load(tsxPath);

				tsxFile.firstGID = tileSet.firstGID;
				tsxFile.isTSX = true;
				map.tileSets[i] = tsxFile;
			}
		}
		return map;
	}

	public void Save (string path) {
        XmlSerializer serializer = new XmlSerializer(typeof(TMXFile));
        TextWriter textWriter = new StreamWriter(path);
        XmlSerializerNamespaces nameSpaces = new XmlSerializerNamespaces();
        nameSpaces.Add("","");
        serializer.Serialize(textWriter, this, nameSpaces);
        textWriter.Close();

        for (int i = 0; i < tileSets.Length; i++) {
			TileSet tileSet = tileSets[i];
			if (tileSet.isTSX) {
				string tsxPath = tileSet.source;
				tsxPath = Path.Combine(Path.GetDirectoryName(path), tsxPath);
                tsxPath = Path.GetFullPath(tsxPath);
				tileSet.Save(tsxPath);
			}
		}
    }

	public int GetIndexOfTileSet (TileSet tileSet) {
		for (int i = 0; i < tileSets.Length; i++) {
			if (tileSets[i] == tileSet) return i;
		}
		return -1;
	}

	public TileSet GetTileSetByTileID (int tileID) {
		foreach (TileSet tileSet in tileSets) {
			if (tileID >= tileSet.firstGID && 
				tileID < tileSet.firstGID + tileSet.tileCount) {
				return tileSet;
			}
		}
		return null;
	}

	public void SetTile (Tile tile, Layer layer, int x, int y) {
		TileSet tileSet = GetTileSetByTileID(tile.id);
		layer.SetTileID(tile.id - tileSet.firstGID, x, y);
	}

	public Tile GetTile (Layer layer, int x, int y) {
		int tileID = layer.GetTileID(x, y);
		TileSet tileSet = GetTileSetByTileID(tileID);
		return tileSet.tiles[tileID - tileSet.firstGID];
	}

	public Tile GetTile (Layer layer, int index) {
		int tileID = layer.tileIDs[index];
		TileSet tileSet = GetTileSetByTileID(tileID);
		return tileSet.tiles[tileID - tileSet.firstGID];
	}
}

[XmlRoot("tileset")]
[System.Serializable]
public class TileSet {
	[XmlIgnore] public bool isTSX = false;
	[XmlAttribute("firstgid")] public int firstGID;
	[XmlIgnore] public bool firstGIDSpecified { get { return !isTSX; } set {} }

	[XmlAttribute("source")] public string source;
	[XmlIgnore] public bool sourceSpecified { get { return !isTSX; } set {} }

	[XmlAttribute("name")] public string name = "";
	[XmlIgnore] public bool nameSpecified { get { return source == null; } set {} }

	[XmlAttribute("tilewidth")] public int tileWidth = 0;
	[XmlIgnore] public bool tileWidthSpecified { get { return source == null; } set {} }

	[XmlAttribute("tileheight")] public int tileHeight = 0;
	[XmlIgnore] public bool tileHeightSpecified { get { return source == null; } set {} }

	[XmlAttribute("spacing")] public int spacing = 0;
	[XmlIgnore] public bool spacingSpecified { get { return source == null; } set {} }

	[XmlAttribute("margin")] public int margin = 0;
	[XmlIgnore] public bool marginSpecified { get { return source == null; } set {} }

	[XmlAttribute("tilecount")] public int tileCount = 0;
	[XmlIgnore] public bool tileCountSpecified { get { return source == null; } set {} }

	[XmlAttribute("columns")] public int columns = 0;
	[XmlIgnore] public bool columnsSpecified { get { return source == null; } set {} }
	
    [XmlIgnore] public int rows { 
		get { return (columns > 0) ? tileCount / columns : 0; } 
		set { tileCount = value * columns; }
	}

	[XmlElement("tileoffset", typeof(TilePoint))] public TilePoint tileOffset;
	[XmlIgnore] public bool tileOffsetSpecified { get { return source == null; } set {} }

	[XmlElement("image", typeof(Image))]  public Image image;
	[XmlIgnore] public bool imageSpecified { get { return source == null; } set {} }

	[XmlElement("tile", typeof(Tile))] public Tile[] tiles;
	[XmlIgnore] public bool tilesSpecified { get { return source == null; } set {} }

	[XmlElement("terraintypes", typeof(Terrain))] public Terrain[] terrainTypes;
	[XmlIgnore] public bool terrainTypesSpecified { get { return source == null; } set {} }

	[XmlElement("properties", typeof(Property))] public Property[] properties;
	[XmlIgnore] public bool propertiesSpecified { get { return source == null; } set {} }

	public static TileSet Load (string path) {
		XmlSerializer deserializer = new XmlSerializer(typeof(TileSet));
		TextReader textReader = new StreamReader(path);
		TileSet map = (TileSet)deserializer.Deserialize(textReader);
		textReader.Close();
		return map;
	}

	public void Save (string path) {
		string source = this.source;
		this.source = null;
        XmlSerializer serializer = new XmlSerializer(typeof(TMXFile));
        TextWriter textWriter = new StreamWriter(path);
        XmlSerializerNamespaces nameSpaces = new XmlSerializerNamespaces();
        nameSpaces.Add("","");
        serializer.Serialize(textWriter, this, nameSpaces);
        textWriter.Close();
        this.source = source;
    }
	public TileRect GetTileUVs (int tileGID) {
		if (tileGID < firstGID || tileGID >= firstGID + tileCount) return null;

		TileRect uvs = new TileRect();
		int tileIndex = tileGID - firstGID;
		int i = tileIndex % columns;
		int j = (rows - 1) - tileIndex / columns;
		
		uvs.x = (margin + i * (tileWidth + spacing));
		uvs.y = (margin + j * (tileHeight + spacing));
		uvs.width = tileWidth;
		uvs.height = tileHeight;

		uvs.x /= (float)image.width;
		uvs.y /= (float)image.height;
		uvs.width /= (float)image.width;
		uvs.height /= (float)image.height;

		return uvs;
	}
}

[System.Serializable]
public class Layer {
	[XmlAttribute("name")] public string name;
	[XmlAttribute("opacity")] public float? opacity;
	[XmlAttribute("visible")] public int? visible;
	[XmlAttribute("offsetx")] public int? offsetX;
	[XmlAttribute("offsety")] public int? offsetY;
	[XmlAttribute("width")] public int width;
	[XmlAttribute("height")] public int height;

	[XmlElement("property", typeof(Property))] public Property[] properties;
	[XmlElement("data", typeof(Data))] public Data tileData;
    
	private int[] _tileIDs;
	public int[] tileIDs {
		get {
			if (_tileIDs == null || 
				_tileIDs.Length != width * height) {

				_tileIDs = new int[width * height];

				if (tileData.encoding == "csv") {
					string[] tiles = tileData.contents.Split(',');
					for (int i = 0; i < tiles.Length; i++) {
						_tileIDs[i] = int.Parse(tiles[i].Trim());
					}
				}
				else if (tileData.encoding == "base64") {
					// > adapted from TiledSharp - https://github.com/marshallward/TiledSharp
					byte[] bytes = Convert.FromBase64String(tileData.contents);
					Stream stream = new MemoryStream(bytes, false);

					if (tileData.compression == "gzip") {
						stream = new GZipStream(stream, CompressionMode.Decompress);
					}
					else if (tileData.compression == "zlib") {
						int bodyLength = bytes.Length - 6;
		                byte[] bodyData = new byte[bodyLength];
		                Array.Copy (bytes, 2, bodyData, 0, bodyLength);
		                stream = new MemoryStream (bodyData, false);
		                stream = new DeflateStream(stream, CompressionMode.Decompress);
					}
					
					using (BinaryReader binaryReader = new BinaryReader(stream)){
						for (int j = 0; j < height; j++) {
							for (int i = 0; i < width; i++) {
								_tileIDs[i + j * width] = (int)binaryReader.ReadUInt32();
							}
						}
					}
					// <
				}
			}
			return _tileIDs;
		}
	}

	public void SetTileID (int id, int x, int y) {
		int index = x + y * width;
		tileIDs[index] = id;

		if (tileData.encoding == "csv") {
			string[] tileStrings = tileData.contents.Split(',');
			string tileString = tileStrings[index];

			if (tileString.StartsWith("\n")) tileStrings[index] = "\n" + id.ToString();
			else tileStrings[index] = id.ToString();
			
			tileData.contents = string.Join(",", tileStrings);
		}
	}

	public int GetTileID (int x, int y) {
		return tileIDs[x + y * width];
	}

	public int[] GetTileLocation (int index) {
		int[] loc = new int[2];
		loc[0] = index % width;
		loc[1] = index / width;
		return loc;
	}
}

[System.Serializable]
public class Tile {
	[XmlAttribute("id")] public int id = 0;
	[XmlAttribute("terrain")] public string terrain;
	[XmlAttribute("probability")] public float? probability;

	[XmlElement("properties", typeof(Property))] public Property[] properties;
	[XmlElement("animation", typeof(Frame))] public Frame[] animation;
	[XmlElement("image", typeof(Image))] public Image image { get; set; }
	[XmlElement("objectgroup", typeof(ObjectGroup))] public ObjectGroup objectGroup { get; set; }
}

[System.Serializable]
public class Image {
	[XmlAttribute("format")] public string format;
	[XmlAttribute("source")] public string source;
	[XmlAttribute("trans")] public string trans;
	[XmlAttribute("width")] public int width = 0;
	[XmlAttribute("height")] public int height = 0;

	[XmlElement("data", typeof(Data))] public Data data { get; set; }
}

[System.Serializable]
public class Data {
	[XmlAttribute("encoding")] public string encoding = "csv";
	[XmlAttribute("compression")] public string compression;
	[XmlText] public string contents = "";
}

[System.Serializable]
public class TilePoint {
	[XmlAttribute("x")] public int x = 0; 
	[XmlAttribute("y")] public int y = 0;
}

[System.Serializable]
public class TileRect {
	public float x = 0;
	public float y = 0;
	public float width = 1;
	public float height = 1;

	public float left { get { return x; } }
	public float right { get { return x + width; } }
	public float bottom { get { return y; } }
	public float top { get { return y + height; } }
}

[System.Serializable]
public class Terrain {
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("tile")] public int tile = 0;
}

[System.Serializable]
public class Property {
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("type")] public string type = "";
	[XmlAttribute("value")] public string val = "";
}

[System.Serializable]
public class Frame {
	[XmlAttribute("tileid")] public int tileID = 0;
	[XmlAttribute("duration")] public float duration = 0;
}

[System.Serializable]
public class ObjectGroup {
	[XmlAttribute("name")] public string name;
	[XmlAttribute("color")] public string color;
	[XmlAttribute("opacity")] public float? opacity;
	[XmlAttribute("visible")] public int? visible;
	[XmlAttribute("offsetx")] public int? offsetX;
	[XmlAttribute("offsety")] public int? offsetY;
	[XmlAttribute("draworder")] public string drawOrder = "topdown";

	[XmlElement("property", typeof(Property))] public Property[] properties;
	[XmlElement("object", typeof(TileObject))] public TileObject[] objects;
}

[System.Serializable]
public class TileObject {
	[XmlAttribute("id")] public int id = 0;
	[XmlAttribute("name")] public string name;
	[XmlAttribute("type")] public string type;
	[XmlAttribute("x")] public int x = 0;
	[XmlAttribute("y")] public int y = 0;
	[XmlAttribute("width")] public int width = 0;
	[XmlAttribute("height")] public int height = 0;
	[XmlAttribute("rotation")] public float? rotation;
	[XmlAttribute("gid")] public int? gid;
	[XmlAttribute("visible")] public int? visible;

	[XmlElement("property", typeof(Property))] public Property[] properties;
	[XmlElement("ellipse", typeof(Elipse))] public Elipse ellipse { get; set; }
	[XmlElement("polygon", typeof(Polygon))] public Polygon polygon { get; set; }
	[XmlElement("polyline", typeof(Polygon))] public Polygon polyline { get; set; }
	[XmlElement("image", typeof(Image))] public Image image { get; set; }
}

[System.Serializable]
public class Elipse {
	[XmlAttribute("x")] public int x = 0;
	[XmlAttribute("y")] public int y = 0;
	[XmlAttribute("width")] public int width  = 0;
	[XmlAttribute("height")] public int height = 0;
}

[System.Serializable]
public class Polygon {
	[XmlAttribute("points")] public string points = "";
}

[System.Serializable]
public class ImageLayer {
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("offsetx")] public int offsetX = 0;
	[XmlAttribute("offsety")] public int offsetY = 0;
	[XmlAttribute("opacity")] public float opacity = 1;
	[XmlAttribute("visible")] public int visible = 1;

	[XmlElement("property", typeof(Property))] public Property[] properties;
	[XmlElement("image", typeof(Image))] public Image image;

}
}