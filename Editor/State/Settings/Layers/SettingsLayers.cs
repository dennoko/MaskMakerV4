using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    internal interface ISettingsLayer
    {
        bool TryGet(string key, out string value);
        bool Set(string key, string value);
        bool Remove(string key);
    }

    /// <summary>Session 層。ScriptableSingleton に置くのでドメインリロードをまたいで残る。</summary>
    internal sealed class SessionLayer : ScriptableSingleton<SessionLayer>, ISettingsLayer
    {
        [SerializeField] private List<string> keys = new List<string>();
        [SerializeField] private List<string> values = new List<string>();

        public bool TryGet(string key, out string value)
        {
            int i = keys.IndexOf(key);
            value = i >= 0 ? values[i] : null;
            return i >= 0;
        }

        public bool Set(string key, string value)
        {
            int i = keys.IndexOf(key);
            if (i >= 0)
            {
                if (values[i] == value) return false;
                values[i] = value;
                return true;
            }
            keys.Add(key);
            values.Add(value);
            return true;
        }

        public bool Remove(string key)
        {
            int i = keys.IndexOf(key);
            if (i < 0) return false;
            keys.RemoveAt(i);
            values.RemoveAt(i);
            return true;
        }

        public void Clear()
        {
            keys.Clear();
            values.Clear();
        }
    }

    /// <summary>Target 層。作業中の対象のファイルだけをメモリに持つ。</summary>
    internal sealed class TargetLayer : ISettingsLayer
    {
        private readonly KeyValueStore _store = new KeyValueStore();
        public string TargetKey { get; private set; }

        public void Load(string targetKey)
        {
            TargetKey = targetKey;
            _store.Clear();
            var path = StoragePaths.TargetSettings(targetKey);
            if (File.Exists(path)) _store.LoadJson(SafeRead(path));
        }

        public void Unload()
        {
            Flush();
            TargetKey = null;
            _store.Clear();
        }

        public bool TryGet(string key, out string value) => _store.TryGet(key, out value);

        public bool Set(string key, string value)
        {
            if (!_store.Set(key, value)) return false;
            ScheduleSave();
            return true;
        }

        public bool Remove(string key)
        {
            if (!_store.Remove(key)) return false;
            ScheduleSave();
            return true;
        }

        public void Flush() => SaveScheduler.Flush(SaveJobId);

        private string SaveJobId => "target-settings";

        private void ScheduleSave()
        {
            if (string.IsNullOrEmpty(TargetKey)) return;
            string key = TargetKey;
            SaveScheduler.Schedule(SaveJobId, 0.5,
                () => AtomicFile.WriteAllText(StoragePaths.TargetSettings(key), _store.ToJson()));
        }

        internal static string SafeRead(string path)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) { return null; }
        }
    }

    /// <summary>Project 層（ProjectSettings/UVMaskMaker.json）。</summary>
    internal sealed class ProjectLayer : ISettingsLayer
    {
        private readonly KeyValueStore _store = new KeyValueStore();
        private DateTime _loadedStamp;
        private bool _loaded;

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            var path = StoragePaths.ProjectSettings;
            if (File.Exists(path))
            {
                _store.LoadJson(TargetLayer.SafeRead(path));
                _loadedStamp = File.GetLastWriteTimeUtc(path);
            }
        }

        /// <summary>外部（バージョン管理など）で書き換わっていれば読み直す。変わったら true。</summary>
        public bool ReloadIfChanged()
        {
            var path = StoragePaths.ProjectSettings;
            if (!_loaded || !File.Exists(path)) return false;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (stamp == _loadedStamp) return false;
            _loaded = false;
            EnsureLoaded();
            return true;
        }

        public bool TryGet(string key, out string value)
        {
            EnsureLoaded();
            return _store.TryGet(key, out value);
        }

        public bool Set(string key, string value)
        {
            EnsureLoaded();
            if (!_store.Set(key, value)) return false;
            Save();
            return true;
        }

        public bool Remove(string key)
        {
            EnsureLoaded();
            if (!_store.Remove(key)) return false;
            Save();
            return true;
        }

        private void Save()
        {
            AtomicFile.WriteAllText(StoragePaths.ProjectSettings, _store.ToJson());
            _loadedStamp = File.GetLastWriteTimeUtc(StoragePaths.ProjectSettings);
        }
    }

    /// <summary>User 層（EditorPrefs）。</summary>
    internal sealed class UserLayer : ISettingsLayer
    {
        private const string PrefsKey = "UVMaskMaker.User";
        private readonly KeyValueStore _store = new KeyValueStore();
        private bool _loaded;

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _store.LoadJson(EditorPrefs.GetString(PrefsKey, string.Empty));
        }

        public bool TryGet(string key, out string value)
        {
            EnsureLoaded();
            return _store.TryGet(key, out value);
        }

        public bool Set(string key, string value)
        {
            EnsureLoaded();
            if (!_store.Set(key, value)) return false;
            EditorPrefs.SetString(PrefsKey, _store.ToJson());
            return true;
        }

        public bool Remove(string key)
        {
            EnsureLoaded();
            if (!_store.Remove(key)) return false;
            EditorPrefs.SetString(PrefsKey, _store.ToJson());
            return true;
        }

        public void RemoveByPrefix(string prefix)
        {
            EnsureLoaded();
            var remove = new List<string>();
            foreach (var kv in _store.Entries) if (kv.Key.StartsWith(prefix, StringComparison.Ordinal)) remove.Add(kv.Key);
            foreach (var k in remove) _store.Remove(k);
            EditorPrefs.SetString(PrefsKey, _store.ToJson());
        }
    }
}
