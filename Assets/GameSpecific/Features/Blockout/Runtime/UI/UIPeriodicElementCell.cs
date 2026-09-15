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

        [Tooltip("Optional custom active-state visual (e.g. a small corner badge), assigned in the Inspector. If left unassigned, a built-in Outline border on the element color swatch is used instead (see Awake) - GDD requires the active indicator not cover the cell's content, so this deliberately never defaults to a full-cover image.")]
        [SerializeField] private GameObject _activeIndicator;

        // Fallback active indicator, built at runtime rather than depending on a prefab-authored
        // asset: Outline only draws a colored silhouette offset around the swatch's existing
        // shape, so - unlike a solid overlay Image - it structurally cannot obscure the symbol/
        // color underneath. Used only when _activeIndicator isn't assigned; a real badge Bezi
        // wires up later takes precedence (see Configure).
        private Outline _activeOutline;
        private static readonly Color ActiveOutlineColor = new(1f, 0.84f, 0.2f, 1f);
        private static readonly Vector2 ActiveOutlineDistance = new(3f, -3f);

        private void Awake()
        {
            if (_activeIndicator == null && _elementColor != null)
            {
                _activeOutline = _elementColor.GetComponent<Outline>();
                if (_activeOutline == null) _activeOutline = _elementColor.gameObject.AddComponent<Outline>();

                _activeOutline.effectColor = ActiveOutlineColor;
                _activeOutline.effectDistance = ActiveOutlineDistance;
                _activeOutline.enabled = false;
            }
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
            else if (_activeOutline != null) _activeOutline.enabled = entry.IsActive;

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onTapped?.Invoke());
            }
        }
    }
}
