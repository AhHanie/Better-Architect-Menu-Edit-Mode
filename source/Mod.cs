using HarmonyLib;
using UnityEngine;
using Verse;

namespace Better_Architect_Edit_mode
{
    public class Mod : Verse.Mod
    {
        public static Mod Instance;

        public Mod(ModContentPack content) : base(content)
        {
            Instance = this;
            LongEventHandler.QueueLongEvent(Init, "BetterArchitectEditMode.LoadingLabel", true, null);
        }

        public void Init()
        {
            Log.Error("[Better Architect Menu | Edit Mode] This mod is deprecated and is no longer needed. All features present in this mod are now integrated into the original Better Architect Menu. Your settings will be lost and you'll have to redo them. I apologize for that.");
            return;
            GetSettings<ModSettings>();
            BamRuntime.Initialize();
            ModSettingsWindow.allCategoryDefs = DefDatabase<DesignationCategoryDef>.AllDefsListForReading;
            new Harmony("sk.betterarchitectedit").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "BetterArchitectEditMode.SettingsTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ModSettingsWindow.Draw(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            BamRuntime.InvalidateAllCaches();
        }
    }
}
