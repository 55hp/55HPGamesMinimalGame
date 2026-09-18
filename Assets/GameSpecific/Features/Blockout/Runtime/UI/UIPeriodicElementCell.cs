using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.UI
{
    // Shop GDD "Stato visivo delle caselle": one periodic-table tile, reused both in the main
    // grid and in the lanthanide/actinide sub-view (same look, same 3 states - locked/unlocked/
    // active, not just 2). Pure view: all data comes from Configure's BlockoutSkinShopEntry: this
    // class resolves no services itself, it only fires the tap callback the owning page supplies
    // (which decides what a tap does - open the detail card).
    public sealed class UIPeriodicElementCell : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _elementColor;
        [SerializeField] private TMP_Text _symbolLabel;

        [Tooltip("Shown only when the element is locked - GDD 'teca spenta': a dark/desaturated overlay, no glow.")]
        [SerializeField] private GameObject _lockedOverlay;

        [Tooltip("Active-state visual (e.g. an Outline component on the element color swatch, or a small corner badge), shown only while this skin is active. GDD requires it not cover the cell's content (symbol/color), so it must never be a full-cover image.")]
        [SerializeField] private GameObject _activeIndicator;

        private void Awake()
        {
            if (_activeIndicator == null)
                Debug.LogError("[UIPeriodicElementCell] _activeIndicator is not assigned in the Inspector.", this);
        }

        public void Configure(BlockoutSkinShopEntry entry, Action onTapped)
        {
            if (_symbolLabel != null) _symbolLabel.text = entry.Skin.ElementSymbol;

            if (_elementColor != null)
            {
                var colors = entry.Skin.PieceColors;
                _elementColor.color = colors != null && colors.Count > 0 ? colors[0] : Color.white;
            }

            if (_lockedOverlay != null) _lockedOverlay.SetActive(!entry.IsUnlocked);

            if (_activeIndicator != null) _activeIndicator.SetActive(entry.IsActive);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onTapped?.Invoke());
            }
        }
    }
}
