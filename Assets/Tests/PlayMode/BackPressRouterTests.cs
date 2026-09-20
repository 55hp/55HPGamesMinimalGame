using NUnit.Framework;
using hp55games.Mobile.UI;

namespace hp55games.Tests.PlayMode
{
    public sealed class BackPressRouterTests
    {
        [Test]
        public void InPause_ResumesInsteadOfClosingThePausePopup()
        {
            // Pause popup is open too (PauseState opens it) - must still resume, not CloseTop.
            Assert.AreEqual(BackAction.ResumeFromPause, BackPressRouter.Decide(isPaused: true, hasOpenPopups: true, canGoBack: true));
        }

        [Test]
        public void InPause_WithoutPopupOrHistory_StillResumes()
        {
            Assert.AreEqual(BackAction.ResumeFromPause, BackPressRouter.Decide(true, false, false));
        }

        [Test]
        public void OpenPopup_ClosesTop_BeforePoppingPage()
        {
            Assert.AreEqual(BackAction.CloseTopPopup, BackPressRouter.Decide(false, true, true));
        }

        [Test]
        public void NoPopup_CanGoBack_PopsPage()
        {
            Assert.AreEqual(BackAction.PopPage, BackPressRouter.Decide(false, false, true));
        }

        [Test]
        public void AtRoot_RaisesBackAtRoot()
        {
            Assert.AreEqual(BackAction.BackAtRoot, BackPressRouter.Decide(false, false, false));
        }
    }
}
