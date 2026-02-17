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
            if (!BamCompatibility.IsBamLoaded)
            {
                Logger.Error("Better Architect Menu (ferny.betterarchitect) is missing. This mod requires Better Architect Menu as a hard dependency.");
                return;
            }
            GetSettings<ModSettings>();
            BamRuntime.Initialize();
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
