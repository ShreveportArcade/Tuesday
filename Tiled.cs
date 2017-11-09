/*
Copyright (C) 2017 Nolan Baker

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, 
and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions 
of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.IO.Compression;

namespace Tiled {
[XmlRoot("map")]
[System.Serializable]
public class TMXFile {
    [XmlIgnore] public bool hasDocType = false;
    [XmlAttribute("version")] public string version = "1.0";
    [XmlAttribute("tiledversion")] public string tiledVersion;
    [XmlIgnore] public bool tiledVersionSpecified { get { return tiledVersion != null; } set { } }
    [XmlAttribute("orientation")] public string orientation = "orthogonal";
    [XmlAttribute("renderorder")] public string renderOrder = "right-down";

    [XmlIgnore] public int _width = 0;
    [XmlAttribute("width")] public int width {
        get { 
            if (infinite && layers != null) {
                int max = 0;
                for (int i = 0; i < layers.Length; i++) {
                    if (layers[i].tileData.width > max) max = layers[i].tileData.width;
                }
                return max;
            }
            return _width;    
        }
        set { _width = value; }
    }

    [XmlIgnore] public int _height = 0;
    [XmlAttribute("height")] public int height {
        get { 
            if (infinite && layers != null) {
                int max = 0;
                for (int i = 0; i < layers.Length; i++) {
                    if (layers[i].tileData.height > max) max = layers[i].tileData.height;
                }
                return max;
            }
            return _height;    
        }
        set { _height = value; }
    }

    [XmlAttribute("tilewidth")] public int tileWidth = 0;
    [XmlAttribute("tileheight")] public int tileHeight = 0;
    [XmlAttribute("hexsidelength")] public int? hexSideLength;
    [XmlAttribute("staggeraxis")] public string staggerAxis;
    [XmlIgnore] public bool staggerAxisSpecified { get { return !string.IsNullOrEmpty(staggerAxis); } set {}}
    [XmlAttribute("staggerindex")] public string staggerIndex;
    [XmlIgnore] public bool staggerIndexSpecified { get { return !string.IsNullOrEmpty(staggerIndex); } set {}}
    [XmlAttribute("backgroundcolor")] public string backgroundColor;
    [XmlIgnore] public bool backgroundColorSpecified { get { return !string.IsNullOrEmpty(backgroundColor); } set {}}
    [XmlAttribute("infinite")] public int infiniteInt = -1;
    [XmlIgnore] public bool infinite { get { return infiniteInt > 0; } }
    [XmlIgnore] public bool infiniteSpecified { get { return infiniteInt > -1; } set {}}
    [XmlAttribute("nextobjectid")] public int nextObjectID = 0;

    [XmlArray("properties")] [XmlArrayItem("property", typeof(Property))] public Property[] properties;
    [XmlIgnore] public bool propertiesSpecified { get { return properties != null && properties.Length > 0; } set { } }
    [XmlElement("tileset", typeof(TileSet))] public TileSet[] tileSets;
    [XmlElement("layer", typeof(Layer))] public Layer[] layers;
    [XmlElement("objectgroup", typeof(ObjectGroup))] public ObjectGroup[] objectGroups;

    public static TMXFile Load (string path) {
        XmlSerializer deserializer = new XmlSerializer(typeof(TMXFile));
        TextReader textReader = new StreamReader(path);
        TMXFile map = (TMXFile)deserializer.Deserialize(textReader);
        textReader.Close();

        XmlTextReader xmlReader = new XmlTextReader(path);
        bool hasDocType = false;
        for (int i = 0; i < 3; i++) {
            xmlReader.Read();
            if (xmlReader.NodeType == XmlNodeType.DocumentType) {
                hasDocType = true;
                break;
            }
        }
        map.hasDocType = hasDocType;
        xmlReader.Close();

        for (int i = 0; i < map.tileSets.Length; i++) {
            TileSet tileSet = map.tileSets[i];
            if (tileSet.hasSource) {
                string tsxPath = tileSet.source;
                tsxPath = Path.Combine(Path.GetDirectoryName(path), tsxPath);
                tsxPath = Path.GetFullPath(tsxPath);
                TileSet tsxFile = TileSet.Load(tsxPath);

                tsxFile.firstGID = tileSet.firstGID;
                tsxFile.source = tileSet.source;
                tileSet = tsxFile;
            }

            if ((tileSet.columns == 0 || tileSet.tileCount == 0) && tileSet.image != null) {
                tileSet.columns = (tileSet.image.width - 2 * tileSet.margin + tileSet.spacing) / (tileSet.tileWidth + tileSet.spacing);
                tileSet.rows = (tileSet.image.height - 2 * tileSet.margin + tileSet.spacing) / (tileSet.tileHeight + tileSet.spacing);
            }
            map.tileSets[i] = tileSet;
        }
        return map;
    }

    public void Save (string path) {
        XmlSerializer serializer = new XmlSerializer(typeof(TMXFile));
        TextWriter textWriter = new StreamWriter(path);
        XmlTextWriter xmlWriter = new XmlTextWriter(textWriter);
        xmlWriter.Formatting = Formatting.Indented;
        xmlWriter.Indentation = 1;
        xmlWriter.WriteStartDocument(); // Why is C# writing "UTF-8" as lowercase?
        if (hasDocType) xmlWriter.WriteDocType("map", null, "http://mapeditor.org/dtd/1.0/map.dtd", null);
        XmlSerializerNamespaces nameSpaces = new XmlSerializerNamespaces();
        nameSpaces.Add("","");
        serializer.Serialize(xmlWriter, this, nameSpaces);
        xmlWriter.Close();
        textWriter.Close();

        for (int i = 0; i < tileSets.Length; i++) {
            TileSet tileSet = tileSets[i];
            if (tileSet.hasSource) {
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

    // TODO: cache results in tileID to TileSet dictionary
    public TileSet GetTileSetByTileID (int tileID) {
        foreach (TileSet tileSet in tileSets) {
            if (tileID >= tileSet.firstGID && 
                tileID < tileSet.firstGID + tileSet.tileCount) {
                return tileSet;
            }
        }
        if (tileSets.Length > 0) return tileSets[0];
        return null;
    }

    public void SetTile (Tile tile, Layer layer, int x, int y) {
        TileSet tileSet = GetTileSetByTileID(tile.id);
        layer.SetTileID(tile.id - tileSet.firstGID, x, y);
    }

    public Tile GetTile (Layer layer, int x, int y) {
        if (!infinite && (x < 0 || x >= width || y < 0 || y >= height)) return null;
        int tileID = layer.GetTileID(x, y);
        TileSet tileSet = GetTileSetByTileID(tileID);
        return tileSet.GetTile(tileID);
    }

    public Tile GetTile (TileSet tileSet, int tileID) {
        return tileSet.GetTile(tileID);
    }

    public Tile GetTile (int[] terrain) {
        foreach (TileSet tileSet in tileSets) {
            foreach (Tile tile in tileSet.tiles) {
                if (terrain == tile.terrain) return tile;
            }
        }
        return null;
    }

    static int seed = 0;
    public Tile GetTile (TileSet tileSet, int[] terrain) {
        int mostMatches = 0;
        Tile bestMatch = null;
        System.Random r = new System.Random(seed + Environment.TickCount);
        seed++;
        foreach (Tile tile in tileSet.tiles) {
            int matches = 0;
            for (int i = 0; i < terrain.Length; i++) {
                if (terrain[i] == tile.terrain[i]) matches++;
            }
            if (matches > mostMatches) {
                mostMatches = matches;
                bestMatch = tile;
            }
            else if (matches == mostMatches 
                && tile.probability.HasValue 
                && tile.probability.Value > r.NextDouble()) {
                bestMatch = tile;
            }
        }
        return bestMatch;
    }
}

[XmlRoot("tileset")]
[System.Serializable]
public class TileSet {
    [XmlIgnore] public bool hasSource { get { return !string.IsNullOrEmpty(source); }}

    [XmlAttribute("firstgid")] public int firstGID;
    [XmlIgnore] public bool firstGIDSpecified { get { return firstGID != 0; } set {} }

    [XmlAttribute("source")] public string source;
    [XmlIgnore] public bool sourceSpecified { get { return hasSource; } set {} }

    [XmlAttribute("name")] public string name = "";
    [XmlIgnore] public bool nameSpecified { get { return !hasSource; } set {} }

    [XmlAttribute("tilewidth")] public int tileWidth = 0;
    [XmlIgnore] public bool tileWidthSpecified { get { return !hasSource; } set {} }

    [XmlAttribute("tileheight")] public int tileHeight = 0;
    [XmlIgnore] public bool tileHeightSpecified { get { return !hasSource; } set {} }

    [XmlAttribute("spacing")] public int spacing = 0;
    [XmlIgnore] public bool spacingSpecified { get { return spacing > 0 && !hasSource; } set {} }

    [XmlAttribute("margin")] public int margin = 0;
    [XmlIgnore] public bool marginSpecified { get { return margin > 0 && !hasSource; } set {} }

    [XmlAttribute("tilecount")] public int tileCount = 0;
    [XmlIgnore] public bool tileCountSpecified { get { return !hasSource; } set {} }

    [XmlAttribute("columns")] public int columns = 0;
    [XmlIgnore] public bool columnsSpecified { get { return !hasSource; } set {} }
    
    [XmlIgnore] public int rows { 
        get { return (columns > 0) ? tileCount / columns : 0; } 
        set { tileCount = value * columns; }
    }

    [XmlElement("tileoffset", typeof(TilePoint))] public TilePoint tileOffset;
    [XmlIgnore] public bool tileOffsetSpecified { 
        get { return string.IsNullOrEmpty(source) && tileOffset.x != 0 && tileOffset.y != 0; } 
        set {} 
    }

    [XmlElement("image", typeof(Image))]  public Image image;
    [XmlIgnore] public bool imageSpecified { get { return !hasSource; } set {} }

    [XmlArray("terraintypes")] [XmlArrayItem("terrain", typeof(Terrain))] public Terrain[] terrainTypes;
    [XmlIgnore] public bool terrainTypesSpecified { get { return !hasSource; } set { } }

    [XmlElement("tile", typeof(Tile))] public Tile[] tiles;
    [XmlIgnore] public bool tilesSpecified { get { return !hasSource; } set {} }

    [XmlArray("properties")] [XmlArrayItem("property", typeof(Property))] public Property[] properties;
    [XmlIgnore] public bool propertiesSpecified { get { return !hasSource && properties != null && properties.Length > 0; } set { } }

    [XmlArray("wangsets")] [XmlArrayItem("wangset", typeof(WangSet))] public WangSet[] wangSets;
    [XmlIgnore] public bool wangSetsSpecified { get { return !hasSource && wangSets != null && wangSets.Length > 0; } set { } }

    public static TileSet Load (string path) {
        XmlSerializer deserializer = new XmlSerializer(typeof(TileSet));
        TextReader textReader = new StreamReader(path);
        TileSet tileSet = (TileSet)deserializer.Deserialize(textReader);
        textReader.Close();
        
        return tileSet;
    }

    public void Save (string path) {
        string source = this.source;
        this.source = null;
        XmlSerializer serializer = new XmlSerializer(typeof(TileSet));
        TextWriter textWriter = new StreamWriter(path);
        XmlTextWriter xmlWriter = new XmlTextWriter(textWriter);
        xmlWriter.Formatting = Formatting.Indented;
        xmlWriter.Indentation = 1;
        xmlWriter.WriteStartDocument();
        XmlSerializerNamespaces nameSpaces = new XmlSerializerNamespaces();
        nameSpaces.Add("","");
        serializer.Serialize(xmlWriter, this, nameSpaces);
        xmlWriter.Close();
        textWriter.Close();
        this.source = source;
    }

    public Tile GetTile (int tileGID) {
        if (tileGID < firstGID || tiles == null) return Tile.empty;
        int tileIndex = tileGID - firstGID;
        for (int i = 0; i < tiles.Length; i++) {
            if (tileIndex == tiles[i].id) return tiles[i];
        }
        return Tile.empty;
    }
    
    // TODO: Cache UVs
    public TileRect GetTileUVs (int tileGID, float inset = 0) {
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

        uvs.x += inset;
        uvs.y += inset;
        uvs.width -= inset * 2;
        uvs.height -= inset * 2;

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
    [XmlAttribute("x")] public int x;
    [XmlAttribute("y")] public int y;
    [XmlAttribute("width")] public int width;
    [XmlAttribute("height")] public int height;

    [XmlArray("properties")] [XmlArrayItem("property", typeof(Property))] public Property[] properties;
    [XmlIgnore] public bool propertiesSpecified { get { return properties != null && properties.Length > 0; } set { } }
    
    [XmlIgnore] public TileData _tileData;
    [XmlElement("data", typeof(TileData))] public TileData tileData {
        get { return _tileData; }
        set {
            _tileData = value;
            _tileData.Refresh(width, height);
        }
    }
    
    const uint FlippedHorizontallyFlag = 0x80000000;
    const uint FlippedVerticallyFlag = 0x40000000;
    const uint FlippedAntiDiagonallyFlag = 0x20000000;
    const uint RotatedHexagonal120Flag = 0x10000000;

    public void SetTileID (int id, int x, int y, uint flags = 0) {
        tileData[x, y] = (uint)id | flags;
    }

    public int GetTileID (int x, int y) {
        return (int)(tileData[x, y] & ~(FlippedHorizontallyFlag | FlippedVerticallyFlag | FlippedAntiDiagonallyFlag | RotatedHexagonal120Flag));
    }

    public bool FlippedHorizontally (int x, int y) {
        return (tileData[x, y] & FlippedHorizontallyFlag) == FlippedHorizontallyFlag;
    }

    public bool FlippedVertically (int x, int y) {
        return (tileData[x, y] & FlippedVerticallyFlag) == FlippedVerticallyFlag;
    }

    public bool FlippedAntiDiagonally (int x, int y) {
        return (tileData[x, y] & FlippedAntiDiagonallyFlag) == FlippedAntiDiagonallyFlag;
    }

    public bool RotatedHexagonal120 (int x, int y) {
        return (tileData[x, y] & RotatedHexagonal120Flag) == RotatedHexagonal120Flag;
    }
}

[System.Serializable]
public class Tile {

    private static Tile _empty;
    public static Tile empty {
        get { 
            if (_empty == null) {
                _empty = new Tile();
                _empty.id = -1;
                _empty.terrainStr = ",,,";
            }
            return _empty;
        }
    }
        
    [XmlAttribute("id")] public int id = 0;

    [XmlIgnore] public int[] terrain;
    [XmlAttribute("terrain")] public string terrainStr {
        get {
            if (terrain == null || terrain.Length == 0) return null;
            string s = (terrain[0] == -1) ? "" : terrain[0].ToString();
            for (int i = 1; i < terrain.Length; i++) {
                s += ",";
                s += (terrain[i] == -1) ? "" : terrain[i].ToString();
            }
            return s;
        }
        set {
            string[] s = value.Split(',');
            terrain = new int[s.Length];
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == "") terrain[i] = -1;
                else terrain[i] = int.Parse(s[i]);
            }
        }
    }
    [XmlIgnore] public bool terrainStrSpecified { get { return !string.IsNullOrEmpty(terrainStr); } set {} }
    
    [XmlAttribute("probability")] public float? probability;
    [XmlIgnore] public bool probabilitySpecified { get { return probability != null; } set { } }

    [XmlArray("properties")] [XmlArrayItem("property", typeof(Property))] public Property[] properties;
    [XmlIgnore] public bool propertiesSpecified { get { return properties != null && properties.Length > 0; } set { } }
    [XmlElement("animation", typeof(Frame))] public Frame[] animation;
    [XmlElement("image", typeof(Image))] public Image image;
    [XmlIgnore] public bool imageSpecified { get { return image != null && !string.IsNullOrEmpty(image.source); } set {} }
    [XmlElement("objectgroup", typeof(ObjectGroup))] public ObjectGroup objectGroup;
    [XmlIgnore] public bool objectGroupSpecified { 
        get { return objectGroup != null && objectGroup.objects != null && objectGroup.objects.Length > 0; } 
        set {} 
    }
}

[System.Serializable]
public class Image {
    [XmlAttribute("format")] public string format;
    [XmlIgnore] public bool formatSpecified { get { return !string.IsNullOrEmpty(format); } set{} }

    [XmlAttribute("source")] public string source;

    [XmlAttribute("trans")] public string trans;
    [XmlIgnore] public bool transSpecified { get { return !string.IsNullOrEmpty(trans); } set{} }

    [XmlAttribute("width")] public int width = 0;
    [XmlAttribute("height")] public int height = 0;

    [XmlElement("data", typeof(Data))] public Data data;
    [XmlIgnore] public bool dataSpecified { get { return data != null && !string.IsNullOrEmpty(data.dataString); } set {} }
}

[System.Serializable]
public class Data {
    [XmlAttribute("encoding")] public string encoding = "csv";

    [XmlAttribute("compression")] public string compression;
    [XmlIgnore] public bool compressionSpecified { get { return !string.IsNullOrEmpty(compression); } set{} }

    [XmlText] public string dataString = "";
    [XmlIgnore] public bool dataStringSpecified { get { return !chunksSpecified; } set {} }

    [XmlElement("chunk", typeof(Chunk))] public Chunk[] chunks;
    [XmlIgnore] public bool chunksSpecified { get { return chunks != null && chunks.Length > 0; } set {} }
}

[System.Serializable]
public class TileData : Data {
    [XmlIgnore] public int x;
    [XmlIgnore] public int y;
    [XmlIgnore] public int width;
    [XmlIgnore] public int height;

    public void Refresh(int width, int height) {
        if (dataStringSpecified) {
            this.x = 0;
            this.y = 0;
            this.width = width;
            this.height = height;
        }
        else {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = -int.MaxValue;
            int maxY = -int.MaxValue;
            for (int i = 0; i < chunks.Length; i++) {
                Chunk c = chunks[i];
                if (c.x > maxX) maxX = c.x;
                if (c.x < minX) minX = c.x;
                if (c.y > maxY) maxY = c.y;
                if (c.y < minY) minY = c.y;                
            }
            this.x = minX;
            this.y = minY;
            this.width = maxX - minX + chunks[0].width;
            this.height = maxY - minY + chunks[0].height;
        }
    }
    
    [XmlIgnore][NonSerialized] private uint[] contentData;
    [XmlIgnore] public uint this[int x, int y] {
        get {
            if (dataStringSpecified) {
                if (contentData == null || contentData.Length != width * height) {
                    contentData = Decode(dataString, width, height);
                }
                return contentData[x + y * width];
            }
            else {
                for (int i = 0; i < chunks.Length; i++) {
                    Chunk c = chunks[i];
                    if (x >= c.x && x < c.x + c.width && y >= c.y && y < c.y + c.height) {
                        if (c.contentData == null) c.contentData = Decode(c.dataString, c.width, c.height);
                        return c.contentData[(x-c.x) + (y-c.y) * c.width];
                    }
                }
                return 0;
            }
        }
        set {
            if (dataStringSpecified) contentData[x + y * width] = value;
            else {
                for (int i = 0; i < chunks.Length; i++) {
                    Chunk c = chunks[i];
                    if (x >= c.x && x < c.x + c.width && y >= c.y && y < c.y + c.height) {
                        if (c.contentData == null) c.contentData = Decode(c.dataString, c.width, c.height);
                        c.contentData[(x-c.x) + (y-c.y) * c.width] = value;
                        return;
                    }
                }
                Chunk chunk = new Chunk();
                chunk.width = chunks[0].width;
                chunk.height = chunks[0].height;
                chunk.x = (int)Math.Floor((double)x / (double)chunk.width) * chunk.width;
                chunk.y = (int)Math.Floor((double)y / (double)chunk.height) * chunk.height;
                
                chunk.contentData = new uint[chunk.width * chunk.height];
                int index = (x-chunk.x) + (y-chunk.y) * chunk.width;
                chunk.contentData[index] = value;
                Array.Resize(ref chunks, chunks.Length+1);
                chunks[chunks.Length-1] = chunk;
                Refresh(width, height);
            }
        }
    }

    [XmlIgnore] public int Length {
        get { return width * height; }
    }

    public void Encode () {
        if (dataStringSpecified) {
            dataString = Encode(contentData, width, height);
        }
        else {
            foreach (Chunk chunk in chunks) {
                chunk.dataString = Encode(chunk.contentData, chunk.width, chunk.height);
            }
        }
    }

    public string Encode (uint[] data, int width, int height) {
        string dataString = "";
        if (encoding == "csv") {
            string csv = "";
            for (int j = 0; j < height; j++) {
                csv += "\n";
                for (int i = 0; i < width; i++) {
                    csv += data[i + j * width] + ",";
                }
            }
            csv = csv.TrimEnd(',');
            csv += "\n";
            dataString = csv;
        }
        else if (encoding == "base64") {
            MemoryStream stream = new MemoryStream();
            using (BinaryWriter binaryWriter = new BinaryWriter(stream)){
                for (int j = 0; j < height; j++) {
                    for (int i = 0; i < width; i++) {
                        binaryWriter.Write(data[i + j * width]);
                    }
                }
            }

            byte[] bytes = stream.ToArray();
            if (compression == "gzip") {
                using (MemoryStream compress = new MemoryStream()) {
                    using (GZipStream gzip = new GZipStream(compress, CompressionMode.Compress)) {
                        gzip.Write(bytes, 0, bytes.Length);
                    }
                    bytes = compress.ToArray();
                }
            }
            else if (compression == "zlib") {
                using (MemoryStream compress = new MemoryStream()) {
                    using (DeflateStream zlib = new DeflateStream(compress, CompressionMode.Compress)) {
                        zlib.Write(bytes, 0, bytes.Length);
                    }
                    UInt32 a = 1;
                    UInt32 b = 0;
                    for (int i = 0; i < bytes.Length; i++) {
                        a = (a + bytes[i]) % 65521;
                        b = (b + a) % 65521;
                    }
                    UInt32 alder = (b << 16) | a;

                    byte[] compressedBytes = compress.ToArray();
                    int len = compressedBytes.Length;
                    bytes = new byte[len+6];
                    Array.ConstrainedCopy(compressedBytes, 0, bytes, 2, len);
                    
                    // first 2 bytes - zlib header for default compression
                    bytes[0] = 0x78;
                    bytes[1] = 0x9C;
                    
                    // last 4 bytes - alder32 checksum
                    bytes[len+2] = (byte)((alder>>24) & 0xFF);
                    bytes[len+3] = (byte)((alder>>16) & 0xFF);
                    bytes[len+4] = (byte)((alder>>8) & 0xFF);
                    bytes[len+5] = (byte)(alder & 0xFF);
                }
            }
            
            dataString = Convert.ToBase64String(bytes);
            stream.Dispose();
        }
        return dataString;
    }

    public uint[] Decode (string dataString, int width, int height) {
        uint[] data = new uint[width * height];

        if (encoding == "csv") {
            string[] tiles = dataString.Split(',');
            for (int i = 0; i < tiles.Length; i++) {
                string id = tiles[i].Trim();
                if (string.IsNullOrEmpty(id)) data[i] = 0;
                else data[i] = uint.Parse(id);
            }
        }
        else if (encoding == "base64") {
            byte[] bytes = Convert.FromBase64String(dataString);
            Stream stream = new MemoryStream(bytes, false);

            if (compression == "gzip") {
                stream = new GZipStream(stream, CompressionMode.Decompress);
            }
            else if (compression == "zlib") {
                stream = new MemoryStream(bytes, 2, bytes.Length-2, false);
                stream = new DeflateStream(stream, CompressionMode.Decompress);
            }
            
            using (BinaryReader binaryReader = new BinaryReader(stream)) {
                for (int j = 0; j < height; j++) {
                    for (int i = 0; i < width; i++) {
                        data[i + j * width] = binaryReader.ReadUInt32();
                    }
                }
            }

            stream.Dispose();
        }
        return data;
    }
}

[System.Serializable]
public class Chunk {
    [XmlAttribute("x")] public int x;
    [XmlAttribute("y")] public int y;
    [XmlAttribute("width")] public int width;
    [XmlAttribute("height")] public int height;

    [XmlText] public string dataString = "";
    [XmlIgnore][NonSerialized] public uint[] contentData;
}

[System.Serializable]
public struct TilePoint {
    [XmlAttribute("x")] public float x; 
    [XmlAttribute("y")] public float y;

    public TilePoint (float x, float y) {
        this.x = x;
        this.y = y;
    }
}

[System.Serializable]
public struct TileRect {
    [XmlAttribute("x")] public float x;
    [XmlAttribute("y")] public float y;
    [XmlAttribute("width")] public float width;
    [XmlAttribute("height")] public float height;

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
    [XmlIgnore] public bool nameSpecified { get { return !string.IsNullOrEmpty(name); } set {} }

    [XmlAttribute("color")] public string color;
    [XmlIgnore] public bool colorSpecified { get { return !string.IsNullOrEmpty(color); } set {} }

    [XmlAttribute("opacity")] public float? opacity;
    [XmlAttribute("visible")] public int? visible;
    [XmlAttribute("offsetx")] public int? offsetX;
    [XmlAttribute("offsety")] public int? offsetY;
    [XmlAttribute("draworder")] public string drawOrder;

    [XmlArray("properties")] [XmlArrayItem("property", typeof(Property))] public Property[] properties;
    [XmlIgnore] public bool propertiesSpecified { get { return properties != null && properties.Length > 0; } set { } }
    [XmlElement("object", typeof(TileObject))] public TileObject[] objects;
}

[System.Serializable]
public class TileObject {
    [XmlAttribute("id")] public int id = 0;
    [XmlAttribute("name")] public string name;
    [XmlIgnore] public bool nameSpecified { get { return !string.IsNullOrEmpty(name); } set {} }
    [XmlAttribute("type")] public string type;
    [XmlIgnore] public bool typeSpecified { get { return !string.IsNullOrEmpty(type); } set {} }
    [XmlAttribute("gid")] public uint? gid;
    [XmlAttribute("x")] public float x = 0;
    [XmlAttribute("y")] public float y = 0;
    [XmlAttribute("width")] public float width = 0;
    [XmlIgnore] public bool widthSpecified { get { return width > 0; } set {} }
    [XmlAttribute("height")] public float height = 0;
    [XmlIgnore] public bool heightSpecified { get { return height > 0; } set {} }
    [XmlAttribute("rotation")] public float? rotation;
    [XmlAttribute("visible")] public int? visible;

    [XmlArray("properties")] [XmlArrayItem("property", typeof(Property))] public Property[] properties;
    [XmlIgnore] public bool propertiesSpecified { get { return properties != null && properties.Length > 0; } set { } }
    [XmlElement("ellipse")] public string ellipse;
    [XmlIgnore] public bool ellipseSpecified { get { return ellipse != null; } set {} }
    [XmlElement("polygon", typeof(Polygon))] public Polygon polygon;
    [XmlIgnore] public bool polygonSpecified { get { return polygon != null && !string.IsNullOrEmpty(polygon.points); } set {} }
    [XmlElement("polyline", typeof(Polygon))] public Polygon polyline;
    [XmlIgnore] public bool polylineSpecified { get { return polyline != null && !string.IsNullOrEmpty(polyline.points); } set {} }
    [XmlElement("image", typeof(Image))] public Image image;
    [XmlIgnore] public bool imageSpecified { get { return image != null && !string.IsNullOrEmpty(image.source); } set {} }
}

[System.Serializable]
public class Polygon {
    [XmlAttribute("points")] public string points = "";
    [XmlIgnore] public TilePoint[] path {
        get {
            string[] pts = points.Split(' ');
            TilePoint[] _path = new TilePoint[pts.Length];
            for (int i = 0; i < pts.Length; i++) {
                string[] pt = pts[i].Trim().Split(',');
                _path[i] = new TilePoint(float.Parse(pt[0]), float.Parse(pt[1]));
            }
            return _path;
        }
    }

}

[System.Serializable]
public class ImageLayer {
    [XmlAttribute("name")] public string name = "";
    [XmlAttribute("offsetx")] public int offsetX = 0;
    [XmlAttribute("offsety")] public int offsetY = 0;
    [XmlAttribute("opacity")] public float opacity = 1;
    [XmlAttribute("visible")] public int visible = 1;

    [XmlArray("properties")] [XmlArrayItem("property", typeof(Property))] public Property[] properties;
    [XmlIgnore] public bool propertiesSpecified { get { return properties != null && properties.Length > 0; } set { } }
    [XmlElement("image", typeof(Image))] public Image image;
}


[System.Serializable]
public class WangSet {
    [XmlAttribute("name")] public string name = "";
    [XmlAttribute("tile")] public int tileID = 0;
    [XmlElement("wangcornercolor", typeof(WangColor))] public WangColor[] cornerColors;
    [XmlElement("wangedgecolor", typeof(WangColor))] public WangColor[] edgeColors;
    [XmlElement("wangtile", typeof(WangTile))] public WangTile[] tiles;
}

[System.Serializable]
public class WangColor {
    [XmlAttribute("name")] public string name = "";
    [XmlAttribute("color")] public string color = "";
    [XmlAttribute("tile")] public int tileID = 0;
    [XmlAttribute("probability")] public float probability = 1;
}

[System.Serializable]
public class WangTile {
    [XmlAttribute("tileid")] public int tileID = 0;
    [XmlAttribute("wangid")] public string _wangIDStr = "0x00000000";
    [XmlIgnore] public UInt32? _wangID;
    [XmlIgnore] public UInt32 wangID {
        get { 
            if (_wangID == null) {
                _wangID = UInt32.Parse(
                    _wangIDStr.Replace("0x", ""), 
                    System.Globalization.NumberStyles.HexNumber
                );
            }
            return _wangID.Value;
        }
        set { 
            if (value != _wangID.Value) {
                _wangID = value;
                _wangIDStr = "0x" + value.ToString("X"); 
            }
        }
    }
}
}