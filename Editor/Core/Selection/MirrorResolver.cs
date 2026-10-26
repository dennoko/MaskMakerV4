using System.Collections.Generic;

namespace Dennoko.UVMaskMaker.Core
{
    /// <summary>対称グループを投票で決める（Impl selection_system §4.5）。</summary>
    public static class MirrorResolver
    {
        /// <summary>対称グループ。見つからない・自分自身の時は -1。</summary>
        public static int Resolve(GroupTable table, int[] mirrorTri, int group)
        {
            if (mirrorTri == null) return -1;
            int start = table.GroupStart[group], end = table.GroupStart[group + 1];
            var votes = new Dictionary<int, int>();
            int best = -1, bestVotes = 0;
            for (int i = start; i < end; i++)
            {
                int m = mirrorTri[table.GroupTris[i]];
                if (m < 0) continue;
                int g = table.TriToGroup[m];
                votes.TryGetValue(g, out int v);
                votes[g] = ++v;
                if (v > bestVotes) { bestVotes = v; best = g; }
            }
            if (best < 0 || best == group) return -1;
            if (bestVotes * 2 < end - start) return -1;
            return best;
        }
    }
}
