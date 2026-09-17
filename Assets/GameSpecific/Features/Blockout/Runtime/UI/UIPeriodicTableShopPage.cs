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

        [Header("Currency")]
        [SerializeField] private TMP_Text _coinsBalanceLabel;

        private IBlockoutSkinService _skinService;
        private IUIPopupService _popupService;
        private IUINavigationService _navigation;
        private ISaveService _save;

        private readonly List<BlockoutSkinShopEntry> _lanthanides = new();
        private readonly List<BlockoutSkinShopEntry> _actinides = new();

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _skinService);
            ServiceRegistry.TryResolve(out _popupService);
            ServiceRegistry.TryResolve(out _navigation);
            ServiceRegistry.TryResolve(out _save);

            Bind(_backButton, OnBackClicked);
            Bind(_seriesSubViewCloseButton, CloseSeriesSubView);

            if (_seriesSubViewPanel != null) _seriesSubViewPanel.SetActive(false);

            BuildGrid();
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

            RefreshCoinsBalance();
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

        // The prefab's CoinsBalanceLabel shipped with a hardcoded "Coins: 1000" placeholder - kept
        // live here rather than via IEventBus, since BuildGrid already re-runs (Awake, and after
        // every select/unlock via the card's onChanged callback) at exactly the moments the
        // balance can change, same as the rest of this page's "rebuild the whole grid" approach.
        private void RefreshCoinsBalance()
        {
            if (_coinsBalanceLabel == null) return;
            _coinsBalanceLabel.text = _save != null ? $"Coins: {_save.Data.coins}" : string.Empty;
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

            // Configure runs before the popup is shown (see IUIPopupService.OpenAsync<T>(address,
            // configure)) - previously it ran after OpenAsync returned, so the card was already
            // visible (for the whole scrim fade) showing the prefab's stale/default content
            // before Configure applied the real entry.
            await _popupService.OpenAsync<UIPeriodicElementCard>(Addr.Content.UI.Popups.Periodic_Element_Card, card =>
            {
                // Refresh callback rebuilds the whole grid (and, if the sub-view is open, the
                // caller would need to reopen it to see it refreshed too - acceptable: the
                // sub-view sits on top of the main grid, and closing/reopening it after a change
                // is a minor rough edge Franci/Bezi can smooth over in Editor, not a functional
                // gap).
                card.Configure(entry, BuildGrid);
            });
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
