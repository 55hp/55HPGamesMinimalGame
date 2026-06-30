using System.Collections.Generic;

namespace hp55games
{
    /// <summary>
    /// Chiavi Addressables centralizzate (niente stringhe sparse).
    /// Convenzione: "config/..." e "/<categoria>/<nome>"
    /// </summary>
    public static class Addr
    {
        public static class Config
        {
            public const string Main = "config/main";
        }

        public static class Content
        {
            public static class UI
            {
                public static class Popups
                {
                    public const string Popup_Generic = "content/ui/popup_generic";
                    public const string Popup_Pause = "content/ui/popups/pause";
                }
                
                public static class Pages
                {
                    public const string Results_Page = "content/ui/pages/results";
                    public const string Credits_Page = "content/ui/pages/credits";
                    public const string Options_Page = "content/ui/pages/options";
                    public const string Main_Menu_Page = "content/ui/pages/main_menu";
                }
                
                public static class Overlays
                {
                    public const string FadeFull    = "content/ui/overlays/overlay_fade_full";
                    public const string LoadingFull = "content/ui/overlays/overlay_loading_full";
                }

                public static class Toasts
                {
                    public const string Default = "content/ui/toasts/toast_generic";
                }

                public static class Screens
                {
                    public const string GameplayHUD = "content/ui/screens/gameplay_hud";
                }
            }

            public static class Audio
            {
                public static class Bgm
                {
                    public const string MenuTheme = "content/audio/bgm/menu_theme";
                    public const string GameTheme = "content/audio/bgm/game_theme";
                }
                
            }
        }

        public static class FlappyTsunami
        {
            public static class Prefabs
            {
                public const string Follower_prefab = "flappytsunamy/content/followers/main_prefab";
            }

            public static class FollowerConfigs
            {
                public const string Follower0 = "flappytsunami/content/followerConfigs/zero";
                public const string Follower1 = "flappytsunami/content/followerConfigs/one";
                public const string Follower2 = "flappytsunami/content/followerConfigs/two";

                public static List<string> GetAllFollowers()
                {
                    List<string> results = new List<string>();
                    results.Add(Follower0);
                    results.Add(Follower1);
                    results.Add(Follower2);
                    return results;
                }
            }
        }
    }
}
