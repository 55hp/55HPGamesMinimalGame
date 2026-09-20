using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("hp55games.Tests.PlayMode")]

namespace hp55games.Mobile.UI
{
    internal enum BackAction
    {
        ResumeFromPause,
        CloseTopPopup,
        PopPage,
        BackAtRoot
    }

    // The back-button priority, kept free of Input/services so it can be unit tested.
    internal static class BackPressRouter
    {
        public static BackAction Decide(bool isPaused, bool hasOpenPopups, bool canGoBack)
        {
            // Pause first: the pause UI is itself a popup owned by PauseState, so closing it
            // directly would leave the FSM in PauseState with timeScale 0 and no UI.
            if (isPaused) return BackAction.ResumeFromPause;
            if (hasOpenPopups) return BackAction.CloseTopPopup;
            if (canGoBack) return BackAction.PopPage;
            return BackAction.BackAtRoot;
        }
    }
}
