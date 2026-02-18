using BetterArchitect;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Better_Architect_Edit_mode
{
    public static class BamCompatibility
    {
        public const string BamPackageId = "ferny.betterarchitect";

        public static bool IsBamLoaded
        {
            get
            {
                return ModsConfig.IsActive(BamPackageId);
            }
        }
    }

    public static class BamRuntime
    {
        private static int cacheVersion;
        private static readonly Dictionary<string, List<string>> defaultParentChildren = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, Dictionary<string, int>> runtimeChildOrderByParent = new Dictionary<string, Dictionary<string, int>>();
        private static readonly Dictionary<string, DesignationCategoryDef> categoryProxyDefs = new Dictionary<string, DesignationCategoryDef>();
        private static HashSet<DesignationCategoryDef> cachedParents = null;

        public static int CacheVersion
        {
            get
            {
                return cacheVersion;
            }
        }

        public static void Initialize()
        {
            cacheVersion++;
            RebuildDefaultCaches();
        }

        public static void InvalidateAllCaches()
        {
            cacheVersion++;
            runtimeChildOrderByParent.Clear();
            RebuildDefaultCaches();
        }

        public static IReadOnlyList<string> GetChildrenForParent(string parentDefName)
        {
            var defaults = defaultParentChildren.TryGetValue(parentDefName, out var list)
                ? new List<string>(list)
                : new List<string>();

            ParentOverride parentOverride = null;
            if (ModSettings.parentOverrides.TryGetValue(parentDefName, out ParentOverride entry))
            {
                parentOverride = entry;
            }
            if (parentOverride == null)
            {
                return defaults;
            }

            if (parentOverride.replaceDefaultChildren)
            {
                return parentOverride.childCategoryIds.ToList();
            }

            foreach (var childId in parentOverride.childCategoryIds)
            {
                if (!defaults.Contains(childId))
                {
                    defaults.Add(childId);
                }
            }

            return defaults;
        }

        public static void SetChildrenForParent(string parentDefName, List<string> childIds, bool replaceDefaults = true)
        {
            var parentOverride = ModSettings.GetOrCreateParentOverride(parentDefName);
            parentOverride.replaceDefaultChildren = replaceDefaults;
            parentOverride.childCategoryIds = childIds == null
                ? new List<string>()
                : childIds.Where(id => !id.NullOrEmpty()).Distinct().ToList();
            InvalidateAllCaches();
        }

        public static bool IsDefaultChild(string parentDefName, string childId)
        {
            return defaultParentChildren.TryGetValue(parentDefName, out var list) && list.Contains(childId);
        }

        public static CategoryOverride GetCategoryOverride(string categoryId)
        {
            if (ModSettings.categoryOverrides.TryGetValue(categoryId, out CategoryOverride entry))
            {
                return entry;
            }
            return null;
        }

        public static string GetCategoryLabel(string categoryId)
        {
            var overrideEntry = GetCategoryOverride(categoryId);
            var def = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(categoryId);
            return def.LabelCap.ToString();
        }

        public static int GetCategoryOrder(string categoryId, int fallbackOrder)
        {
            var overrideEntry = GetCategoryOverride(categoryId);
            if (overrideEntry != null && overrideEntry.hasOrderOverride)
            {
                return overrideEntry.orderOverride;
            }

            var def = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(categoryId);
            if (def != null)
            {
                return (int)def.order;
            }

            return fallbackOrder;
        }

        public static void SetRuntimeParentOrder(string parentDefName, List<string> orderedChildIds)
        {
            var orderMap = new Dictionary<string, int>();
            for (int i = 0; i < orderedChildIds.Count; i++)
            {
                var id = orderedChildIds[i];
                if (orderMap.ContainsKey(id))
                {
                    continue;
                }

                orderMap[id] = i;
            }

            runtimeChildOrderByParent[parentDefName] = orderMap;
        }

        public static int GetRuntimeChildOrder(string parentDefName, string childCategoryId, int fallbackOrder)
        {
            if (runtimeChildOrderByParent.TryGetValue(parentDefName, out var map) &&
                map.TryGetValue(childCategoryId, out var index))
            {
                return index;
            }

            return fallbackOrder;
        }

        public static DesignationCategoryDef GetOrCreateCategoryProxy(string parentDefName, string categoryId, int fallbackOrder)
        {
            var proxyKey = parentDefName + "::" + categoryId;
            DesignationCategoryDef proxy;
            if (!categoryProxyDefs.TryGetValue(proxyKey, out proxy))
            {
                proxy = new DesignationCategoryDef();
                proxy.defName = categoryId;
                categoryProxyDefs[proxyKey] = proxy;
            }

            var sourceDef = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(categoryId);
            var orderIndex = GetRuntimeChildOrder(parentDefName, categoryId, fallbackOrder);
            var sortOrder = 100000 - orderIndex;

            proxy.label = GetCategoryLabel(categoryId);
            proxy.order = sortOrder;

            if (sourceDef != null && sourceDef.specialDesignatorClasses != null)
            {
                proxy.specialDesignatorClasses = sourceDef.specialDesignatorClasses.ToList();
            }
            else
            {
                proxy.specialDesignatorClasses = new List<System.Type>();
            }

            return proxy;
        }

        public static HashSet<DesignationCategoryDef> GetParents()
        {
            if (cachedParents == null)
            {
                cachedParents = DefDatabase<DesignationCategoryDef>.AllDefsListForReading
                .Where(d => !defaultParentChildren.TryGetValue(d.defName, out var children) || children.Count > 0)
                .OrderBy(d => d.order)
                .ThenBy(d => d.LabelCap.ToString()).ToHashSet();
            }
            return cachedParents;
        }

        public static bool IsParentCategory(string defName)
        {
            return defaultParentChildren.TryGetValue(defName, out var children) && children != null && children.Count > 0;
        }

        private static void RebuildDefaultCaches()
        {
            defaultParentChildren.Clear();

            foreach (var parent in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                defaultParentChildren[parent.defName] = new List<string>();
            }

            foreach (var category in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                var nested = category.GetModExtension<NestedCategoryExtension>();
                if (nested == null || nested.parentCategory == null)
                {
                    continue;
                }

                var parentDefName = nested.parentCategory.defName;
                if (!defaultParentChildren.TryGetValue(parentDefName, out var list))
                {
                    list = new List<string>();
                    defaultParentChildren[parentDefName] = list;
                }

                if (!list.Contains(category.defName))
                {
                    list.Add(category.defName);
                }
            }

            foreach (var key in defaultParentChildren.Keys.ToList())
            {
                defaultParentChildren[key] = defaultParentChildren[key]
                    .Distinct()
                    .OrderByDescending(child => DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(child).order)
                    .ThenBy(child => GetCategoryLabel(child))
                    .ToList();
            }
        }
    }
}




