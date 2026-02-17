using System;
using System.Collections.Generic;
using System.Linq;
using BetterArchitect;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Better_Architect_Edit_mode
{
    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "HandleCategorySelection")]
    public static class Bam_HandleCategorySelection_OverridePatch
    {
        private static readonly Dictionary<string, DesignatorCategoryData> byDefNameBuffer = new Dictionary<string, DesignatorCategoryData>();
        private static readonly HashSet<string> currentParentVisibleChildIds = new HashSet<string>();
        private static readonly List<DesignatorCategoryData> cachedRows = new List<DesignatorCategoryData>();
        private static readonly HashSet<string> cachedVisibleChildIds = new HashSet<string>();
        private static string cachedParentDefName;
        private static int cachedAtRuntimeVersion = -1;

        internal static bool IsCurrentParentChildVisible(string categoryDefName)
        {
            return currentParentVisibleChildIds.Contains(categoryDefName);
        }

        internal static void ClearSelectionCache()
        {
            byDefNameBuffer.Clear();
            currentParentVisibleChildIds.Clear();
            cachedRows.Clear();
            cachedVisibleChildIds.Clear();
            cachedParentDefName = null;
            cachedAtRuntimeVersion = -1;
        }

        public static void Prefix(DesignationCategoryDef mainCat, List<DesignatorCategoryData> designatorDataList)
        {
            if (TryApplyCachedRows(mainCat, designatorDataList))
            {
                return;
            }

            byDefNameBuffer.Clear();
            currentParentVisibleChildIds.Clear();
            for (int i = 0; i < designatorDataList.Count; i++)
            {
                var row = designatorDataList[i];
                var key = row.def.defName;
                if (!byDefNameBuffer.ContainsKey(key))
                {
                    byDefNameBuffer[key] = row;
                }
            }

            var childIds = new List<string>(BamRuntime.GetChildrenForParent(mainCat.defName));
            for (int i = 0; i < childIds.Count; i++)
            {
                var childId = childIds[i];
                // Only force-show a child if it was explicitly added (not a default child) or
                // has real designator modifications. Default children defer to BAM's own visibility.
                if (!BamRuntime.IsDefaultChild(mainCat.defName, childId) ||
                    (ModSettings.categoryOverrides.TryGetValue(childId, out var co) && co.HasModifications))
                {
                    currentParentVisibleChildIds.Add(childId);
                }
            }
            ParentOverride parentOverride = null;
            if (ModSettings.parentOverrides.TryGetValue(mainCat.defName, out ParentOverride entry))
            {
                parentOverride = entry;
            }
            BamRuntime.SetRuntimeParentOrder(mainCat.defName, childIds);

            List<DesignatorCategoryData> result;
            if (parentOverride == null)
            {
                result = new List<DesignatorCategoryData>(designatorDataList.Count);
                for (int i = 0; i < designatorDataList.Count; i++)
                {
                    var d = designatorDataList[i];
                    var built = BuildDataRow(mainCat.defName, d.def, d.def == mainCat, d.allDesignators, d.def.defName, 0, false);
                    result.Add(built);
                }
            }
            else
            {
                result = new List<DesignatorCategoryData>();
                for (int i = 0; i < childIds.Count; i++)
                {
                    var childId = childIds[i];
                    var existing = byDefNameBuffer.TryGetValue(childId, out var current) ? current : null;

                    // If BAM would hide this child (not in its list) and it is a default child
                    // with no real modifications, respect BAM's decision and keep it hidden.
                    if (existing == null && BamRuntime.IsDefaultChild(mainCat.defName, childId))
                    {
                        var co = BamRuntime.GetCategoryOverride(childId);
                        if (co == null || !co.HasModifications) continue;
                    }

                    var sourceDef = existing != null ? existing.def : DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(childId);
                    var sourceDesignators = existing != null ? existing.allDesignators : GetDefaultDesignatorsFor(sourceDef);

                    var built = BuildDataRow(mainCat.defName, sourceDef, false, sourceDesignators, childId, i, true);
                    result.Add(built);
                }

                if (!result.Any(d => d.def.defName == mainCat.defName))
                {
                    var mainExisting = byDefNameBuffer.TryGetValue(mainCat.defName, out var currentMain) ? currentMain : null;
                    var mainDesignators = mainExisting != null ? mainExisting.allDesignators : GetDefaultDesignatorsFor(mainCat);
                    var builtMain = BuildDataRow(mainCat.defName, mainCat, true, mainDesignators, mainCat.defName, 9999, false);
                    result.Add(builtMain);
                }
            }

            designatorDataList.Clear();
            designatorDataList.AddRange(result);
            UpdateSelectionCache(mainCat.defName, result);
        }

        private static bool TryApplyCachedRows(DesignationCategoryDef mainCat, List<DesignatorCategoryData> designatorDataList)
        {
            if (cachedAtRuntimeVersion != BamRuntime.CacheVersion)
            {
                return false;
            }

            if (!string.Equals(cachedParentDefName, mainCat.defName, StringComparison.Ordinal))
            {
                return false;
            }

            currentParentVisibleChildIds.Clear();
            foreach (var childId in cachedVisibleChildIds)
            {
                currentParentVisibleChildIds.Add(childId);
            }

            designatorDataList.Clear();
            designatorDataList.AddRange(cachedRows);
            return true;
        }

        private static void UpdateSelectionCache(string parentDefName, List<DesignatorCategoryData> rows)
        {
            cachedParentDefName = parentDefName;
            cachedAtRuntimeVersion = BamRuntime.CacheVersion;

            cachedRows.Clear();
            cachedRows.AddRange(rows);

            cachedVisibleChildIds.Clear();
            foreach (var childId in currentParentVisibleChildIds)
            {
                cachedVisibleChildIds.Add(childId);
            }
        }

        private static DesignatorCategoryData BuildDataRow(
            string parentDefName,
            DesignationCategoryDef sourceDef,
            bool isMainCategory,
            List<Designator> sourceAllDesignators,
            string categoryId,
            int fallbackOrder,
            bool useProxyDef)
        {
            DesignationCategoryDef rowDef = sourceDef;
            if (useProxyDef)
            {
                rowDef = BamRuntime.GetOrCreateCategoryProxy(parentDefName, categoryId, fallbackOrder);
            }

            var all = BuildEffectiveDesignators(categoryId, rowDef, sourceAllDesignators);
            var separated = ArchitectCategoryTab_DesignationTabOnGUI_Patch.SeparateDesignatorsByType(all, rowDef);
            return new DesignatorCategoryData(rowDef, isMainCategory, all, separated.buildables, separated.orders);
        }

        private static List<Designator> GetDefaultDesignatorsFor(DesignationCategoryDef def)
        {
            if (def.ResolvedAllowedDesignators == null)
            {
                return new List<Designator>();
            }

            return def.ResolvedAllowedDesignators.Where(d => d.Visible).ToList();
        }

        private static List<Designator> BuildEffectiveDesignators(string categoryId, DesignationCategoryDef categoryDef, List<Designator> defaults)
        {
            var entry = BamRuntime.GetCategoryOverride(categoryId);
            if (entry == null)
            {
                return defaults.ToList();
            }

            var defaultBuildables = new List<Designator>();
            var defaultSpecials = new List<Designator>();
            for (int i = 0; i < defaults.Count; i++)
            {
                var defName = GetBuildableDefName(defaults[i]);
                if (defName.NullOrEmpty())
                {
                    defaultSpecials.Add(defaults[i]);
                }
                else
                {
                    defaultBuildables.Add(defaults[i]);
                }
            }

            var buildables = entry.replaceDefaultBuildables ? new List<Designator>() : defaultBuildables.ToList();
            var specials = entry.replaceDefaultSpecials ? new List<Designator>() : defaultSpecials.ToList();

            if (entry.removedBuildableDefNames.Count > 0)
            {
                var removed = new HashSet<string>(entry.removedBuildableDefNames);
                buildables = buildables.Where(d => !removed.Contains(GetBuildableDefName(d))).ToList();
            }

            if (entry.buildableDefNames.Count > 0)
            {
                var currentByDefName = new Dictionary<string, Designator>();
                for (int i = 0; i < buildables.Count; i++)
                {
                    var key = GetBuildableDefName(buildables[i]);
                    if (!currentByDefName.ContainsKey(key))
                    {
                        currentByDefName[key] = buildables[i];
                    }
                }
                for (int i = 0; i < defaultBuildables.Count; i++)
                {
                    var key = GetBuildableDefName(defaultBuildables[i]);
                    if (!currentByDefName.ContainsKey(key))
                    {
                        currentByDefName[key] = defaultBuildables[i];
                    }
                }

                foreach (var defName in entry.buildableDefNames)
                {
                    if (currentByDefName.TryGetValue(defName, out var existing))
                    {
                        if (!buildables.Contains(existing))
                        {
                            buildables.Add(existing);
                        }

                        continue;
                    }

                    var created = CreateBuildableDesignator(defName);
                    buildables.Add(created);
                    currentByDefName[defName] = created;
                }

                var order = entry.buildableDefNames
                    .Select((name, index) => new { name, index })
                    .ToDictionary(x => x.name, x => x.index);

                buildables.Sort(delegate(Designator a, Designator b)
                {
                    var keyA = GetBuildableDefName(a);
                    var keyB = GetBuildableDefName(b);
                    var orderA = order.TryGetValue(keyA, out var idxA) ? idxA : int.MaxValue;
                    var orderB = order.TryGetValue(keyB, out var idxB) ? idxB : int.MaxValue;
                    var cmp = orderA.CompareTo(orderB);
                    if (cmp != 0) return cmp;
                    var labelA = a.LabelCap.ToString();
                    var labelB = b.LabelCap.ToString();
                    return string.Compare(labelA, labelB, StringComparison.OrdinalIgnoreCase);
                });
            }

            if (entry.specialClassNames.Count > 0)
            {
                var currentByClass = new Dictionary<string, Designator>();
                for (int i = 0; i < specials.Count; i++)
                {
                    var d = specials[i];
                    var key = d.GetType().FullName;
                    if (!currentByClass.ContainsKey(key))
                    {
                        currentByClass[key] = d;
                    }
                }

                foreach (var className in entry.specialClassNames)
                {
                    if (currentByClass.TryGetValue(className, out var existing))
                    {
                        if (!specials.Contains(existing))
                        {
                            specials.Add(existing);
                        }

                        continue;
                    }

                    var created = CreateSpecialDesignator(className);
                    specials.Add(created);
                    currentByClass[className] = created;
                }

                var classOrder = entry.specialClassNames
                    .Select((name, index) => new { name, index })
                    .ToDictionary(x => x.name, x => x.index);

                specials.Sort(delegate(Designator a, Designator b)
                {
                    var keyA = a.GetType().FullName;
                    var keyB = b.GetType().FullName;
                    var orderA = classOrder.TryGetValue(keyA, out var idxA) ? idxA : int.MaxValue;
                    var orderB = classOrder.TryGetValue(keyB, out var idxB) ? idxB : int.MaxValue;
                    var cmp = orderA.CompareTo(orderB);
                    if (cmp != 0) return cmp;
                    var labelA = a.LabelCap.ToString();
                    var labelB = b.LabelCap.ToString();
                    return string.Compare(labelA, labelB, StringComparison.OrdinalIgnoreCase);
                });
            }

            var result = new List<Designator>(buildables.Count + specials.Count);
            for (int i = 0; i < buildables.Count; i++)
            {
                result.Add(buildables[i]);
            }
            for (int i = 0; i < specials.Count; i++)
            {
                result.Add(specials[i]);
            }
            return result;
        }

        private static Designator CreateBuildableDesignator(string defName)
        {
            return new Designator_Build(DefDatabase<BuildableDef>.GetNamedSilentFail(defName));
        }

        private static Designator CreateSpecialDesignator(string className)
        {
            var type = AccessTools.TypeByName(className);
            var instance = Activator.CreateInstance(type) as Designator;
            return instance;
        }

        private static string GetBuildableDefName(Designator d)
        {
            if (d is Designator_Build db)
            {
                return db.PlacingDef != null ? db.PlacingDef.defName : null;
            }

            if (d is Designator_Dropdown dd)
            {
                var nested = dd.Elements.OfType<Designator_Build>().FirstOrDefault();
                return nested?.PlacingDef?.defName;
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "IsSpecialCategory")]
    public static class Bam_IsSpecialCategory_ProxyFixPatch
    {
        public static void Postfix(DesignationCategoryDef cat, ref bool __result)
        {
            if (__result)
            {
                return;
            }

            // Keep explicitly-configured children visible even when they are empty.
            if (Bam_HandleCategorySelection_OverridePatch.IsCurrentParentChildVisible(cat.defName))
            {
                __result = true;
                return;
            }

            // Proxy categories are synthetic instances; resolve by defName so special categories
            // (Orders/Zone/Blueprints/extension-driven) keep their original behavior.
            var sourceDef = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(cat.defName);
            if (sourceDef == null)
            {
                return;
            }

            if (sourceDef == DefsOf.Orders ||
                sourceDef == DesignationCategoryDefOf.Zone ||
                sourceDef.defName == "Blueprints" ||
                sourceDef.GetModExtension<SpecialCategoryExtension>() != null)
            {
                __result = true;
            }
        }
    }
}

