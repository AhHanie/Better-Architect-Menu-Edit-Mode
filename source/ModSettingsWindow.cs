using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace Better_Architect_Edit_mode
{
    public static class ModSettingsWindow
    {
        private static Vector2 parentCategoryScrollPosition;
        public static List<DesignationCategoryDef> allCategoryDefs;

        public static void Draw(Rect parent)
        {
            var listing = new Listing_Standard();
            listing.Begin(parent.ContractedBy(8f));

            listing.Label("BetterArchitectEditMode.DeprecationLabel".Translate());
            listing.End();

            return;
            listing.GapLine();

            if (!BamCompatibility.IsBamLoaded)
            {
                listing.Gap(6f);
                listing.Label("BetterArchitectEditMode.BamMissing".Translate());
            }
            else
            {
                DrawSkipParentCategorySection(listing, parent);
            }

            listing.Gap(8f);
            if (listing.ButtonText("BetterArchitectEditMode.ResetAll".Translate()))
            {
                ModSettings.ResetAll();
                BamRuntime.InvalidateAllCaches();
                RefreshArchitectTabs();
                Messages.Message("BetterArchitectEditMode.ResetAllComplete".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            
        }

        private static void DrawSkipParentCategorySection(Listing_Standard listing, Rect parent)
        {
            listing.GapLine();
            listing.Label("BetterArchitectEditMode.SkipParentCategoriesTitle".Translate());
            listing.Label("BetterArchitectEditMode.SkipParentCategoriesDesc".Translate());
            listing.Gap(4f);

            var outRectHeight = Mathf.Clamp(parent.height - 180f, 120f, 320f);
            var outRect = listing.GetRect(outRectHeight);
            Widgets.DrawBoxSolid(outRect, new Color(0f, 0f, 0f, 0.15f));
            Widgets.DrawBox(outRect, 1);

            var viewRect = new Rect(0f, 0f, outRect.width - 16f, allCategoryDefs.Count * 30f + 8f);
            Widgets.BeginScrollView(outRect, ref parentCategoryScrollPosition, viewRect);

            var rowListing = new Listing_Standard();
            rowListing.Begin(new Rect(4f, 4f, viewRect.width - 8f, viewRect.height - 8f));
            for (int i = 0; i < allCategoryDefs.Count; i++)
            {
                var parentDef = allCategoryDefs[i];

                var isEnabled = !ModSettings.ShouldSkipParentCategory(parentDef.defName);
                var previous = isEnabled;
                rowListing.CheckboxLabeled(parentDef.LabelCap + " (" + parentDef.defName + ")", ref isEnabled);

                if (isEnabled != previous)
                {
                    SetParentCategorySkipped(parentDef.defName, !isEnabled);
                    BamRuntime.InvalidateAllCaches();
                    RefreshArchitectTabs();
                }
            }
            rowListing.End();

            Widgets.EndScrollView();
        }

        private static void SetParentCategorySkipped(string defName, bool shouldSkip)
        {
            if (shouldSkip)
            {
                if (!ModSettings.skippedParentCategoryIds.Contains(defName))
                {
                    ModSettings.skippedParentCategoryIds.Add(defName);
                }
            }
            else
            {
                ModSettings.skippedParentCategoryIds.Remove(defName);
            }
        }

        private static void RefreshArchitectTabs()
        {
            var architectWindow = MainButtonDefOf.Architect.TabWindow as MainTabWindow_Architect;
            architectWindow.CacheDesPanels();
        }
    }
}
