// Assets/Editor/HP55_DebugToolMenuItem.cs
// Adds a menu item to toggle the DebugToolOverlay during Play Mode.
// Only available while the application is playing.

using hp55games.Mobile.Core.UIScripts.Overlays;
using UnityEditor;
using UnityEngine;

namespace hp55games.Editor.Tools
{

public static class HP55_DebugToolMenuItem
{
    private const string MenuPath = "hp55games Tools/Debug_Tool";

    [MenuItem(MenuPath)]
    private static void ToggleDebugTool()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Debug Tool can only be activated during Play Mode.");
            return;
        }

        var debugTool = Object.FindAnyObjectByType<DebugToolOverlay>();

        if (debugTool == null)
            CreateDebugTool();
        else
            debugTool.ToggleVisibility();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggleDebugTool()
    {
        return Application.isPlaying;
    }

    private static void CreateDebugTool()
    {
        var debugToolGO = new GameObject("DebugToolOverlay");
        debugToolGO.AddComponent<DebugToolOverlay>();
        Object.DontDestroyOnLoad(debugToolGO);
    }
}

}
