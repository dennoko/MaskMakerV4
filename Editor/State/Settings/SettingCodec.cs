using System;
using System.Globalization;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>設定値 ⇄ 文字列（InvariantCulture）。</summary>
    internal static class SettingCodec
    {
        public static string Format<T>(T value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case string s: return s;
                case Color c: return "#" + ColorUtility.ToHtmlStringRGBA(c);
                case Enum e: return e.ToString();
                default: return value.ToString();
            }
        }

        public static bool TryParse<T>(string text, out T value)
        {
            value = default;
            if (text == null) return false;
            var type = typeof(T);
            object result = null;
            if (type == typeof(bool))
            {
                if (text == "true") result = true;
                else if (text == "false") result = false;
            }
            else if (type == typeof(int))
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) result = i;
            }
            else if (type == typeof(float))
            {
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) result = f;
            }
            else if (type == typeof(string))
            {
                result = text;
            }
            else if (type == typeof(Color))
            {
                if (ColorUtility.TryParseHtmlString(text, out Color c)) result = c;
            }
            else if (type.IsEnum)
            {
                try { result = Enum.Parse(type, text); } catch (ArgumentException) { }
            }
            if (result == null) return false;
            value = (T)result;
            return true;
        }
    }
}
