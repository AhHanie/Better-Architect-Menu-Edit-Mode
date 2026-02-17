using RimWorld;
using UnityEngine;
using Verse;

namespace Better_Architect_Edit_mode
{
    public static class ModSettingsWindow
    {
        public static void Draw(Rect parent)
        {
            var listing = new Listing_Standard();
            listing.Begin(parent.ContractedBy(8f));

            listing.Label("BetterArchitectEditMode.SettingsIntro".Translate());
            listing.GapLine();

            if (!BamCompatibility.IsBamLoaded)
            {
                listing.Gap(6f);
                listing.Label("BetterArchitectEditMode.BamMissing".Translate());
            }

            listing.Gap(8f);
            if (listing.ButtonText("BetterArchitectEditMode.ResetAll".Translate()))
            {
                ModSettings.ResetAll();
                BamRuntime.InvalidateAllCaches();
                Messages.Message("BetterArchitectEditMode.ResetAllComplete".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            listing.End();
        }
    }
}
