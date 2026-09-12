using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Blockout.Gameplay
{
    // TEMP debug readout: current fall interval, phase index, and whether spawning has stopped
    // (well full). Code-only - creates its own Canvas/Text if none exist yet in the scene, no
    // manual scene/camera setup required. Delete once a real HUD covers this.
    public sealed class BlockoutDebugOverlay : MonoBehaviour
    {
        private BlockoutSpawner _spawner;
        private TMP_Text _label;

        private void Awake()
        {
            _spawner = FindObjectOfType<BlockoutSpawner>();
            if (_spawner == null)
            {
                Debug.LogError("[BlockoutDebugOverlay] No BlockoutSpawner found in the scene.", this);
                enabled = false;
                return;
            }

            _label = CreateLabel();
        }

        private void Update()
        {
            var piece = _spawner.CurrentPiece;

            string interval = piece != null ? piece.CurrentInterval.ToString("F2") : "-";
            string phase = piece != null ? piece.PhaseIndex.ToString() : "-";
            string status = _spawner.SpawnBlockedWellFull ? "WELL FULL - spawning stopped" : "spawning";

            _label.text =
                $"Fall interval: {interval}s\n" +
                $"Phase: {phase}\n" +
                $"Status: {status}";
        }

        private TMP_Text CreateLabel()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("BlockoutDebugCanvas (TEMP)");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            var panelObject = new GameObject("BlockoutDebugOverlayPanel (TEMP)");
            panelObject.transform.SetParent(canvas.transform, false);

            var panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(16f, -16f);
            panelRect.sizeDelta = new Vector2(300f, 110f);

            // Plain dark backing so the text stays legible over an arbitrary 3D background.
            var background = panelObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(panelObject.transform, false);

            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;

            return text;
        }
    }
}
