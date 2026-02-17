using System.Collections.Generic;
using BetterArchitect;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Better_Architect_Edit_mode
{
    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "HandleCategorySelection")]
    public static class Bam_HandleCategorySelection_ReserveEditToolbarPatch
    {
        internal const float EditToolbarHeight = 28f;
        internal const float EditToolbarVerticalPadding = 4f;
        internal const float EditToolbarReservedHeight = EditToolbarHeight + EditToolbarVerticalPadding;

        public static void Prefix(ref Rect rect)
        {
            if (!ModSettings.editMode)
            {
                return;
            }

            if (rect.height > EditToolbarReservedHeight + 8f)
            {
                rect.height -= EditToolbarReservedHeight;
            }
        }
    }

    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "DrawViewControls")]
    public static class Bam_DrawViewControls_EditButtonPatch
    {
        public static void Postfix(Rect rect, DesignationCategoryDef category, List<Designator> designators)
        {
            var buttonRect = new Rect(rect.x - 30f, rect.y + 4f, 24f, 24f).ExpandedBy(1f);
            var icon = ModSettings.editMode ? Assets.EditIconHighlighted : Assets.EditIcon;

            if (Widgets.ButtonImage(buttonRect, icon ?? BaseContent.BadTex))
            {
                ModSettings.editMode = !ModSettings.editMode;
                Mod.Instance.WriteSettings();
            }

            if (ModSettings.editMode)
            {
                Widgets.DrawHighlight(buttonRect);
            }

            TooltipHandler.TipRegion(buttonRect, "BetterArchitectEditMode.EditModeToggleTooltip".Translate());
        }
    }

    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), nameof(ArchitectCategoryTab_DesignationTabOnGUI_Patch.Reset))]
    public static class Bam_Reset_CacheResetPatch
    {
        public static void Postfix()
        {
            Bam_HandleCategorySelection_OverridePatch.ClearSelectionCache();
            BamRuntime.InvalidateAllCaches();
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Architect), "CacheDesPanels")]
    public static class Bam_MainTabWindowArchitect_CacheDesPanels_SkipParentsPatch
    {
        public static void Postfix(MainTabWindow_Architect __instance)
        {
            var removedSelected = false;
            for (int i = __instance.desPanelsCached.Count - 1; i >= 0; i--)
            {
                var tab = __instance.desPanelsCached[i];

                var defName = tab.def.defName;
                if (!BamRuntime.IsParentCategory(defName))
                {
                    continue;
                }

                if (ModSettings.ShouldSkipParentCategory(defName))
                {
                    if (__instance.selectedDesPanel == tab)
                    {
                        removedSelected = true;
                    }

                    __instance.desPanelsCached.RemoveAt(i);
                }
            }

            if (removedSelected)
            {
                __instance.selectedDesPanel = null;
            }
        }
    }
}
