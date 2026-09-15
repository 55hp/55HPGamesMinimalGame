using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Blockout.UI
{
    // Shop GDD "Layout": the two main-grid placeholder tiles (period 6 / period 7, both at group
    // 3) that open the lanthanide/actinide sub-view instead of a detail card - lanthanides
    // (57-71) and actinides (89-103) never occupy their real grid slot directly, only this shared
    // stand-in per series.
    public sealed class UIPeriodicSeriesPlaceholderCell : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

        public void Configure(string label, Action onTapped)
        {
            if (_label != null) _label.text = label;

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onTapped?.Invoke());
            }
        }
    }
}
