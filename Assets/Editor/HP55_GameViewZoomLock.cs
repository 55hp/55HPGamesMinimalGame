// Assets/Editor/HP55_GameViewZoomLock.cs
// Forces the Game View zoom to 1x each time Play Mode is entered.
// Uses reflection on private internal UnityEditor fields (not a stable public API).
// Wrapped in try/catch: if a future Unity version renames these members,
// the script silently stops acting instead of breaking the Editor.

using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace hp55games.Editor.Tools
{

[InitializeOnLoad]
public static class HP55_GameViewZoomLock
{
    private const float LockedZoom = 1f;

    static HP55_GameViewZoomLock()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;

        try
        {
            var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);

            var zoomAreaField = gameViewType.GetField("m_ZoomArea", BindingFlags.NonPublic | BindingFlags.Instance);
            object zoomArea = zoomAreaField?.GetValue(gameView);
            if (zoomArea == null) return;

            var scaleField = zoomArea.GetType().GetField("m_Scale", BindingFlags.NonPublic | BindingFlags.Instance);
            scaleField?.SetValue(zoomArea, new Vector2(LockedZoom, LockedZoom));

            gameView.Repaint();
        }
        catch
        {
            // Internal API unavailable in this Unity version: no crash, no zoom lock applied.
        }
    }
}

}
