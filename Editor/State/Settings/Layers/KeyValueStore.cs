using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    /// <summary>設定層の中身。JSON は {"version":1,"values":[{"key":..,"value":..}]}。</summary>
    internal sealed class KeyValueStore
    {
        public const int FormatVersion = 1;

        [Serializable]
        private class Entry
        {
            public string key;
            public string value;
        }

        [Serializable]
        private class Document
        {
            public int version = FormatVersion;
            public List<Entry> values = new List<Entry>();
        }

        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public bool TryGet(string key, out string value) => _values.TryGetValue(key, out value);

        /// <summary>値が変わった時だけ true。</summary>
        public bool Set(string key, string value)
        {
            if (_values.TryGetValue(key, out var old) && old == value) return false;
            _values[key] = value;
            return true;
        }

        public bool Remove(string key) => _values.Remove(key);

        public void Clear() => _values.Clear();

        public int Count => _values.Count;

        public IEnumerable<KeyValuePair<string, string>> Entries => _values;

        public string ToJson()
        {
            var doc = new Document();
            foreach (var kv in _values) doc.values.Add(new Entry { key = kv.Key, value = kv.Value });
            doc.values.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            return JsonUtility.ToJson(doc, true);
        }

        /// <summary>知らないキーもそのまま保持する。形式が新しすぎる・壊れている時は false（中身は空）。</summary>
        public bool LoadJson(string json)
        {
            _values.Clear();
            if (string.IsNullOrEmpty(json)) return true;
            Document doc;
            try { doc = JsonUtility.FromJson<Document>(json); }
            catch (ArgumentException) { return false; }
            if (doc == null || doc.version > FormatVersion) return false;
            if (doc.values == null) return true;
            foreach (var e in doc.values)
                if (!string.IsNullOrEmpty(e.key)) _values[e.key] = e.value ?? string.Empty;
            return true;
        }
    }
}
