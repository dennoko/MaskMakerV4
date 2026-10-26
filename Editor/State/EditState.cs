using System;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;
using UnityEngine;

namespace Dennoko.UVMaskMaker
{
    [Flags]
    public enum EditChange
    {
        None = 0,
        Selection = 1,
        Paint = 2,
        Target = 4,
        All = Selection | Paint | Target,
    }

    /// <summary>
    /// 作業内容の真実（Design state_and_lifecycle §1.1）。Unity の Undo の対象。
    /// [FilePath] を付けないのでディスクには書かれない（保存は SessionPersistence が行う）。
    /// ScriptableSingleton なのでドメインリロード後も中身と Undo 履歴が残る。
    /// </summary>
    internal sealed class EditState : ScriptableSingleton<EditState>
    {
        public const int PaintSize = 4096;

        [SerializeField] private string targetKey = string.Empty;
        [SerializeField] private long meshHash;
        [SerializeField] private int triangleCount;
        [SerializeField] private int[] selectedBits = Array.Empty<int>();
        [SerializeField] private byte[] paintRle = Array.Empty<byte>();

        [NonSerialized] private ulong _selHash;
        [NonSerialized] private ulong _paintHash;
        [NonSerialized] private bool _hashesReady;

        /// <summary>変わるたびに +1。リロード後は 0 から（キャッシュも 0 から作り直すのでそろう）。</summary>
        [NonSerialized] public int SelectionRevision;
        [NonSerialized] public int PaintRevision;

        public static event Action<EditChange> Changed;

        public string TargetKey => targetKey;
        public ulong MeshHash => (ulong)meshHash;
        public int TriangleCount => triangleCount;
        public int[] SelectedBits => selectedBits;
        public byte[] PaintRle => paintRle;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EnsureHashes();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        // ── EditCommands からだけ呼ぶ ──

        internal void SetTarget(string key, ulong hash, int triCount)
        {
            targetKey = key ?? string.Empty;
            meshHash = (long)hash;
            triangleCount = triCount;
            selectedBits = TriangleBits.Create(triCount);
            paintRle = Core.PaintRle.Empty(PaintSize * PaintSize);
            Touched(EditChange.All);
        }

        internal void SetSelectedBits(int[] bits)
        {
            selectedBits = bits;
            Touched(EditChange.Selection);
        }

        internal void SetPaintRle(byte[] rle)
        {
            paintRle = rle;
            Touched(EditChange.Paint);
        }

        internal void SetBoth(int[] bits, byte[] rle)
        {
            selectedBits = bits;
            paintRle = rle;
            Touched(EditChange.Selection | EditChange.Paint);
        }

        private void Touched(EditChange change)
        {
            if ((change & EditChange.Selection) != 0) { SelectionRevision++; _selHash = Fnv1a.Of(selectedBits); }
            if ((change & EditChange.Paint) != 0) { PaintRevision++; _paintHash = Fnv1a.Of(paintRle); }
            _hashesReady = true;
            Changed?.Invoke(change);
        }

        private void EnsureHashes()
        {
            if (_hashesReady) return;
            _selHash = Fnv1a.Of(selectedBits);
            _paintHash = Fnv1a.Of(paintRle);
            _hashesReady = true;
        }

        /// <summary>Undo.RevertAllDownToGroup など、undoRedoPerformed を通らない巻き戻しの後に呼ぶ。</summary>
        public void ResyncAfterExternalChange() => OnUndoRedo();

        /// <summary>Undo / Redo で中身が書き換わった時、変わった方だけリビジョンを上げて知らせる。</summary>
        private void OnUndoRedo()
        {
            EnsureHashes();
            var change = EditChange.None;
            if (Fnv1a.Of(selectedBits) != _selHash) change |= EditChange.Selection;
            if (Fnv1a.Of(paintRle) != _paintHash) change |= EditChange.Paint;
            if (change != EditChange.None) Touched(change);
        }
    }
}
