using NUnit.Framework;
using UnityEngine;
using hp55games.Blockout.UI;

namespace hp55games.Tests.PlayMode.Blockout
{
    public sealed class HudMirrorMathTests
    {
        private static HudRectLayout Layout(Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos) =>
            new HudRectLayout { AnchorMin = min, AnchorMax = max, Pivot = pivot, AnchoredPosition = pos };

        [Test]
        public void Mirror_BottomRightCorner_BecomesBottomLeft()
        {
            var m = HudMirrorMath.Mirror(Layout(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 30f)));

            Assert.AreEqual(new Vector2(0f, 0f), m.AnchorMin);
            Assert.AreEqual(new Vector2(0f, 0f), m.AnchorMax);
            Assert.AreEqual(new Vector2(0f, 0f), m.Pivot);
            Assert.AreEqual(new Vector2(20f, 30f), m.AnchoredPosition);
        }

        [Test]
        public void Mirror_AsymmetricStretch_SwapsMinAndMaxX()
        {
            var m = HudMirrorMath.Mirror(Layout(new Vector2(0.25f, 0.1f), new Vector2(1f, 0.4f), new Vector2(0.3f, 0.5f), Vector2.zero));

            Assert.AreEqual(0f, m.AnchorMin.x, 1e-6f);
            Assert.AreEqual(0.1f, m.AnchorMin.y);
            Assert.AreEqual(0.75f, m.AnchorMax.x, 1e-6f);
            Assert.AreEqual(0.4f, m.AnchorMax.y);
            Assert.AreEqual(0.7f, m.Pivot.x, 1e-6f);
            Assert.AreEqual(0.5f, m.Pivot.y);
        }

        [Test]
        public void Mirror_CenteredLayout_IsUnchanged()
        {
            var l = Layout(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f));
            var m = HudMirrorMath.Mirror(l);

            Assert.AreEqual(l.AnchorMin, m.AnchorMin);
            Assert.AreEqual(l.AnchorMax, m.AnchorMax);
            Assert.AreEqual(l.Pivot, m.Pivot);
            Assert.AreEqual(12f, m.AnchoredPosition.y);
            Assert.AreEqual(0f, m.AnchoredPosition.x);
        }

        [Test]
        public void Mirror_Twice_RestoresOriginal()
        {
            var l = Layout(new Vector2(0.2f, 0.1f), new Vector2(0.9f, 0.3f), new Vector2(0.15f, 0.6f), new Vector2(-7f, 9f));
            var back = HudMirrorMath.Mirror(HudMirrorMath.Mirror(l));

            Assert.AreEqual(l.AnchorMin.x, back.AnchorMin.x, 1e-6f);
            Assert.AreEqual(l.AnchorMax.x, back.AnchorMax.x, 1e-6f);
            Assert.AreEqual(l.Pivot.x, back.Pivot.x, 1e-6f);
            Assert.AreEqual(l.AnchoredPosition, back.AnchoredPosition);
            Assert.AreEqual(l.AnchorMin.y, back.AnchorMin.y);
        }
    }
}
