using System.Collections.Generic;

namespace Dennoko.UVMaskMaker.Core
{
    public enum GroupSelection
    {
        None,
        Partial,
        Full,
    }

    /// <summary>三角形ビット列に対する選択演算（Impl selection_system §2–4）。</summary>
    public static class SelectionOps
    {
        public static GroupSelection StateOf(int[] bits, GroupTable table, int group)
        {
            int start = table.GroupStart[group], end = table.GroupStart[group + 1];
            int selected = 0;
            for (int i = start; i < end; i++)
                if (TriangleBits.Get(bits, table.GroupTris[i])) selected++;
            if (selected == 0) return GroupSelection.None;
            return selected == end - start ? GroupSelection.Full : GroupSelection.Partial;
        }

        /// <summary>スマートトグル: Full のグループは除外、それ以外は追加になる。</summary>
        public static bool ToggleResult(int[] bits, GroupTable table, IReadOnlyList<int> groups)
        {
            if (groups.Count == 0) return true;
            for (int i = 0; i < groups.Count; i++)
                if (StateOf(bits, table, groups[i]) != GroupSelection.Full) return true;
            return false;
        }

        /// <summary>グループ全体を選択 / 除外する。変更があれば true。</summary>
        public static bool SetGroup(int[] bits, GroupTable table, int group, bool value)
        {
            bool changed = false;
            int start = table.GroupStart[group], end = table.GroupStart[group + 1];
            for (int i = start; i < end; i++)
            {
                int t = table.GroupTris[i];
                if (TriangleBits.Get(bits, t) == value) continue;
                TriangleBits.Set(bits, t, value);
                changed = true;
            }
            return changed;
        }

        public static bool SelectAll(int[] bits, int[] active)
        {
            bool changed = false;
            for (int i = 0; i < bits.Length; i++)
            {
                int v = bits[i] | active[i];
                if (v != bits[i]) { bits[i] = v; changed = true; }
            }
            return changed;
        }

        public static bool Invert(int[] bits, int[] active)
        {
            bool changed = false;
            for (int i = 0; i < bits.Length; i++)
            {
                if (active[i] == 0) continue;
                bits[i] ^= active[i];
                changed = true;
            }
            return changed;
        }

        public static bool Clear(int[] bits)
        {
            bool changed = false;
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] == 0) continue;
                bits[i] = 0;
                changed = true;
            }
            return changed;
        }

        /// <summary>選択に隣接する未選択の有効三角形を含むグループを追加する（1 段）。</summary>
        public static bool Grow(int[] bits, int[] active, GroupTable table, int[] adjStart, int[] adjTris)
        {
            var groups = new HashSet<int>();
            int triCount = table.TriToGroup.Length;
            for (int t = 0; t < triCount; t++)
            {
                if (!TriangleBits.Get(bits, t)) continue;
                for (int i = adjStart[t]; i < adjStart[t + 1]; i++)
                {
                    int n = adjTris[i];
                    if (!TriangleBits.Get(bits, n) && TriangleBits.Get(active, n)) groups.Add(table.TriToGroup[n]);
                }
            }
            bool changed = false;
            foreach (int g in groups) changed |= SetGroup(bits, table, g, true);
            return changed;
        }

        /// <summary>未選択・無効の三角形に隣接する選択済み三角形を含むグループを除外する（1 段）。</summary>
        public static bool Shrink(int[] bits, int[] active, GroupTable table, int[] adjStart, int[] adjTris)
        {
            var groups = new HashSet<int>();
            int triCount = table.TriToGroup.Length;
            for (int t = 0; t < triCount; t++)
            {
                if (!TriangleBits.Get(bits, t)) continue;
                for (int i = adjStart[t]; i < adjStart[t + 1]; i++)
                {
                    int n = adjTris[i];
                    if (!TriangleBits.Get(bits, n) || !TriangleBits.Get(active, n))
                    {
                        groups.Add(table.TriToGroup[t]);
                        break;
                    }
                }
            }
            bool changed = false;
            foreach (int g in groups) changed |= SetGroup(bits, table, g, false);
            return changed;
        }

        /// <summary>有効三角形を含むグループの数と、そのうち Full のグループの数。</summary>
        public static void CountGroups(int[] bits, int[] active, GroupTable table, out int full, out int total)
        {
            full = 0;
            total = 0;
            for (int g = 0; g < table.Count; g++)
            {
                int first = table.GroupTris[table.GroupStart[g]];
                if (!TriangleBits.Get(active, first)) continue;
                total++;
                if (StateOf(bits, table, g) == GroupSelection.Full) full++;
            }
        }
    }
}
