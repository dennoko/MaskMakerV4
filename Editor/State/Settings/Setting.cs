namespace Dennoko.UVMaskMaker
{
    /// <summary>設定の層。値は Session → Target → Project → User → 既定 の順に解決する。</summary>
    public enum SettingLayer
    {
        Session = 0,
        Target = 1,
        Project = 2,
        User = 3,
    }

    /// <summary>設定キーの宣言。書き込み先の層を 1 つ持つ（Impl work_context_and_rendering §4）。</summary>
    public abstract class Setting
    {
        public string Key { get; }
        public SettingLayer WriteLayer { get; }

        protected Setting(string key, SettingLayer writeLayer)
        {
            Key = key;
            WriteLayer = writeLayer;
        }

        internal abstract string DefaultText { get; }

        public override string ToString() => Key;
    }

    public sealed class Setting<T> : Setting
    {
        public T Default { get; }

        public Setting(string key, T defaultValue, SettingLayer writeLayer) : base(key, writeLayer)
        {
            Default = defaultValue;
        }

        internal override string DefaultText => SettingCodec.Format(Default);
    }
}
