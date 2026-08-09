// hp55games.Ui
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using hp55games.Mobile.Core.UI;

namespace hp55games.Ui
{
    public class UIPopup_Generic : UIPopupBase
    {
        public CanvasGroup cg;
        public TMP_Text title;
        public TMP_Text body;
        public Button confirm;
        public Button cancel;

        public void Open(string t, string b, System.Action onConfirm, System.Action onCancel = null)
        {
            title.text = t; body.text = b;

            Bind(confirm, () => { onConfirm?.Invoke(); ClosePopup(); });
            if (cancel != null)
                Bind(cancel, () => { onCancel?.Invoke(); ClosePopup(); });

            cg.alpha = 1; cg.blocksRaycasts = true; cg.interactable = true;
        }

        /// <summary>Public wrapper for backwards compatibility: always closes through the service now, not just by lowering alpha.</summary>
        public void Close() => ClosePopup();
    }
}