using System;
using Dennoko.UVMaskMaker.Core;
using UnityEditor;

namespace Dennoko.UVMaskMaker
{
    /// <summary>
    /// EditState を変える唯一の入口（Design architecture §5.2）。Undo の記録は必ずここで行う。
    /// 保存の予約と変更通知は EditState.Changed を受けた SessionPersistence / 各ビューが行う。
    /// </summary>
    internal static class EditCommands
    {
        private static EditState S => EditState.instance;

        /// <summary>選択ビットの複製を mutate に渡し、変わった時だけ Undo に記録して反映する。</summary>
        public static bool ModifySelection(string undoName, Func<int[], bool> mutate)
        {
            var bits = TriangleBits.Copy(S.SelectedBits);
            if (!mutate(bits)) return false;
            Undo.RecordObject(S, undoName);
            S.SetSelectedBits(bits);
            return true;
        }

        public static void CommitPaint(string undoName, byte[] rle)
        {
            Undo.RecordObject(S, undoName);
            S.SetPaintRle(rle);
        }

        /// <summary>統合クリア（Impl mask_processing §3.3）。</summary>
        public static bool ClearAll(string undoName)
        {
            bool hasSelection = TriangleBits.AnySet(S.SelectedBits);
            bool hasPaint = !PaintRle.IsEmpty(S.PaintRle);
            if (!hasSelection && !hasPaint) return false;
            Undo.RecordObject(S, undoName);
            S.SetBoth(TriangleBits.Create(S.TriangleCount), PaintRle.Empty(EditState.PaintSize * EditState.PaintSize));
            return true;
        }

        /// <summary>保存していた作業を 1 件の履歴として戻す（Ctrl+Z で空に戻せる）。</summary>
        public static void Restore(string undoName, int[] bits, byte[] rle)
        {
            Undo.RecordObject(S, undoName);
            S.SetBoth(bits, rle);
        }

        /// <summary>対象を切り替える。別の対象の履歴が混ざらないよう Undo を消してから空にする。</summary>
        public static void ResetForTarget(string targetKey, ulong meshHash, int triangleCount)
        {
            Undo.ClearUndo(S);
            S.SetTarget(targetKey, meshHash, triangleCount);
        }
    }
}
