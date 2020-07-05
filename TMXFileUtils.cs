/*
Copyright (C) 2020 Nolan Baker

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
using System.IO;
using System.Reflection;
using UnityEngine;

public class TMXFileUtils {

    public static void SetProperties (GameObject g, Tiled.Property[] props, Dictionary<int, GameObject> objectMap = null) {
        if (props == null) return;
        foreach (Tiled.Property prop in props) {
            if (prop == null || string.IsNullOrEmpty(prop.name)) continue;
            string[] classVar = prop.name.Split('.');
            UnityEngine.Object o = g as UnityEngine.Object;
            System.Type type = System.Type.GetType(classVar[0]);
            if (type != null && classVar[0] == "GameObject") {
                if (classVar[1] == "layer" && !prop.typeSpecified) {
                    g.layer = LayerMask.NameToLayer(prop.val);
                    continue;
                }
            }
            else {
                if (type == null) type = System.Type.GetType("UnityEngine.Rendering."+classVar[0]+",UnityEngine");
                if (type == null) type = System.Type.GetType("UnityEngine.Tilemaps."+classVar[0]+",UnityEngine");
                if (type == null) continue;
                Component c = g.GetComponent(type);
                if (c == null) c = g.AddComponent(type);
                o = c as UnityEngine.Object;
            }
            if (classVar.Length < 2) continue;
            FieldInfo fieldInfo = type.GetField(classVar[1], BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null) {
                switch (prop.type) {
                    case "float":
                        fieldInfo.SetValue(o, float.Parse(prop.val));
                        break;
                    case "int":
                        fieldInfo.SetValue(o, int.Parse(prop.val));
                        break;
                    case "bool":
                        fieldInfo.SetValue(o, bool.Parse(prop.val));
                        break;
                    case "color":
                        fieldInfo.SetValue(o, TiledColorFromString(prop.val));
                        break;
                    case "object":
                        if (objectMap == null) break;
                        int id = int.Parse(prop.val);
                        if (!objectMap.ContainsKey(id)) break;
                        fieldInfo.SetValue(o, objectMap[id]);
                        break;
                    default:
                        fieldInfo.SetValue(o, prop.val);
                        break;
                }
                continue;
            }
            PropertyInfo propInfo = type.GetProperty(classVar[1], BindingFlags.Public | BindingFlags.Instance);
            if (propInfo != null) {
                switch (prop.type) {
                    case "float":
                        propInfo.SetValue(o, float.Parse(prop.val));
                        break;
                    case "int":
                        propInfo.SetValue(o, int.Parse(prop.val));
                        break;
                    case "bool":
                        propInfo.SetValue(o, bool.Parse(prop.val));
                        break;
                    case "color":
                        propInfo.SetValue(o, TiledColorFromString(prop.val));
                        break;
                    case "object":
                        if (objectMap == null) break;
                        int id = int.Parse(prop.val);
                        if (!objectMap.ContainsKey(id)) break;
                        propInfo.SetValue(o, objectMap[id]);
                        break;
                    default:
                        propInfo.SetValue(o, prop.val);
                        break;
                }
                continue;
            }
        }
    }

    public static Color TiledColorFromString (string colorStr) {
        Color color = Color.white;
        if (colorStr == null) return color;
        if (colorStr.Length > 8) colorStr = "#" + colorStr.Substring(3) + colorStr.Substring(1, 2);
        else colorStr = "#" + colorStr;
        ColorUtility.TryParseHtmlString(colorStr, out color); 
        return color;
    }

    public static string TiledColorToString (Color color, bool allowWhite = false) {
        if (color == Color.white && !allowWhite) return null;
        if (color.a == 1) return "#" + ColorUtility.ToHtmlStringRGB(color).ToLower();
        string colorStr = ColorUtility.ToHtmlStringRGBA(color).ToLower();;
        colorStr = "#" + colorStr.Substring(6, 2) + colorStr.Substring(0, 6);
        return colorStr;
    }
}
