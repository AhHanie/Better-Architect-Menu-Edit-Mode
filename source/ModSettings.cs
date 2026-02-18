using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Better_Architect_Edit_mode
{
    public class ModSettings : Verse.ModSettings
    {
        public static bool editMode;
        public static int schemaVersion = 1;
        public static Dictionary<string, ParentOverride> parentOverrides = new Dictionary<string, ParentOverride>();
        public static Dictionary<string, CategoryOverride> categoryOverrides = new Dictionary<string, CategoryOverride>();
        public static List<string> skippedParentCategoryIds = new List<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref editMode, "editMode", false);
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
            Scribe_Collections.Look(ref parentOverrides, "parentOverrides", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref categoryOverrides, "categoryOverrides", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref skippedParentCategoryIds, "skippedParentCategoryIds", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (parentOverrides == null) parentOverrides = new Dictionary<string, ParentOverride>();
                if (categoryOverrides == null) categoryOverrides = new Dictionary<string, CategoryOverride>();
                if (skippedParentCategoryIds == null) skippedParentCategoryIds = new List<string>();
                skippedParentCategoryIds = skippedParentCategoryIds
                    .Where(id =>
                        !id.NullOrEmpty() &&
                        DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(id) != null)
                    .ToList();
            }
        }

        public static ParentOverride GetOrCreateParentOverride(string parentDefName)
        {
            if (parentOverrides.TryGetValue(parentDefName, out ParentOverride entry))
            {
                return entry;
            }

            entry = new ParentOverride { parentDefName = parentDefName };
            parentOverrides.Add(parentDefName, entry);
            return entry;
        }

        public static CategoryOverride GetOrCreateCategoryOverride(string categoryId)
        {
            if (categoryOverrides.TryGetValue(categoryId, out CategoryOverride entry))
            {
                return entry;
            }

            entry = new CategoryOverride { categoryId = categoryId };
            categoryOverrides.Add(categoryId, entry);
            return entry;
        }

        public static void ResetAll()
        {
            editMode = false;
            parentOverrides.Clear();
            categoryOverrides.Clear();
            skippedParentCategoryIds.Clear();
        }

        public static bool ShouldSkipParentCategory(string parentDefName)
        {
            return skippedParentCategoryIds.Contains(parentDefName);
        }
    }

    public class ParentOverride : IExposable
    {
        public string parentDefName;
        public bool replaceDefaultChildren;
        public List<string> childCategoryIds = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref parentDefName, "parentDefName");
            Scribe_Values.Look(ref replaceDefaultChildren, "replaceDefaultChildren", false);
            Scribe_Collections.Look(ref childCategoryIds, "childCategoryIds", LookMode.Value);
            if (childCategoryIds == null) childCategoryIds = new List<string>();
        }
    }

    public class CategoryOverride : IExposable
    {
        public string categoryId;
        public bool hasOrderOverride;
        public int orderOverride;

        public bool replaceDefaultSpecials;
        public bool replaceDefaultBuildables;
        public List<string> specialClassNames = new List<string>();
        public List<string> buildableDefNames = new List<string>();
        public List<string> removedBuildableDefNames = new List<string>();

        public bool HasModifications =>
            replaceDefaultBuildables ||
            replaceDefaultSpecials ||
            hasOrderOverride ||
            buildableDefNames.Count > 0 ||
            specialClassNames.Count > 0 ||
            removedBuildableDefNames.Count > 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref categoryId, "categoryId");
            Scribe_Values.Look(ref hasOrderOverride, "hasOrderOverride", false);
            Scribe_Values.Look(ref orderOverride, "orderOverride", 0);

            Scribe_Values.Look(ref replaceDefaultSpecials, "replaceDefaultSpecials", false);
            Scribe_Values.Look(ref replaceDefaultBuildables, "replaceDefaultBuildables", false);
            Scribe_Collections.Look(ref specialClassNames, "specialClassNames", LookMode.Value);
            Scribe_Collections.Look(ref buildableDefNames, "buildableDefNames", LookMode.Value);
            Scribe_Collections.Look(ref removedBuildableDefNames, "removedBuildableDefNames", LookMode.Value);

            if (specialClassNames == null) specialClassNames = new List<string>();
            if (buildableDefNames == null) buildableDefNames = new List<string>();
            if (removedBuildableDefNames == null) removedBuildableDefNames = new List<string>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                buildableDefNames.RemoveAll(defName =>
                    defName.NullOrEmpty() || DefDatabase<BuildableDef>.GetNamedSilentFail(defName) == null);
                removedBuildableDefNames.RemoveAll(defName =>
                    defName.NullOrEmpty() || DefDatabase<BuildableDef>.GetNamedSilentFail(defName) == null);
            }
        }
    }
}
