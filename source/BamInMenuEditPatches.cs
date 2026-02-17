using System;
using System.Collections.Generic;
using System.Linq;
using BetterArchitect;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Better_Architect_Edit_mode
{
    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "HandleCategorySelection")]
    public static class Bam_HandleCategorySelection_InMenuButtonsPatch
    {
        public static void Postfix(Rect rect, DesignationCategoryDef mainCat, DesignationCategoryDef __result)
        {
            if (!ModSettings.editMode)
            {
                return;
            }

            DrawCategoryEditControls(rect, mainCat, __result);
        }

        private static void DrawCategoryEditControls(Rect leftRect, DesignationCategoryDef mainCat, DesignationCategoryDef selectedCategory)
        {
            var toolbarHeight = Bam_HandleCategorySelection_ReserveEditToolbarPatch.EditToolbarHeight;
            var toolbarRect = new Rect(leftRect.x + 4f, leftRect.yMax + 2f, leftRect.width - 8f, toolbarHeight);

            var parentId = mainCat.defName;
            var children = BamRuntime.GetChildrenForParent(parentId).ToList();
            var selectedId = selectedCategory != null ? selectedCategory.defName : null;

            var buttonW = (toolbarRect.width - 15f) / 6f;
            var plusRect = new Rect(toolbarRect.x, toolbarRect.y, buttonW, toolbarRect.height);
            var minusRect = new Rect(plusRect.xMax + 3f, toolbarRect.y, buttonW, toolbarRect.height);
            var upRect = new Rect(minusRect.xMax + 3f, toolbarRect.y, buttonW, toolbarRect.height);
            var downRect = new Rect(upRect.xMax + 3f, toolbarRect.y, buttonW, toolbarRect.height);
            var resetCategoriesRect = new Rect(downRect.xMax + 3f, toolbarRect.y, buttonW, toolbarRect.height);
            var resetDesignatorsRect = new Rect(resetCategoriesRect.xMax + 3f, toolbarRect.y, buttonW, toolbarRect.height);

            if (DrawIconButton(plusRect, TexButton.Plus)) OpenAddCategoryMenu(parentId, children);

            var canModifySelected = !selectedId.NullOrEmpty() && children.Contains(selectedId);
            if (DrawIconButton(minusRect, TexButton.Delete) && canModifySelected)
            {
                children.Remove(selectedId);
                BamRuntime.SetChildrenForParent(parentId, children);
            }
            if (DrawIconButton(upRect, TexButton.ReorderUp) && canModifySelected)
            {
                MoveString(children, selectedId, -1);
                BamRuntime.SetChildrenForParent(parentId, children);
            }
            if (DrawIconButton(downRect, TexButton.ReorderDown) && canModifySelected)
            {
                MoveString(children, selectedId, 1);
                BamRuntime.SetChildrenForParent(parentId, children);
            }
            if (DrawIconButton(resetCategoriesRect, Assets.RestoreIcon)) BamRuntime.SetChildrenForParent(parentId, new List<string>(), false);
            if (DrawIconButton(resetDesignatorsRect, Assets.RestoreIcon) && !selectedId.NullOrEmpty()) ResetDesignatorsForCategory(selectedId);

            TooltipHandler.TipRegion(plusRect, "BetterArchitectEditMode.TooltipAddCategoryToParent".Translate());
            TooltipHandler.TipRegion(minusRect, "BetterArchitectEditMode.TooltipRemoveSelectedChildCategory".Translate());
            TooltipHandler.TipRegion(upRect, "BetterArchitectEditMode.TooltipMoveSelectedChildUp".Translate());
            TooltipHandler.TipRegion(downRect, "BetterArchitectEditMode.TooltipMoveSelectedChildDown".Translate());
            TooltipHandler.TipRegion(resetCategoriesRect, "BetterArchitectEditMode.TooltipResetCategoriesForParent".Translate());
            TooltipHandler.TipRegion(resetDesignatorsRect, "BetterArchitectEditMode.TooltipResetDesignatorsForSelectedCategory".Translate());
        }

        private static bool DrawIconButton(Rect rect, Texture2D icon)
        {
            var iconRect = rect.ContractedBy(3f);
            return Widgets.ButtonImage(iconRect, icon);
        }

        private static void OpenAddCategoryMenu(string parentId, List<string> currentChildren)
        {
            var options = new List<FloatMenuOption>();
            var parentCategoryIds = new HashSet<string>(BamRuntime.GetParents().Select(d => d.defName));
            foreach (var def in DefDatabase<DesignationCategoryDef>.AllDefsListForReading
                         .Where(d => d.defName != parentId &&
                                     !currentChildren.Contains(d.defName) &&
                                     !parentCategoryIds.Contains(d.defName))
                         .OrderBy(d => d.LabelCap.ToString()))
            {
                var defName = def.defName;
                options.Add(new FloatMenuOption("BetterArchitectEditMode.CategoryOptionFormat".Translate(def.LabelCap, defName), delegate
                {
                    var updated = BamRuntime.GetChildrenForParent(parentId).ToList();
                    if (!updated.Contains(defName))
                    {
                        updated.Add(defName);
                        BamRuntime.SetChildrenForParent(parentId, updated);
                    }
                }));
            }

            if (!options.Any()) options.Add(new FloatMenuOption("BetterArchitectEditMode.NoCategoriesAvailable".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        internal static void MoveString(List<string> list, string value, int direction)
        {
            if (value.NullOrEmpty()) return;
            var idx = list.IndexOf(value);
            if (idx < 0) return;
            var target = idx + direction;
            if (target < 0 || target >= list.Count) return;
            var tmp = list[idx];
            list[idx] = list[target];
            list[target] = tmp;
        }

        internal static void ResetDesignatorsForCategory(string categoryId)
        {
            var entry = ModSettings.GetOrCreateCategoryOverride(categoryId);
            entry.replaceDefaultBuildables = false;
            entry.replaceDefaultSpecials = false;
            entry.buildableDefNames.Clear();
            entry.specialClassNames.Clear();
            entry.removedBuildableDefNames.Clear();
            Mod.Instance.WriteSettings();
        }
    }

    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "DrawDesignatorGrid")]
    public static class Bam_DrawDesignatorGrid_CaptureCategoryPatch
    {
        public static DesignationCategoryDef CurrentCategory;

        public static void Prefix(DesignationCategoryDef category)
        {
            CurrentCategory = category;
            if (ModSettings.editMode)
            {
                BetterArchitectSettings.groupByTechLevelPerCategory[category.defName] = false;
                if (!BetterArchitectSettings.sortSettingsPerCategory.ContainsKey(category.defName))
                {
                    BetterArchitectSettings.sortSettingsPerCategory[category.defName] = new SortSettings();
                }
                BetterArchitectSettings.sortSettingsPerCategory[category.defName].SortBy = SortBy.Default;
            }
        }
    }

    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "DrawFlatGrid")]
    public static class Bam_DrawFlatGrid_InlineDesignatorEditorPatch
    {
        public static void Prefix(Rect rect, List<Designator> designators)
        {
            if (!ModSettings.editMode || Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory == null) return;
            Bam_InlineDesignatorEditor.TryHandleOverlayInput(rect, designators, ArchitectCategoryTab_DesignationTabOnGUI_Patch.designatorGridScrollPosition, false);
        }

        public static void Postfix(Rect rect, List<Designator> designators)
        {
            if (!ModSettings.editMode || Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory == null) return;
            Bam_InlineDesignatorEditor.DrawGridOverlay(rect, designators, ArchitectCategoryTab_DesignationTabOnGUI_Patch.designatorGridScrollPosition, false);
        }
    }

    [HarmonyPatch(typeof(ArchitectCategoryTab_DesignationTabOnGUI_Patch), "DrawOrdersPanel")]
    public static class Bam_DrawOrdersPanel_InlineDesignatorEditorPatch
    {
        public static void Prefix(Rect rect, List<Designator> designators)
        {
            if (!ModSettings.editMode || Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory == null) return;
            var outRect = rect.ContractedBy(2f);
            outRect.width += 2f;
            Bam_InlineDesignatorEditor.TryHandleOverlayInput(outRect, designators, ArchitectCategoryTab_DesignationTabOnGUI_Patch.ordersScrollPosition, true);
        }

        public static void Postfix(Rect rect, List<Designator> designators)
        {
            if (!ModSettings.editMode || Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory == null) return;

            var outRect = rect.ContractedBy(2f);
            outRect.width += 2f;
            Bam_InlineDesignatorEditor.DrawGridOverlay(outRect, designators, ArchitectCategoryTab_DesignationTabOnGUI_Patch.ordersScrollPosition, true);
        }
    }

    public static class Bam_InlineDesignatorEditor
    {
        public static void TryHandleOverlayInput(Rect rect, List<Designator> designators, Vector2 scroll, bool twoColumns)
        {
            if (Event.current.type != EventType.MouseDown)
            {
                return;
            }

            const float gizmoSize = 75f;
            const float gizmoSpacing = 5f;
            const float rowHeight = gizmoSize + gizmoSpacing + 5f;
            var perRow = twoColumns ? 2 : Mathf.Max(1, Mathf.FloorToInt((rect.width - 16f) / (gizmoSize + gizmoSpacing)));

            for (int i = 0; i < designators.Count; i++)
            {
                var row = i / perRow;
                var col = i % perRow;
                var gizmoRect = new Rect(rect.x + col * (gizmoSize + gizmoSpacing), rect.y + row * rowHeight - scroll.y, gizmoSize, gizmoSize);
                if (gizmoRect.yMax < rect.y || gizmoRect.y > rect.yMax) continue;

                if (HandleOverlayClick(gizmoRect, designators, i))
                {
                    Event.current.Use();
                    return;
                }
            }

            var plusIndex = designators.Count;
            var plusRow = plusIndex / perRow;
            var plusCol = plusIndex % perRow;
            var plusRect = new Rect(rect.x + plusCol * (gizmoSize + gizmoSpacing) + 25f, rect.y + plusRow * rowHeight - scroll.y + 25f, 24f, 24f);
            if (plusRect.yMax >= rect.y && plusRect.y <= rect.yMax && Mouse.IsOver(plusRect))
            {
                OpenAddDesignatorMenu();
                Event.current.Use();
            }
        }

        public static void DrawGridOverlay(Rect rect, List<Designator> designators, Vector2 scroll, bool twoColumns)
        {
            const float gizmoSize = 75f;
            const float gizmoSpacing = 5f;
            const float rowHeight = gizmoSize + gizmoSpacing + 5f;

            var perRow = twoColumns ? 2 : Mathf.Max(1, Mathf.FloorToInt((rect.width - 16f) / (gizmoSize + gizmoSpacing)));

            for (int i = 0; i < designators.Count; i++)
            {
                var row = i / perRow;
                var col = i % perRow;
                var gizmoRect = new Rect(rect.x + col * (gizmoSize + gizmoSpacing), rect.y + row * rowHeight - scroll.y, gizmoSize, gizmoSize);
                if (gizmoRect.yMax < rect.y || gizmoRect.y > rect.yMax) continue;
                DrawButtonsForDesignator(gizmoRect, designators, i);
            }

            var plusIndex = designators.Count;
            var plusRow = plusIndex / perRow;
            var plusCol = plusIndex % perRow;
            var plusRect = new Rect(rect.x + plusCol * (gizmoSize + gizmoSpacing) + 25f, rect.y + plusRow * rowHeight - scroll.y + 25f, 24f, 24f);
            if (plusRect.yMax >= rect.y && plusRect.y <= rect.yMax)
            {
                if (Widgets.ButtonImage(plusRect, TexButton.Plus)) OpenAddDesignatorMenu();
                TooltipHandler.TipRegion(plusRect, "BetterArchitectEditMode.TooltipAddBuildableOrSpecialDesignator".Translate());
            }
        }

        private static void DrawButtonsForDesignator(Rect gizmoRect, List<Designator> designators, int index)
        {
            var designator = designators[index];
            var category = Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory;

            var minusRect = new Rect(gizmoRect.xMax - 14f, gizmoRect.y + 2f, 12f, 12f);
            var minusButtonRect = minusRect.ExpandedBy(2f);
            var minusExtraHeight = minusButtonRect.height * 0.5f;
            minusButtonRect.y -= minusExtraHeight * 0.5f;
            minusButtonRect.height += minusExtraHeight;
            var leftRect = new Rect(gizmoRect.x + 2f, gizmoRect.yMax - 14f, 12f, 12f);
            var rightRect = new Rect(gizmoRect.xMax - 14f, gizmoRect.yMax - 14f, 12f, 12f);

            if (Widgets.ButtonImage(minusButtonRect, TexButton.Minus)) RemoveDesignator(category, designators, designator);
            if (Widgets.ButtonImage(leftRect, TexButton.ReorderUp)) MoveDesignator(category, designators, designator, -1);
            if (Widgets.ButtonImage(rightRect, TexButton.ReorderDown)) MoveDesignator(category, designators, designator, 1);

            TooltipHandler.TipRegion(minusButtonRect, "BetterArchitectEditMode.TooltipRemoveDesignator".Translate());
            TooltipHandler.TipRegion(leftRect, "BetterArchitectEditMode.TooltipMoveLeftUp".Translate());
            TooltipHandler.TipRegion(rightRect, "BetterArchitectEditMode.TooltipMoveRightDown".Translate());
        }

        private static bool HandleOverlayClick(Rect gizmoRect, List<Designator> designators, int index)
        {
            var designator = designators[index];
            var category = Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory;

            var minusRect = new Rect(gizmoRect.xMax - 14f, gizmoRect.y + 2f, 12f, 12f);
            var minusButtonRect = minusRect.ExpandedBy(2f);
            var minusExtraHeight = minusButtonRect.height * 0.5f;
            minusButtonRect.y -= minusExtraHeight * 0.5f;
            minusButtonRect.height += minusExtraHeight;
            var leftRect = new Rect(gizmoRect.x + 2f, gizmoRect.yMax - 14f, 12f, 12f);
            var rightRect = new Rect(gizmoRect.xMax - 14f, gizmoRect.yMax - 14f, 12f, 12f);

            if (Mouse.IsOver(minusButtonRect))
            {
                RemoveDesignator(category, designators, designator);
                return true;
            }

            if (Mouse.IsOver(leftRect))
            {
                MoveDesignator(category, designators, designator, -1);
                return true;
            }

            if (Mouse.IsOver(rightRect))
            {
                MoveDesignator(category, designators, designator, 1);
                return true;
            }

            return false;
        }

        private static void RemoveDesignator(DesignationCategoryDef category, List<Designator> currentDesignators, Designator target)
        {
            var entry = ModSettings.GetOrCreateCategoryOverride(category.defName);
            var buildableDefName = GetBuildableDefName(target);

            if (!buildableDefName.NullOrEmpty())
            {
                if (entry.replaceDefaultBuildables) entry.buildableDefNames.Remove(buildableDefName);
                else if (!entry.removedBuildableDefNames.Contains(buildableDefName)) entry.removedBuildableDefNames.Add(buildableDefName);
                entry.buildableDefNames.Remove(buildableDefName);
            }
            else
            {
                var className = target.GetType().FullName;
                if (!entry.replaceDefaultSpecials)
                {
                    entry.replaceDefaultSpecials = true;
                    entry.specialClassNames = currentDesignators.Where(d => GetBuildableDefName(d).NullOrEmpty()).Select(d => d.GetType().FullName).Where(n => !n.NullOrEmpty()).Distinct().ToList();
                }
                entry.specialClassNames.Remove(className);
            }

            Mod.Instance.WriteSettings();
        }

        private static void MoveDesignator(DesignationCategoryDef category, List<Designator> currentDesignators, Designator target, int dir)
        {
            var entry = ModSettings.GetOrCreateCategoryOverride(category.defName);
            var buildableDefName = GetBuildableDefName(target);

            if (!buildableDefName.NullOrEmpty())
            {
                if (entry.buildableDefNames == null || entry.buildableDefNames.Count == 0)
                {
                    entry.buildableDefNames = currentDesignators.Select(GetBuildableDefName).Where(n => !n.NullOrEmpty()).Distinct().ToList();
                }
                if (!entry.buildableDefNames.Contains(buildableDefName)) entry.buildableDefNames.Add(buildableDefName);
                Bam_HandleCategorySelection_InMenuButtonsPatch.MoveString(entry.buildableDefNames, buildableDefName, dir);
                entry.removedBuildableDefNames.Remove(buildableDefName);
            }
            else
            {
                var className = target.GetType().FullName;
                if (!entry.replaceDefaultSpecials)
                {
                    entry.replaceDefaultSpecials = true;
                    entry.specialClassNames = currentDesignators.Where(d => GetBuildableDefName(d).NullOrEmpty()).Select(d => d.GetType().FullName).Where(n => !n.NullOrEmpty()).Distinct().ToList();
                }
                if (!entry.specialClassNames.Contains(className)) entry.specialClassNames.Add(className);
                Bam_HandleCategorySelection_InMenuButtonsPatch.MoveString(entry.specialClassNames, className, dir);
            }

            Mod.Instance.WriteSettings();
        }

        private static void OpenAddDesignatorMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("BetterArchitectEditMode.AddBuildable".Translate(), delegate
                {
                    Find.WindowStack.Add(new DesignatorSearchWindow(
                        "BetterArchitectEditMode.AddBuildableTitle".Translate(),
                        DesignatorSearchMode.Buildable,
                        delegate(string value) { AddBuildableToCurrentCategory(value); }));
                }),
                new FloatMenuOption("BetterArchitectEditMode.AddSpecialDesignator".Translate(), delegate
                {
                    Find.WindowStack.Add(new DesignatorSearchWindow(
                        "BetterArchitectEditMode.AddSpecialDesignatorTitle".Translate(),
                        DesignatorSearchMode.Special,
                        delegate(string value) { AddSpecialToCurrentCategory(value); }));
                })
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void AddBuildableToCurrentCategory(string defName)
        {
            var category = Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory;
            var entry = ModSettings.GetOrCreateCategoryOverride(category.defName);
            if (!entry.buildableDefNames.Contains(defName)) entry.buildableDefNames.Add(defName);
            entry.removedBuildableDefNames.Remove(defName);
            Mod.Instance.WriteSettings();
        }

        private static void AddSpecialToCurrentCategory(string className)
        {
            var category = Bam_DrawDesignatorGrid_CaptureCategoryPatch.CurrentCategory;
            var entry = ModSettings.GetOrCreateCategoryOverride(category.defName);
            if (!entry.specialClassNames.Contains(className)) entry.specialClassNames.Add(className);
            Mod.Instance.WriteSettings();
        }

        private static string GetBuildableDefName(Designator d)
        {
            if (d is Designator_Build db) return db.PlacingDef != null ? db.PlacingDef.defName : null;
            if (d is Designator_Dropdown dd)
            {
                var nested = dd.Elements.OfType<Designator_Build>().FirstOrDefault();
                return nested != null && nested.PlacingDef != null ? nested.PlacingDef.defName : null;
            }
            return null;
        }
    }

    public enum DesignatorSearchMode
    {
        Buildable,
        Special
    }

    public class DesignatorChoice
    {
        public string key;
        public string label;
        public string secondary;
        public string searchText;
    }

    public static class DesignatorSearchCache
    {
        private static readonly List<DesignatorChoice> buildables = new List<DesignatorChoice>();
        private static readonly List<DesignatorChoice> specials = new List<DesignatorChoice>();
        private static int builtAtCacheVersion = -1;

        public static IReadOnlyList<DesignatorChoice> GetChoices(DesignatorSearchMode mode)
        {
            if (builtAtCacheVersion != BamRuntime.CacheVersion)
            {
                Rebuild();
            }

            return mode == DesignatorSearchMode.Buildable ? buildables : specials;
        }

        public static void Rebuild()
        {
            buildables.Clear();
            specials.Clear();

            foreach (var def in DefDatabase<BuildableDef>.AllDefsListForReading)
            {
                if (!def.BuildableByPlayer)
                {
                    continue;
                }

                var label = def.LabelCap.ToString();
                if (label.NullOrEmpty())
                {
                    label = def.defName;
                }

                buildables.Add(new DesignatorChoice
                {
                    key = def.defName,
                    label = label,
                    secondary = def.defName,
                    searchText = (label + " " + def.defName).ToLowerInvariant()
                });
            }

            var classNames = new HashSet<string>();
            foreach (var cat in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                if (cat.specialDesignatorClasses != null)
                {
                    foreach (var type in cat.specialDesignatorClasses)
                    {
                        if (type != null && !type.FullName.NullOrEmpty())
                        {
                            classNames.Add(type.FullName);
                        }
                    }
                }

                if (cat.ResolvedAllowedDesignators != null)
                {
                    foreach (var d in cat.ResolvedAllowedDesignators)
                    {
                        if (d is Designator_Build)
                        {
                            continue;
                        }

                        var fullName = d != null ? d.GetType().FullName : null;
                        if (!fullName.NullOrEmpty())
                        {
                            classNames.Add(fullName);
                        }
                    }
                }
            }

            foreach (var entry in ModSettings.categoryOverrides.Values)
            {
                foreach (var className in entry.specialClassNames)
                {
                    if (!className.NullOrEmpty())
                    {
                        classNames.Add(className);
                    }
                }
            }

            foreach (var className in classNames)
            {
                var simpleName = className.Split('.').Last();
                specials.Add(new DesignatorChoice
                {
                    key = className,
                    label = simpleName,
                    secondary = className,
                    searchText = (simpleName + " " + className).ToLowerInvariant()
                });
            }

            buildables.Sort(delegate(DesignatorChoice a, DesignatorChoice b)
            {
                var cmp = string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                return string.Compare(a.secondary, b.secondary, StringComparison.OrdinalIgnoreCase);
            });

            specials.Sort(delegate(DesignatorChoice a, DesignatorChoice b)
            {
                var cmp = string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                return string.Compare(a.secondary, b.secondary, StringComparison.OrdinalIgnoreCase);
            });

            builtAtCacheVersion = BamRuntime.CacheVersion;
        }
    }

    public class DesignatorSearchWindow : Window
    {
        private readonly Action<string> onPick;
        private readonly string title;
        private readonly DesignatorSearchMode mode;

        private string query = "";
        private string lastQuery = null;
        private readonly List<DesignatorChoice> filtered = new List<DesignatorChoice>();
        private Vector2 scrollPosition;
        private bool focusSearchOnOpen = true;

        private const float RowHeight = 30f;
        private const float FooterHeight = 30f;
        private const float SearchHeight = 30f;
        private const float HeaderHeight = 28f;
        private const string SearchFieldControlName = "BAMEditMode_DesignatorSearchField";

        public override Vector2 InitialSize { get { return new Vector2(760f, 620f); } }

        public DesignatorSearchWindow(string title, DesignatorSearchMode mode, Action<string> onPick)
        {
            this.title = title;
            this.mode = mode;
            this.onPick = onPick;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, HeaderHeight), title);

            GUI.SetNextControlName(SearchFieldControlName);
            query = Widgets.TextField(new Rect(0f, HeaderHeight + 4f, inRect.width, SearchHeight - 4f), query);
            if (focusSearchOnOpen && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SearchFieldControlName);
                focusSearchOnOpen = false;
            }
            var listRect = new Rect(0f, HeaderHeight + SearchHeight + 4f, inRect.width, inRect.height - HeaderHeight - SearchHeight - FooterHeight - 8f);
            var footerRect = new Rect(0f, listRect.yMax + 4f, inRect.width, FooterHeight);

            DrawChoiceList(listRect);

            var sourceCount = DesignatorSearchCache.GetChoices(mode).Count;
            Widgets.Label(new Rect(0f, footerRect.y + 6f, footerRect.width - 120f, 24f), "BetterArchitectEditMode.SearchShowing".Translate(filtered.Count, sourceCount));
            if (Widgets.ButtonText(new Rect(footerRect.width - 100f, footerRect.y + 2f, 100f, 24f), "BetterArchitectEditMode.Close".Translate()))
            {
                Close();
            }
        }

        private void DrawChoiceList(Rect outRect)
        {
            RefreshFilterIfNeeded();

            var viewRect = new Rect(0f, 0f, outRect.width - 16f, filtered.Count * RowHeight);
            Widgets.DrawBoxSolid(outRect, new Color(0f, 0f, 0f, 0.2f));
            Widgets.DrawBox(outRect, 1);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            var start = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / RowHeight));
            var visible = Mathf.CeilToInt(outRect.height / RowHeight) + 2;
            var end = Mathf.Min(filtered.Count, start + visible);

            for (int i = start; i < end; i++)
            {
                var rowRect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                DrawChoiceRow(rowRect, filtered[i], query);
            }

            Widgets.EndScrollView();
        }

        private void DrawChoiceRow(Rect rowRect, DesignatorChoice choice, string filterQuery)
        {
            Widgets.DrawHighlightIfMouseover(rowRect);
            if (Widgets.ButtonInvisible(rowRect))
            {
                if (onPick != null && !choice.key.NullOrEmpty())
                {
                    onPick(choice.key);
                }
                Close();
                return;
            }

            var labelRect = new Rect(rowRect.x + 6f, rowRect.y + 2f, rowRect.width * 0.45f, rowRect.height - 4f);
            var secondaryRect = new Rect(rowRect.x + rowRect.width * 0.45f + 12f, rowRect.y + 2f, rowRect.width * 0.55f - 16f, rowRect.height - 4f);
            DrawHighlightedLabel(labelRect, choice.label, filterQuery, Color.white);
            DrawHighlightedLabel(secondaryRect, choice.secondary, filterQuery, Color.gray);
        }

        private void RefreshFilterIfNeeded()
        {
            var normalized = query.NullOrEmpty() ? "" : query.Trim().ToLowerInvariant();
            if (normalized == lastQuery)
            {
                return;
            }

            lastQuery = normalized;
            filtered.Clear();
            var source = DesignatorSearchCache.GetChoices(mode);
            if (normalized.NullOrEmpty())
            {
                filtered.AddRange(source);
                return;
            }

            foreach (var choice in source)
            {
                if (choice.searchText.Contains(normalized))
                {
                    filtered.Add(choice);
                }
            }
        }

        private static void DrawHighlightedLabel(Rect rect, string text, string filterQuery, Color baseColor)
        {
            if (text.NullOrEmpty())
            {
                return;
            }

            if (filterQuery.NullOrEmpty())
            {
                GUI.color = baseColor;
                Widgets.Label(rect, text);
                GUI.color = Color.white;
                return;
            }

            var idx = text.IndexOf(filterQuery, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                GUI.color = baseColor;
                Widgets.Label(rect, text);
                GUI.color = Color.white;
                return;
            }

            var before = text.Substring(0, idx);
            var match = text.Substring(idx, filterQuery.Length > text.Length - idx ? text.Length - idx : filterQuery.Length);
            var after = text.Substring(idx + match.Length);

            var beforeSize = Text.CalcSize(before).x;
            var matchSize = Text.CalcSize(match).x;

            GUI.color = baseColor;
            Widgets.Label(rect, before);

            var matchRect = new Rect(rect.x + beforeSize, rect.y + 2f, matchSize, rect.height - 4f);
            Widgets.DrawBoxSolid(matchRect, new Color(1f, 0.92f, 0.35f, 0.35f));
            GUI.color = Color.white;
            Widgets.Label(new Rect(rect.x + beforeSize, rect.y, rect.width - beforeSize, rect.height), match);

            GUI.color = baseColor;
            Widgets.Label(new Rect(rect.x + beforeSize + matchSize, rect.y, rect.width - beforeSize - matchSize, rect.height), after);
            GUI.color = Color.white;
        }
    }
}

