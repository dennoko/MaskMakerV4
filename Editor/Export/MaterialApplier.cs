using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Dennoko.UVMaskMaker
{
    /// <summary>出力したマスクをマテリアルのスロットへ割り当てる（Impl export §5）。</summary>
    internal static class MaterialApplier
    {
        public sealed class ApplyPlan
        {
            public Material Material;
            public readonly List<string> Candidates = new List<string>();
            public string RememberedSlot;
            public bool AlreadyAssigned;
            public bool CanApply => Material != null && Candidates.Count > 0;
        }

        public static ApplyPlan Plan(WorkSession s, Texture2D tex)
        {
            var plan = new ApplyPlan();
            var mat = s.TargetMaterial;
            if (mat == null || tex == null || !IsEditable(mat)) return plan;
            plan.Material = mat;

            var shader = mat.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                if ((shader.GetPropertyFlags(i) & ShaderPropertyFlags.HideInInspector) != 0) continue;
                string name = shader.GetPropertyName(i);
                if (mat.GetTexture(name) == tex) plan.AlreadyAssigned = true;
                if (Contains(name, "mask") || Contains(shader.GetPropertyDescription(i), "mask")) plan.Candidates.Add(name);
            }

            string remembered = SettingsService.Get(SettingKeys.MaterialSlot);
            if (!string.IsNullOrEmpty(remembered) && mat.HasProperty(remembered))
            {
                plan.RememberedSlot = remembered;
                if (mat.GetTexture(remembered) == tex) plan.AlreadyAssigned = true;
                if (!plan.Candidates.Contains(remembered)) plan.Candidates.Insert(0, remembered);
            }
            return plan;
        }

        /// <summary>記憶したスロット → 候補が 1 つ → 候補リスト（選んだ時点で適用して記憶）の順。</summary>
        public static void ApplyInteractive(WorkSession s, Texture2D tex, ApplyPlan plan, VisualElement anchor = null)
        {
            if (!plan.CanApply) return;
            if (plan.RememberedSlot != null) { Apply(s, tex, plan.RememberedSlot); return; }
            if (plan.Candidates.Count == 1 || anchor == null) { Apply(s, tex, plan.Candidates[0]); return; }

            var menu = new GenericDropdownMenu();
            foreach (var slot in plan.Candidates)
            {
                string label = Describe(plan.Material, slot);
                menu.AddItem(label, false, () => Apply(s, tex, slot));
            }
            menu.DropDown(anchor.worldBound, anchor, false);
        }

        public static void Apply(WorkSession s, Texture2D tex, string slot)
        {
            var mat = s.TargetMaterial;
            if (mat == null || tex == null || !mat.HasProperty(slot)) return;
            Undo.RecordObject(mat, L10n.T("undo.apply"));
            mat.SetTexture(slot, tex);
            EditorUtility.SetDirty(mat);
            SettingsService.Set(SettingKeys.MaterialSlot, slot);
            Notifier.Post(MessageLevel.Success, L10n.F("status.applied", Path.GetFileName(AssetDatabase.GetAssetPath(tex))), MessageOrigin.Scene);
        }

        private static bool IsEditable(Material mat)
        {
            var path = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(path)) return true;
            return path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("Packages/", StringComparison.Ordinal);
        }

        private static string Describe(Material mat, string slot)
        {
            var shader = mat.shader;
            int i = shader.FindPropertyIndex(slot);
            string desc = i >= 0 ? shader.GetPropertyDescription(i) : null;
            return string.IsNullOrEmpty(desc) || desc == slot ? slot : desc + "  (" + slot + ")";
        }

        private static bool Contains(string s, string part)
            => !string.IsNullOrEmpty(s) && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
