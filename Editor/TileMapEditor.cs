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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Linq;

namespace Tiled {
[CustomEditor(typeof(TileMap))]
public class TileMapEditor : Editor {
    
    private TileMap tileMap {
        get { return (target as TileMap); }
    }

    private TMXFile tmxFile {
        get { return tileMap.tmxFile; }
        set { tileMap.tmxFile = value; }
    }

    private string tmxFileString {
        get { return tileMap.tmxFileString; }
        set { tileMap.tmxFileString = value; }
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
    

    [SerializeField] TreeViewState treeViewState;
    TileMapTreeView treeView;

    void OnEnable () {
        Undo.undoRedoPerformed += UndoRedo;
        if (tmxFile == null) {
            tileMap.tmxFile = TMXFile.Load(path);
            tileMap.Setup();
        }
        if (treeViewState == null) treeViewState = new TreeViewState ();
        treeView = new TileMapTreeView(tileMap, treeViewState);
    }
    
    void OnDisable () {
        Undo.undoRedoPerformed -= UndoRedo;
    }

    void UndoRedo () {
        if (target != null) {
            tileMap.tmxFile = TMXFile.Load(tileMap.tmxFileString, path);
            tileMap.ReloadMap();
            treeView.Reload();
        }
    }   

    private static Material _mat;
    private static Material mat {
        get {
            if (_mat == null) _mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            return _mat;
        }
    }

    private static Dictionary<string, Texture2D> tileSetTextures = new Dictionary<string, Texture2D>();
    private static Dictionary<string, Material> tileSetMaterials = new Dictionary<string, Material>();
    public static Material[] GetMaterials (TMXFile tmxFile, string path) {
        Material[] materials = new Material[tmxFile.tileSets.Length];
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            TileSet tileSet = tmxFile.tileSets[i];
            if (tileSet == null || tileSet.image == null || string.IsNullOrEmpty(tileSet.image.source)) continue;

            Material mat = null;
            if (tileSetMaterials.ContainsKey(tileSet.image.source)) {
                mat = tileSetMaterials[tileSet.image.source];
            }
            else {
                string materialPath = Path.Combine(Path.GetDirectoryName(tileSet.image.source), "Materials");
                materialPath = Path.Combine(materialPath, Path.GetFileNameWithoutExtension(tileSet.image.source) + ".mat");
                materialPath = Path.Combine(Path.GetDirectoryName(path), materialPath);
                materialPath = Path.GetFullPath(materialPath);
                string materialDir = Path.GetDirectoryName(materialPath);                
                Directory.CreateDirectory(materialDir);
                string dataPath = Path.GetFullPath(Application.dataPath);
                materialPath = materialPath.Replace(dataPath, "Assets");
                mat = AssetDatabase.LoadAssetAtPath(materialPath, typeof(Material)) as Material;
                if (mat == null) {
                    mat = new Material(Shader.Find("Unlit/Transparent"));
                    mat.mainTexture = GetTileSetTexture(tileSet, path);
                    AssetDatabase.CreateAsset(mat, materialPath);
                }
            }
            if (mat != null) tileSetMaterials[tileSet.image.source] = mat;
            
            materials[i] = mat;
        }
        return materials;
    }

    public static Sprite[][] GetSprites (TMXFile tmxFile, string path) {
        Sprite[][] tileSetSprites = new Sprite[tmxFile.tileSets.Length][];
        for (int i = 0; i < tmxFile.tileSets.Length; i++) {
            TileSet tileSet = tmxFile.tileSets[i];
            if (tileSet != null && tileSet.image != null && !string.IsNullOrEmpty(tileSet.image.source)) continue;

            List<Sprite> sprites = new List<Sprite>();
            foreach (Tile tile in tileSet.tiles) {
                if (tile.image == null || string.IsNullOrEmpty(tile.image.source)) continue;
                string texturePath = Path.Combine(Path.GetDirectoryName(path), tile.image.source);
                Texture2D tex = GetImageTexture(tile.image, texturePath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(tex));
                sprites.Add(sprite);
            }
            tileSetSprites[i] = sprites.ToArray();
        }
        return tileSetSprites;
    }

    public static Texture2D GetTileSetTexture (TileSet tileSet, string path) {
        if (tileSet.image == null || tileSet.image.source == null) return null;

        string texturePath = tileSet.image.source;
        if (tileSet.source == null) {
            texturePath = Path.Combine(Path.GetDirectoryName(path), texturePath);
        }
        else {
            string tileSetPath = Path.Combine(Path.GetDirectoryName(path), tileSet.source);
            texturePath = Path.Combine(Path.GetDirectoryName(tileSetPath), texturePath);
        }
        return GetImageTexture(tileSet.image, texturePath);
    }

    public static Texture2D GetImageTexture (Image image, string texturePath) {
        if (tileSetTextures.ContainsKey(image.source)) return tileSetTextures[image.source];

        texturePath = Path.GetFullPath(texturePath);
        string dataPath = Path.GetFullPath(Application.dataPath);
        texturePath = texturePath.Replace(dataPath, "Assets");
        Texture2D tex = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;

        if (tex != null) tileSetTextures[image.source] = tex;

        return tex;
    }

    public static TileSet selectedTileSet;
    public static int selectedTileIndex;
    private int GetTileIndex (TileSet tileSet, Rect rect, Vector2 pos) {
        pos -= rect.min;
        pos.x /= rect.width;
        pos.y /= rect.height;
        int i = tileSet.firstGID;
        i += Mathf.FloorToInt(pos.y * tileSet.rows) * tileSet.columns + Mathf.FloorToInt(pos.x * tileSet.columns);
        return i;
    }

    private static int selectedLayerID;
    public static Layer selectedLayer;
    public static int selectedTerrainIndex = 0;

    Rect tileRect;
    private void TileSetField (TileSet tileSet) {
        if (tileSet != null && tileSet.image != null && !string.IsNullOrEmpty(tileSet.image.source)) TileSetTextureField(tileSet);
        else TileSetSpriteCollectionField(tileSet);
    }

    private void TileSetSpriteCollectionField (TileSet tileSet) {

    }

    private void TileSetTextureField (TileSet tileSet) {
        int id = tileSet.firstGID;        
        if (selectedTileSet == null) tileRect = new Rect(-1,-1,0,0);

        Texture2D currentTexture = GetTileSetTexture(tileSet, path);
        Texture2D tex = EditorGUILayout.ObjectField(currentTexture, typeof(Texture2D), false) as Texture2D;
        if (currentTexture != tex) {
            Uri textureURI = new Uri("/" + AssetDatabase.GetAssetPath(tex));
            Uri tmxFileURI = new Uri("/" + Path.GetDirectoryName(path));
            tileSet.image.source = "../" + tmxFileURI.MakeRelativeUri(textureURI);
        }

        if (tex == null) return;
        
        float x = Screen.width - 40;
        float y = tex.height * x / (float)tex.width;
        if (x > tex.width) {
            x = tex.width;
            y = tex.height;
        }
        Rect r = GUILayoutUtility.GetRect(x, y);
        r.width = r.height * (float)tex.width / (float)tex.height;
        r.height = r.width * (float)tex.height / (float)tex.width;
        r.x = (Screen.width - r.width) * 0.5f;
        EditorGUI.DrawPreviewTexture(r, tex, mat);

        if (selectedTileSet != null && selectedTileSet == tileSet && selectedTileIndex > 0) {
            TileRect uvTileRect = selectedTileSet.GetTileUVs(selectedTileIndex);
            tileRect = r;
            tileRect.x += uvTileRect.x * r.width;
            tileRect.y += (1 - (uvTileRect.y + uvTileRect.height)) * r.height;
            tileRect.width *= uvTileRect.width;
            tileRect.height *= uvTileRect.height;
        }

        Handles.DrawSolidRectangleWithOutline(tileRect, Color.clear, Color.white);

        if (Event.current.type == EventType.MouseDown && 
            Event.current.button == 0 && 
            r.Contains(Event.current.mousePosition)) {
            selectedTileSet = tileSet;
            selectedTileIndex = GetTileIndex(tileSet, r, Event.current.mousePosition);
            Event.current.Use();
        }
    }

    public static int editState = 0;
    public static int paintType = 0;
    public static int selectedTileSetIndex = 0;
    public override void OnInspectorGUI() {
        DefaultAsset asset = AssetDatabase.LoadAssetAtPath(path, typeof(DefaultAsset)) as DefaultAsset;
        asset = EditorGUILayout.ObjectField("TMX File", asset, typeof(DefaultAsset), false) as DefaultAsset;
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (path != assetPath && Path.GetExtension(assetPath) == ".tmx") {
            Undo.RecordObject(target, "Assign new TMX file");
            path = assetPath;
            tmxFile = TMXFile.Load(assetPath);
            tileMap.Setup();
        }
        if (path != null) DrawFilePanel();

        tileMap.pivot = EditorGUILayout.Vector2Field("Pivot", tileMap.pivot);
        DrawLayers();
        DrawTileSets();
    }

    void DrawFilePanel() {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reload")) {
            tmxFile = TMXFile.Load(path);
            tileMap.tileSetMaterials = TileMapEditor.GetMaterials(tmxFile, path);
            tileMap.tileSetSprites = TileMapEditor.GetSprites(tmxFile, path);
            tileMap.Setup();
            treeView.Reload();
        }
        if (GUILayout.Button("Save")) {
            tmxFile.Save(path);
            AssetDatabase.ImportAsset(path);
        }
        if (GUILayout.Button("Save As")) {
            tmxFile.Save(
                EditorUtility.SaveFilePanel(
                    "Save as TMX",
                    Path.GetDirectoryName(path),
                    Path.GetFileNameWithoutExtension(path),
                    Path.GetExtension(path).TrimStart(new char[]{'.'})
                )
            );
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(path);
        }
        EditorGUILayout.EndHorizontal();
    }

    public void DrawLayers () {
        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
        Rect r = GUILayoutUtility.GetRect(Screen.width, EditorGUIUtility.singleLineHeight * 5);
        if (treeView == null) treeView = new TileMapTreeView(tileMap, treeViewState);
        treeView.OnGUI(r);
    }

    void DrawTileSets () {
        if (tmxFile.tileSets == null) return;
    
        if (tileMap.tileSetMaterials == null || tileMap.tileSetMaterials.Length != tmxFile.tileSets.Length) {
            tileMap.tileSetMaterials = TileMapEditor.GetMaterials(tmxFile, path);
            tileMap.tileSetSprites = TileMapEditor.GetSprites(tmxFile, path);
        }
        
        EditorGUILayout.LabelField("Tile Sets", EditorStyles.boldLabel);
        string[] tilesSetNames = Array.ConvertAll(tmxFile.tileSets, (t) => t.name);
        selectedTileSetIndex = GUILayout.Toolbar(selectedTileSetIndex, tilesSetNames);

        switch (paintType) {
            case 0:
                TileSetField(tmxFile.tileSets[selectedTileSetIndex]);
                break;
            case 1:
                string[] terrainNames = Array.ConvertAll(terrains, (terrain) => terrain.name);
                selectedTerrainIndex = GUILayout.SelectionGrid(selectedTerrainIndex, terrainNames, 1);
                break;
            default:
                break;
        }

        paintType = GUILayout.Toolbar(paintType, new string[] {"Tiles", "Terrains"});
        EditorGUILayout.Separator();
    }

    public override bool HasPreviewGUI() {
        return selectedTileSet != null;
    }

    public override GUIContent GetPreviewTitle() {
        return new GUIContent(selectedTileSet.name + " - Tile: " + selectedTileIndex);
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background) {
        if (Event.current.type != EventType.Repaint) return;

        Texture2D tex = GetTileSetTexture(selectedTileSet, path);
        TileRect uvTileRect = selectedTileSet.GetTileUVs(selectedTileIndex);

        Rect uvRect = new Rect(uvTileRect.x, uvTileRect.y, uvTileRect.width, uvTileRect.height);
        if (r.height > r.width) {
            r.height = r.width;
            r.x += (r.height - r.width) * 0.5f;
        }
        else if (r.width > r.height) {
            r.width = r.height;
            r.y += (r.width - r.height) * 0.5f;
        }
        r.x = (Screen.width - r.width) * 0.5f;
        GUI.DrawTextureWithTexCoords(r, tex, uvRect, true);

        Tile t = selectedTileSet.GetTile(selectedTileIndex);
        if (t != null && t.objectGroup != null && t.objectGroup.objects != null && t.objectGroup.objects.Length > 0) {
            foreach (TileObject obj in t.objectGroup.objects) {
                if (obj.polygonSpecified) {
                    TilePoint[] path = obj.polygon.path;
                    Vector3[] poly = System.Array.ConvertAll(path, (p) => {
                        Vector3 v = new Vector3(p.x + obj.x, p.y + obj.y, 0);
                        v.x *= r.width / (float)tmxFile.tileWidth;
                        v.y *= r.height / (float)tmxFile.tileHeight;
                        v.x += r.x;
                        v.y += r.y;
                        return v;
                    });
                    Handles.color = new Color(1,1,1,0.1f);
                    Handles.DrawAAConvexPolygon(poly);
                }
            }
        }
    }

    class TileMapTreeViewItem : TreeViewItem {
        public Layer layer;
    }

    class TileMapTreeView : TreeView {

        static Texture2D visibleIcon = EditorGUIUtility.IconContent("VisibilityOn").image as Texture2D;
        static Texture2D invisibleIcon = EditorGUIUtility.IconContent("VisibilityOff").image as Texture2D;
        static Texture2D lockedIcon = EditorGUIUtility.IconContent("IN LockButton on").image as Texture2D;
        static Texture2D unlockedIcon = EditorGUIUtility.IconContent("IN LockButton").image as Texture2D;
        static Texture2D tileLayerIcon = EditorGUIUtility.IconContent("Tilemap Icon").image as Texture2D;
        static Texture2D objectGroupIcon = EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D;
        static Texture2D imageLayerIcon = EditorGUIUtility.IconContent("RawImage Icon").image as Texture2D;
        static Texture2D groupLayerIcon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;

        TileMap tileMap;
        TileMapTreeViewItem root;

        public TileMapTreeView(TileMap tileMap, TreeViewState treeViewState) : base(treeViewState) {
            this.tileMap = tileMap;
            Reload();
        }

        void AddLayerItem (TileMapTreeViewItem parent, Layer layer) {
            TileMapTreeViewItem item = new TileMapTreeViewItem {id = layer.id, displayName = layer.name, layer = layer};
            parent.AddChild(item);
            if (layer is GroupLayer) {
                GroupLayer group = layer as GroupLayer;
                for (int i = group.layers.Count-1; i >= 0; i--) {
                    AddLayerItem(item, group.layers[i] as Layer);
                }
            }
        }
            
        protected override TreeViewItem BuildRoot () {
            root = new TileMapTreeViewItem {id = -1, depth = -1, displayName = "TileMap"};
            for (int i = tileMap.tmxFile.layers.Count-1; i >= 0; i--) {
                AddLayerItem(root, tileMap.tmxFile.layers[i] as Layer);
            }            
            SetupDepthsFromParentsAndChildren(root);   
            return root;
        }

        protected override void SelectionChanged(IList<int> selectedIds) {
            if (selectedIds.Count == 0) return;
            int id = selectedIds[0];
            TileMapTreeViewItem item = FindItem(id, root) as TileMapTreeViewItem; 
            if (item != null && item.layer != null) {
                // Debug.Log(item.layer.name);
                selectedLayer = item.layer;
                selectedLayerID = item.id;
            }
            else if (item == null) {
                List<int> ids = new List<int>();
                ids.Add(selectedLayerID);
                SetSelection(ids);
            }
        }

        protected override void RowGUI (RowGUIArgs args) {
            TileMapTreeViewItem item = args.item as TileMapTreeViewItem;
            Layer l = item.layer;
            Rect r = args.rowRect;
            float indent = GetContentIndent(args.item);
            r.x += indent;
            r.width -= EditorGUIUtility.singleLineHeight * 2 + indent;
            GUIContent c = new GUIContent(l.name);
            if (l is TileLayer) c.image = tileLayerIcon;
            else if (l is ObjectGroup) c.image = objectGroupIcon;
            else if (l is ImageLayer) c.image = imageLayerIcon;
            else if (l is GroupLayer) c.image = groupLayerIcon;
            GUI.Label(r, c);

            GUIStyle visStyle = new GUIStyle(GUI.skin.toggle);
            visStyle.normal.background = invisibleIcon;
            visStyle.onNormal.background = visibleIcon;
            r.x += r.width;
            r.width = EditorGUIUtility.singleLineHeight;
            bool visible = GUI.Toggle(r, l.visible, "", visStyle);
            if (l.visible != visible) {
                l.visible = visible;
                tileMap.UpdateVisible();
            }

            Color color = GUI.color;
            GUI.color = Color.black;
            GUIStyle lockStyle = new GUIStyle(GUI.skin.toggle);
            lockStyle.normal.background = unlockedIcon;
            lockStyle.onNormal.background = lockedIcon;  
            r.x += r.width;
            l.locked = GUI.Toggle(r, l.locked, "", lockStyle);  
            GUI.color = color;
        }
    }
}
}