using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.UI;
using hp55games.Blockout.Achievements;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.UI
{
    // Shop GDD "Interazione": the detail card a tapped element tile expands into - symbol, name,
    // atomic number, state, cost/unlock condition, and the one action available for that state
    // (select if unlocked, buy if a Coins-method element with enough coins; an Achievement-method
    // element shows its condition as text only, nothing to press). Closes via
    // UIPopupBase/IUIPopupService - including tapping the scrim (built into the popup service),
    // matching the GDD's "tap fuori dalla card... torna alla vista tavola periodica".
    public sealed class UIPeriodicElementCard : UIPopupBase
    {
        [SerializeField] private TMP_Text _symbolLabel;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _atomicNumberLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _conditionLabel;
        [SerializeField] private Image _elementColor;
        [SerializeField] private Button _actionButton;
        [SerializeField] private TMP_Text _actionButtonLabel;
        [SerializeField] private Button _closeButton;

        private IBlockoutSkinService _skinService;
        private IBlockoutAchievementService _achievements;
        private ISaveService _save;
        private BlockoutSkinShopEntry _entry;
        private Action _onChanged;

        protected override void Awake()
        {
            base.Awake();
            ServiceRegistry.TryResolve(out _skinService);
            ServiceRegistry.TryResolve(out _achievements);
            ServiceRegistry.TryResolve(out _save);

            Bind(_closeButton, ClosePopup);

            // Bug (Bezi visual QA, 2026-09-15): long element names (e.g. "Hydrogen") were
            // spilling outside their label's box. Auto-sizing shrinks the font to whatever
            // actually fits instead of clipping - independent of the exact RectTransform width
            // authored in the prefab, so it stays correct even if that width changes later.
            ConfigureAutoSizing(_symbolLabel);
            ConfigureAutoSizing(_nameLabel);
            ConfigureAutoSizing(_atomicNumberLabel);
            ConfigureAutoSizing(_statusLabel);
            ConfigureAutoSizing(_conditionLabel);
            ConfigureAutoSizing(_actionButtonLabel);
        }

        // Caps auto-sizing at whatever font size was already authored in the Inspector (never
        // grows text beyond the designed look, only shrinks it when content would otherwise
        // overflow), with a small floor so it never shrinks to the point of being unreadable -
        // Truncate is only a last-resort safety net below that floor.
        private static void ConfigureAutoSizing(TMP_Text label)
        {
            if (label == null) return;

            float authoredSize = label.fontSize > 0 ? label.fontSize : 36f;
            label.enableAutoSizing = true;
            label.fontSizeMax = authoredSize;
            label.fontSizeMin = Mathf.Max(6f, authoredSize * 0.4f);
            label.overflowMode = TextOverflowModes.Truncate;
        }

        // onChanged: called right after a successful select/unlock action, while the popup is
        // still open (it closes only via the scrim tap or the close button) - lets the page
        // behind refresh its grid so the change is visible by the time this card closes.
        public void Configure(BlockoutSkinShopEntry entry, Action onChanged)
        {
            _entry = entry;
            _onChanged = onChanged;
            Refresh();
        }

        private void Refresh()
        {
            var skin = _entry.Skin;

            if (_symbolLabel != null) _symbolLabel.text = skin.ElementSymbol;
            if (_nameLabel != null) _nameLabel.text = skin.DisplayName;
            if (_atomicNumberLabel != null) _atomicNumberLabel.text = skin.AtomicNumber.ToString();

            if (_elementColor != null)
            {
                var colors = skin.PieceColors;
                _elementColor.color = colors != null && colors.Count > 0 ? colors[0] : Color.white;
            }

            bool isUnlocked = _skinService != null && _skinService.IsUnlocked(skin);
            bool isActive = _skinService != null && ReferenceEquals(_skinService.ActiveSkin, skin);

            if (_statusLabel != null)
                _statusLabel.text = isActive ? "Attivo" : isUnlocked ? "Sbloccato" : "Bloccato";

            ConfigureCondition(isUnlocked, skin);
            ConfigureAction(isUnlocked, isActive, skin);
        }

        private void ConfigureCondition(bool isUnlocked, BlockoutSkin skin)
        {
            if (_conditionLabel == null) return;

            if (isUnlocked)
            {
                _conditionLabel.text = string.Empty;
                return;
            }

            switch (skin.UnlockMethod)
            {
                case BlockoutSkinUnlockMethod.Coins:
                    _conditionLabel.text = skin.HasCostSet ? $"{skin.CostInCoins} coins" : "Costo non ancora disponibile";
                    break;
                case BlockoutSkinUnlockMethod.Achievement:
                    _conditionLabel.text = _achievements != null
                        ? _achievements.GetDescription(skin.UnlockAchievementId)
                        : skin.UnlockAchievementId;
                    break;
                default:
                    _conditionLabel.text = string.Empty;
                    break;
            }
        }

        private void ConfigureAction(bool isUnlocked, bool isActive, BlockoutSkin skin)
        {
            if (_actionButton == null) return;

            _actionButton.onClick.RemoveAllListeners();

            if (isActive)
            {
                _actionButton.gameObject.SetActive(false);
                return;
            }

            if (isUnlocked)
            {
                _actionButton.gameObject.SetActive(true);
                if (_actionButtonLabel != null) _actionButtonLabel.text = "Seleziona";
                _actionButton.interactable = true;
                _actionButton.onClick.AddListener(OnSelectClicked);
                return;
            }

            if (skin.UnlockMethod == BlockoutSkinUnlockMethod.Coins && skin.HasCostSet)
            {
                _actionButton.gameObject.SetActive(true);
                if (_actionButtonLabel != null) _actionButtonLabel.text = $"Sblocca ({skin.CostInCoins})";
                _actionButton.interactable = _save != null && _save.Data.coins >= skin.CostInCoins;
                _actionButton.onClick.AddListener(OnUnlockClicked);
                return;
            }

            // Achievement-method, or a Coins-method skin whose cost isn't set yet: nothing to press.
            _actionButton.gameObject.SetActive(false);
        }

        private void OnSelectClicked()
        {
            _skinService?.SetActiveSkin(_entry.Skin.SkinId);
            _onChanged?.Invoke();
            Refresh();
        }

        private void OnUnlockClicked()
        {
            _skinService?.TryUnlockSkin(_entry.Skin.SkinId);
            _onChanged?.Invoke();
            Refresh();
        }
    }
}
