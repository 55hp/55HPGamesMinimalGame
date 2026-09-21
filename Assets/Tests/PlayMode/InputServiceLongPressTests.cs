using NUnit.Framework;
using UnityEngine;
using hp55games.Mobile.Core.InputSystem;

namespace hp55games.Tests.PlayMode
{
    // Drives InputService.Process with a fake pointer + clock (no real Input / Time involved).
    public sealed class InputServiceLongPressTests
    {
        private InputService _input;
        private int _longPress, _tap, _swipe, _pointerUp;
        private Vector2 _longPressPos;

        // Derived from the production constant so the tests follow any retune of it.
        private const float T = InputService.LongPressMinDuration;

        private static readonly Vector2 Start = new Vector2(100f, 100f);

        [SetUp]
        public void SetUp()
        {
            _input = new InputService();
            _longPress = _tap = _swipe = _pointerUp = 0;
            _input.LongPress += p => { _longPress++; _longPressPos = p; };
            _input.Tap += _ => _tap++;
            _input.Swipe += (_, __) => _swipe++;
            _input.PointerUp += _ => _pointerUp++;
        }

        private void Press(Vector2 pos, float t) => _input.Process(true, pos, -1, t);
        private void Release(float t) => _input.Process(false, default, -1, t);

        [Test]
        public void LongPress_DoesNotFire_BeforeThreshold()
        {
            Press(Start, 0f);
            Press(Start, T - 0.01f);

            Assert.AreEqual(0, _longPress);
        }

        [Test]
        public void LongPress_FiresOnce_AtThreshold_NotOnRelease()
        {
            Press(Start, 0f);
            Press(Start, T);
            Assert.AreEqual(1, _longPress);
            Assert.AreEqual(Start, _longPressPos);

            Press(Start, T * 2f);
            Assert.AreEqual(1, _longPress, "must not re-fire while still held");

            Release(T * 2f + 0.2f);
            Assert.AreEqual(1, _longPress);
        }

        [Test]
        public void LongPress_ConsumesGesture_NoTapOrSwipeOnRelease()
        {
            Press(Start, 0f);
            Press(Start, T + 0.1f);
            Release(T + 0.2f);

            Assert.AreEqual(1, _longPress);
            Assert.AreEqual(0, _tap);
            Assert.AreEqual(0, _swipe);
            Assert.AreEqual(1, _pointerUp, "PointerUp still fires");
        }

        [Test]
        public void LongPress_ThenDragAndRelease_StillNoSwipe()
        {
            Press(Start, 0f);
            Press(Start, T + 0.1f);
            Press(Start + new Vector2(200f, 0f), T + 0.2f);
            Release(T + 0.3f);

            Assert.AreEqual(0, _swipe);
            Assert.AreEqual(0, _tap);
        }

        [Test]
        public void LongPress_DoesNotFire_IfPointerMovedBeyondTapDistance()
        {
            Press(Start, 0f);
            Press(Start + new Vector2(50f, 0f), T * 0.5f);
            Press(Start + new Vector2(50f, 0f), T + 0.1f);

            Assert.AreEqual(0, _longPress);
        }

        [Test]
        public void ShortTap_StillFiresTap_AndNoLongPress()
        {
            Press(Start, 0f);
            Press(Start, 0.1f);
            Release(0.2f);

            Assert.AreEqual(1, _tap);
            Assert.AreEqual(0, _longPress);
            Assert.AreEqual(0, _swipe);
        }

        [Test]
        public void Swipe_StillFiresSwipe_AndNoLongPress()
        {
            Press(Start, 0f);
            Press(Start + new Vector2(100f, 0f), 0.1f);
            Release(0.2f);

            Assert.AreEqual(1, _swipe);
            Assert.AreEqual(0, _tap);
            Assert.AreEqual(0, _longPress);
        }

        [Test]
        public void NextGesture_AfterLongPress_WorksNormally()
        {
            Press(Start, 0f);
            Press(Start, T + 0.1f);
            Release(T + 0.2f);

            Press(Start, T + 1f);
            Release(T + 1.1f);

            Assert.AreEqual(1, _longPress);
            Assert.AreEqual(1, _tap);
        }
    }
}
