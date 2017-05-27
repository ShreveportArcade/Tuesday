using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;
using UnityEngine;

namespace Tiled {

[XmlRoot("map")]
public class Map {
	[XmlAttribute("version")] public string version = "1.0";
	[XmlAttribute("orientation")] public string orientation = "orthogonal";
	[XmlAttribute("renderorder")] public string renderOrder = "right-down";
	[XmlAttribute("width")] public int width = 0;
	[XmlAttribute("height")] public int height = 0;
	[XmlAttribute("tilewidth")] public int tileWidth = 0;
	[XmlAttribute("tileheight")] public int tileHeight = 0;
	[XmlAttribute("hexsidelength")] public int hexSideLength = 0;
	[XmlAttribute("staggeraxis")] public string staggerAxis = "x";
	[XmlAttribute("staggerindex")] public string staggerIndex = "even";
	[XmlAttribute("backgroundcolor")] public string backgroundColor = "#00000000";
	[XmlAttribute("nextobjectid")] public int nextObjectID = 0;	
	[XmlElement("tileset", typeof(TileSet))] public TileSet[] tileSets;
	[XmlElement("layer", typeof(Layer))] public Layer[] layers;

	public static void Blah () {
		Map map = Map.LoadTMX(Path.Combine(Application.dataPath, "read.tmx"));
		map.SaveTMX(Path.Combine(Application.dataPath, "write.tmx"));
	}

	public static Map LoadTMX (string path) {
		XmlSerializer deserializer = new XmlSerializer(typeof(Map));
		TextReader textReader = new StreamReader(path);
		Map map = (Map)deserializer.Deserialize(textReader);
		textReader.Close();
		return map;
	}

	public void SaveTMX (string path) {
		XmlSerializer serializer = new XmlSerializer(typeof(Map));
		TextWriter textWriter = new StreamWriter(path);
		serializer.Serialize(textWriter, this);
		textWriter.Close();
	}
}

public class TileSet {
	[XmlAttribute("firstgid")] public int firstGID = 0;
	[XmlAttribute("source")] public string source = "";
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("tilewidth")] public int tileWidth = 0;
	[XmlAttribute("tileheight")] public int tileHeight = 0;
	[XmlAttribute("spacing")] public int spacing = 0;
	[XmlAttribute("margin")] public int margin = 0;
	[XmlAttribute("tilecount")] public int tileCount = 0;
	[XmlAttribute("columns")] public int columns = 0;

	public Image image;
	public TileOffset tileoffset;
	public Terrain[] terrainTypes;
	[XmlElement("properties", typeof(Property))] public Property[] properties;
	[XmlElement("tile", typeof(Tile))] public Tile[] tiles;
	
	public void SaveTMS (string path) {
		XmlSerializer serializer = new XmlSerializer(typeof(TileSet));
		TextWriter textWriter = new StreamWriter(path);
		serializer.Serialize(textWriter, this);
		textWriter.Close();
	}
}

public class TileOffset {
	[XmlAttribute("x")] public int x = 0; 
	[XmlAttribute("y")] public int y = 0;
}

public class Image {
	[XmlAttribute("format")] public string format = "";
	[XmlAttribute("source")] public string source = "";
	[XmlAttribute("trans")] public string trans = "";
	[XmlAttribute("width")] public int width = 0;
	[XmlAttribute("height")] public int height = 0;

	[XmlElement("data", typeof(Data))] public Data data;
}

public class Data {
	[XmlAttribute("encoding")] public string encoding = "csv";
	[XmlAttribute("compression")] public string compression = "gzip";
	[XmlText] public string data = "";
}

public class Terrain {
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("tile")] public int tile = 0;
}

public class Property {
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("type")] public string type = "";
	[XmlAttribute("value")] public string val = "";
}

public class Tile {
	[XmlAttribute("id")] public int id = 0;
	[XmlAttribute("terrain")] public string terrain = "0,0,0,0";
	[XmlAttribute("probability")] public float probability = 1;

	[XmlElement("properties", typeof(Property))] public Property[] properties;
	[XmlElement("animation", typeof(Frame))] public Frame[] animation;
	[XmlElement("image", typeof(Image))] public Image image;
	[XmlElement("objectgroup", typeof(ObjectGroup))] public ObjectGroup objectGroup;
}

public class Frame {
	[XmlAttribute("tileid")] public int tileID = 0;
	[XmlAttribute("duration")] public float duration = 0;
}

public class Layer {
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("opacity")] public float opacity = 1;
	[XmlAttribute("visible")] public int visible = 1;
	[XmlAttribute("offsetx")] public int offsetX = 0;
	[XmlAttribute("offsety")] public int offsetY = 0;
	[XmlAttribute("width")] public int width = 0;
	[XmlAttribute("height")] public int height = 0;

	[XmlElement("property", typeof(Property))] public Property[] properties;
	[XmlElement("data", typeof(Data))] public Data data;
}

public class ObjectGroup {
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("color")] public string color = "00000000";
	[XmlAttribute("opacity")] public float opacity = 1;
	[XmlAttribute("visible")] public int visible = 1;
	[XmlAttribute("offsetx")] public int offsetX = 0;
	[XmlAttribute("offsety")] public int offsetY = 0;
	[XmlAttribute("draworder")] public string drawOrder = "topdown";

	[XmlElement("property", typeof(Property))] public Property[] properties;
	[XmlElement("object", typeof(Object))] public Object[] objects;
}

public class Object {
	[XmlAttribute("id")] public int id = 0;
	[XmlAttribute("name")] public string name = "";
	[XmlAttribute("type")] public string type = "";
	[XmlAttribute("x")] public int x = 0;
	[XmlAttribute("y")] public int y = 0;
	[XmlAttribute("width")] public int width = 0;
	[XmlAttribute("height")] public int height = 0;
	[XmlAttribute("rotation")] public float rotation = 0;
	[XmlAttribute("gid")] public int gid = 0;
	[XmlAttribute("visible")] public int visible = 1;

	[XmlElement("property", typeof(Property))] public Property[] properties;
	[XmlElement("ellipse", typeof(Elipse))] public Elipse ellipse;
	[XmlElement("polygon", typeof(Polygon))] public Polygon polygon;
	[XmlElement("polyline", typeof(Polygon))] public Polygon polyline;
	[XmlElement("image", typeof(Image))] public Image image;
}

public class Elipse {
	[XmlAttribute("x")] public int x = 0;
	[XmlAttribute("y")] public int y = 0;
	[XmlAttribute("width")] public int width  = 0;
	[XmlAttribute("height")] public int height = 0;
}

public class Polygon {
	[XmlAttribute("points")] public string points = "0,0 1,1 1,-1";
}

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