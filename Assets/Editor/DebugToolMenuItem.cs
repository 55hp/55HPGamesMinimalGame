using hp55games.Mobile.Core.UIScripts.Overlays;
using UnityEditor;
using UnityEngine;

namespace hp55games.Mobile.Core.Editor.Tools
{
    public static class DebugToolMenuItem
    {
        private const string MENU_PATH = "hp55games Tools/Debug_Tool";
        
        [MenuItem(MENU_PATH)]
        private static void ToggleDebugTool()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Debug Tool can only be activated during Play Mode.");
                return;
            }

            var debugTool = Object.FindAnyObjectByType<DebugToolOverlay>();
            
            if (debugTool == null)
            {
                CreateDebugTool();
            }
            else
            {
                debugTool.ToggleVisibility();
            }
        }

        [MenuItem(MENU_PATH, true)]
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
