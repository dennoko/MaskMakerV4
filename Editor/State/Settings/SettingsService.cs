using System;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// 設定の唯一の入口（Design state_and_lifecycle §1.2）。
    /// 永続層（Target / Project / User）はファイルの遅延読み込みキャッシュなので、ドメインリロード後は読み直すだけでよい。
    /// </summary>
    public static class SettingsService
    {
        private static readonly TargetLayer Target = new TargetLayer();
        private static readonly ProjectLayer Project = new ProjectLayer();
        private static readonly UserLayer User = new UserLayer();

        /// <summary>値が変わった設定（null = 全部が変わった可能性がある）。</summary>
        public static event Action<Setting> Changed;

        public static int Revision { get; private set; }

        private static ISettingsLayer Layer(SettingLayer layer)
        {
            switch (layer)
            {
                case SettingLayer.Session: return SessionLayer.instance;
                case SettingLayer.Target: return Target;
                case SettingLayer.Project: return Project;
                default: return User;
            }
        }

        public static T Get<T>(Setting<T> s) => GetFrom(s, SettingLayer.Session);

        /// <summary>from の層から下だけで解決した値（上書きマークや設定画面の表示に使う）。</summary>
        public static T GetFrom<T>(Setting<T> s, SettingLayer from)
        {
            for (int l = (int)from; l <= (int)SettingLayer.User; l++)
            {
                if (Layer((SettingLayer)l).TryGet(s.Key, out string text) && SettingCodec.TryParse(text, out T value))
                    return value;
            }
            return s.Default;
        }

        public static void Set<T>(Setting<T> s, T value) => Set(s, value, s.WriteLayer);

        public static void Set<T>(Setting<T> s, T value, SettingLayer layer)
        {
            if (Layer(layer).Set(s.Key, SettingCodec.Format(value))) Raise(s);
        }

        /// <summary>書き込み先の層に値があり、下の層の値と違う時 true（上書きマーク ●）。</summary>
        public static bool IsOverridden<T>(Setting<T> s)
        {
            if (!Layer(s.WriteLayer).TryGet(s.Key, out string text) || !SettingCodec.TryParse(text, out T value)) return false;
            if (s.WriteLayer == SettingLayer.User) return !Equals(value, s.Default);
            return !Equals(value, GetFrom(s, s.WriteLayer + 1));
        }

        /// <summary>書き込み先の層の値を消して、下の層の値に戻す。</summary>
        public static void ResetToLower(Setting s) => Remove(s, s.WriteLayer);

        public static void Remove(Setting s, SettingLayer layer)
        {
            if (Layer(layer).Remove(s.Key)) Raise(s);
        }

        /// <summary>作業開始時（再開を除く）に Session 層を空にする。</summary>
        public static void ClearSession()
        {
            SessionLayer.instance.Clear();
            Raise(null);
        }

        public static void LoadTarget(string targetKey)
        {
            Target.Load(targetKey);
            Raise(null);
        }

        public static void UnloadTarget()
        {
            Target.Unload();
            Raise(null);
        }

        public static void FlushTarget() => Target.Flush();

        public static void ReloadProjectIfChanged()
        {
            if (Project.ReloadIfChanged()) Raise(null);
        }

        public static void ResetHintCounts()
        {
            User.RemoveByPrefix(SettingKeys.HintCountPrefix);
            Raise(null);
        }

        private static void Raise(Setting s)
        {
            Revision++;
            Changed?.Invoke(s);
        }
    }
}
