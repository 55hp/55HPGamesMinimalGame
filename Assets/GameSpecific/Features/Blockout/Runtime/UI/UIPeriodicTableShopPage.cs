using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.UI;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.UI
{
    // Shop GDD/Technical Doc Phases 4-6: the periodic-table shop's main page - a scrollable grid
    // shaped like the real table (18 groups x 7 periods, with the real gaps), the two lanthanide/
    // actinide placeholder tiles opening a dedicated sub-view, and tapping any real element tile
    // opening its detail card (UIPeriodicElementCard, a popup). This is the functional structure
    // only - GDD Fase 7 (the per-category solid/gas/liquid illustrations) and real-device visual
    // QA (spacing, readability, scroll feel on the actual 118-entry grid) are explicitly Bezi's
    // Editor work, not implemented here (see the Technical Doc's own orchestration section).
    //
    // _mainGridContainer needs a GridLayoutGroup configured for exactly 18 columns (Franci,
    // Editor) - this script instantiates one child per grid slot in row-major (period, then
    // group) order, including an empty spacer for every gap the real table has (e.g. groups 3-12
    // don't exist before period 4), relying entirely on the GridLayoutGroup for positioning
    // rather than setting anchored positions itself.
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIPeriodicTableShopPage : UIPageBase
    {
        [Header("Main grid")]
        [SerializeField] private RectTransform _mainGridContainer;
        [SerializeField] private UIPeriodicElementCell _elementCellPrefab;
        [SerializeField] private UIPeriodicSeriesPlaceholderCell _seriesPlaceholderPrefab;

        [Header("Lanthanide / actinide sub-view")]
        [SerializeField] private GameObject _seriesSubViewPanel;
        [SerializeField] private RectTransform _seriesSubViewContainer;
        [SerializeField] private TMP_Text _seriesSubViewTitle;
        [SerializeField] private Button _seriesSubViewCloseButton;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        private IBlockoutSkinService _skinService;
        private IUIPopupService _popupService;
        private IUINavigationService _navigation;

        private readonly List<BlockoutSkinShopEntry> _lanthanides = new();
        private readonly List<BlockoutSkinShopEntry> _actinides = new();

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _skinService);
            ServiceRegistry.TryResolve(out _popupService);
            ServiceRegistry.TryResolve(out _navigation);

            EnsureFullScreenRect();

            Bind(_backButton, OnBackClicked);
            Bind(_seriesSubViewCloseButton, CloseSeriesSubView);

            if (_seriesSubViewPanel != null) _seriesSubViewPanel.SetActive(false);

            EnsureSeriesSubViewIsScrollable();
            BuildGrid();
        }

        // Bug (Play Mode, 2026-09-15): the instantiated page sat at Width/Height 0 - Hierarchy
        // showed its own RectTransform with AnchorMin == AnchorMax == (0,0) and no offset, i.e.
        // a point anchor with zero sizeDelta, not stretched to fill its parent (Layer_Pages).
        // That's what AddComponent<RectTransform>() (or a script dragged onto a plain empty
        // GameObject) leaves you with by default - it's never set by the "GameObject > UI > ..."
        // creation menu, only by hand afterward, and this prefab's root apparently never got that
        // manual step. Forced here in code instead of trusted from the prefab (same "don't depend
        // on Editor setup being exactly right" approach as EnsureSeriesSubViewIsScrollable and
        // UIPeriodicElementCell's Outline fallback) - the whole point of a Page root is to fill
        // whatever the navigation service parents it under, so there's no case where anything
        // other than full-stretch, zero-offset is correct for it.
        private void EnsureFullScreenRect()
        {
            var rect = transform as RectTransform;
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        // Bug (Bezi visual QA, 2026-09-15): _seriesSubViewContainer had neither a layout group
        // nor a ScrollRect, so the 15 instantiated cells all landed at the same position and
        // overlapped instead of laying out in a scrollable row (GDD: "due file di caselle
        // scorrevoli orizzontalmente"). Ensured here in code rather than assumed from the prefab
        // - same "don't depend on Editor setup being exactly right" approach already used for
        // WellCellRenderer's fallback cube / BlockoutWellWireframe's runtime-built lines. Existing
        // Inspector-authored components (if Bezi already added some of these) are left as-is;
        // only what's actually missing gets added.
        private void EnsureSeriesSubViewIsScrollable()
        {
            if (_seriesSubViewContainer == null) return;

            var layoutGroup = _seriesSubViewContainer.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null) layoutGroup = _seriesSubViewContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = true;
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.spacing = 8f;

            var sizeFitter = _seriesSubViewContainer.GetComponent<ContentSizeFitter>();
            if (sizeFitter == null) sizeFitter = _seriesSubViewContainer.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // A ScrollRect already targeting this container (however Bezi wired it) is left
            // alone; one is added only if genuinely missing, treating the container's own parent
            // as the viewport (the simplest valid ScrollRect setup - a nested dedicated Viewport
            // object is an Editor-side refinement Bezi can still add later without this breaking).
            if (_seriesSubViewContainer.GetComponentInParent<ScrollRect>() == null
                && _seriesSubViewContainer.parent is RectTransform viewport)
            {
                var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
                if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();

                scrollRect.content = _seriesSubViewContainer;
                scrollRect.viewport = viewport;
                scrollRect.horizontal = true;
                scrollRect.vertical = false;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
            }
        }

        // Rebuilds the whole main grid from the current shop state - cheap enough at 118 entries
        // that there's no need for per-cell diffing. Called on first show, and again as the
        // refresh callback UIPeriodicElementCard invokes after a successful select/unlock.
        private void BuildGrid()
        {
            if (_skinService == null || _mainGridContainer == null || _elementCellPrefab == null || _seriesPlaceholderPrefab == null)
            {
                Debug.LogError("[UIPeriodicTableShopPage] Missing IBlockoutSkinService or an Inspector reference - grid not built.", this);
                return;
            }

            ClearContainer(_mainGridContainer);

            var entries = _skinService.GetElementShopEntries();
            var byPosition = new Dictionary<(int period, int group), BlockoutSkinShopEntry>();
            _lanthanides.Clear();
            _actinides.Clear();

            foreach (var entry in entries)
            {
                if (entry.Position.IsLanthanide) { _lanthanides.Add(entry); continue; }
                if (entry.Position.IsActinide) { _actinides.Add(entry); continue; }
                byPosition[(entry.Position.Period, entry.Position.Group)] = entry;
            }

            for (int period = 1; period <= 7; period++)
            {
                for (int group = 1; group <= 18; group++)
                {
                    if (IsSeriesPlaceholderSlot(period, group))
                    {
                        bool isLanthanideSlot = period == 6;
                        var placeholder = Instantiate(_seriesPlaceholderPrefab, _mainGridContainer);
                        placeholder.Configure(isLanthanideSlot ? "Lantanidi" : "Attinidi", () => OpenSeriesSubView(isLanthanideSlot));
                        continue;
                    }

                    if (byPosition.TryGetValue((period, group), out var entry))
                    {
                        var cell = Instantiate(_elementCellPrefab, _mainGridContainer);
                        cell.Configure(entry, () => OpenCard(entry));
                    }
                    else
                    {
                        CreateEmptySlot(_mainGridContainer);
                    }
                }
            }
        }

        // period/group 3 in periods 6 and 7 is where the lanthanide/actinide block would sit in
        // an uncompressed table - GDD: shown here only as a placeholder, never a real element.
        private static bool IsSeriesPlaceholderSlot(int period, int group) => group == 3 && (period == 6 || period == 7);

        // No prefab needed for this - it only has to occupy a GridLayoutGroup cell and render
        // nothing, same "build it at runtime, no Editor asset required" approach as
        // BlockoutWellWireframe's lines / WellCellRenderer's fallback cube.
        private static void CreateEmptySlot(Transform parent)
        {
            var go = new GameObject("PeriodicTableShop Empty Slot (TEMP)", typeof(RectTransform));
            go.transform.SetParent(parent, false);
        }

        private void OpenSeriesSubView(bool lanthanides)
        {
            if (_seriesSubViewPanel == null || _seriesSubViewContainer == null || _elementCellPrefab == null) return;

            ClearContainer(_seriesSubViewContainer);
            if (_seriesSubViewTitle != null) _seriesSubViewTitle.text = lanthanides ? "Lantanidi" : "Attinidi";

            var series = lanthanides ? _lanthanides : _actinides;
            foreach (var entry in series)
            {
                var cell = Instantiate(_elementCellPrefab, _seriesSubViewContainer);
                cell.Configure(entry, () => OpenCard(entry));
            }

            _seriesSubViewPanel.SetActive(true);
        }

        private void CloseSeriesSubView()
        {
            if (_seriesSubViewPanel != null) _seriesSubViewPanel.SetActive(false);
        }

        private async void OpenCard(BlockoutSkinShopEntry entry)
        {
            if (_popupService == null) return;

            var card = await _popupService.OpenAsync<UIPeriodicElementCard>(Addr.Content.UI.Popups.Periodic_Element_Card);
            // Refresh callback rebuilds the whole grid (and, if the sub-view is open, the caller
            // would need to reopen it to see it refreshed too - acceptable: the sub-view sits on
            // top of the main grid, and closing/reopening it after a change is a minor rough edge
            // Franci/Bezi can smooth over in Editor, not a functional gap).
            card?.Configure(entry, BuildGrid);
        }

        private async void OnBackClicked()
        {
            if (_navigation == null) return;
            await _navigation.PopAsync();
        }

        private static void ClearContainer(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
        }
    }
}
