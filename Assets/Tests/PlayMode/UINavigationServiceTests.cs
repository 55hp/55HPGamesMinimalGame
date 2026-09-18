using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.UI;
using hp55games.Ui;

namespace hp55games.Mobile.Core.UI.Tests
{
    // PushAsync/ReplaceAsync/PopAsync used to be able to run concurrently - two overlapping calls
    // could interleave their _stack mutations and fades. UINavigationService now serializes its
    // three entry points behind an internal lock so each call's whole body finishes before the
    // next one starts, in call order. These tests fire overlapping calls without awaiting between
    // them (the same pattern a rapid double button tap produces) and assert the stack still ends
    // up correct.
    public class UINavigationServiceTests
    {
        private GameObject _uiRootGo;
        private FakeContentLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _uiRootGo = new GameObject("UIRoot", typeof(RectTransform), typeof(UIRoot));
            var pages = new GameObject("Pages", typeof(RectTransform));
            pages.transform.SetParent(_uiRootGo.transform, false);
            _uiRootGo.GetComponent<UIRoot>().pages = (RectTransform)pages.transform;

            _loader = new FakeContentLoader();
            ServiceRegistry.Register<IContentLoader>(_loader);
        }

        [TearDown]
        public void TearDown()
        {
            _loader.DestroyAllLiveInstances();
            UnityEngine.Object.DestroyImmediate(_uiRootGo);
        }

        [UnityTest]
        public IEnumerator PushAsync_TwoOverlappingPushes_EndWithCorrectStackOrder_NoDuplicatesOrOrphans()
        {
            _loader.SetDelayMs("page-a", 300); // slower load than page-b, on purpose
            _loader.SetDelayMs("page-b", 0);

            var service = new UINavigationService();

            // Fired back-to-back with no await between them, like two rapid taps.
            var pushA = service.PushAsync("page-a");
            var pushB = service.PushAsync("page-b");

            yield return AwaitTask(pushA);
            yield return AwaitTask(pushB);

            Assert.AreEqual(2, _loader.LiveInstanceCount, "exactly one instance per address - no duplicated or orphaned page GameObjects");
            Assert.IsTrue(service.CanGoBack);

            var pageA = _loader.GetInstance("page-a");
            var pageB = _loader.GetInstance("page-b");
            Assert.IsNotNull(pageA);
            Assert.IsNotNull(pageB);

            // page-a pushed first (bottom, hidden), page-b pushed second (top, visible) - despite
            // page-a's slower load, serialization means page-b's push only starts once page-a's
            // has fully finished, so call order still wins.
            Assert.IsFalse(pageA.activeSelf, "page-a should end up hidden beneath page-b");
            Assert.IsTrue(pageB.activeSelf, "page-b should be the visible top page");
            Assert.AreEqual(1f, pageB.GetComponent<CanvasGroup>().alpha, 0.01f);
        }

        [UnityTest]
        public IEnumerator PushAsync_ThreeOverlappingPushes_PreserveCallOrder_EvenWithReversedLoadDelays()
        {
            // Load speed is the inverse of call order, so a race would very likely reorder these.
            _loader.SetDelayMs("page-a", 300);
            _loader.SetDelayMs("page-b", 150);
            _loader.SetDelayMs("page-c", 0);

            var service = new UINavigationService();

            var pushA = service.PushAsync("page-a");
            var pushB = service.PushAsync("page-b");
            var pushC = service.PushAsync("page-c");

            yield return AwaitTask(pushA);
            yield return AwaitTask(pushB);
            yield return AwaitTask(pushC);

            Assert.AreEqual(3, _loader.LiveInstanceCount, "no duplicated or orphaned page GameObjects");

            var pageA = _loader.GetInstance("page-a");
            var pageB = _loader.GetInstance("page-b");
            var pageC = _loader.GetInstance("page-c");

            Assert.IsFalse(pageA.activeSelf);
            Assert.IsFalse(pageB.activeSelf);
            Assert.IsTrue(pageC.activeSelf, "page-c (pushed last) should be the visible top page");
        }

        private static IEnumerator AwaitTask(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception?.InnerException ?? task.Exception;
        }

        // Minimal fake IContentLoader: instantiates plain GameObjects (no Addressables handle),
        // with a per-address artificial delay so tests can force overlapping in-flight loads.
        private sealed class FakeContentLoader : IContentLoader
        {
            private readonly Dictionary<string, int> _delaysMs = new();
            private readonly Dictionary<string, GameObject> _instancesByAddress = new();
            private readonly List<GameObject> _liveInstances = new();

            public int LiveInstanceCount => _liveInstances.Count;

            public void SetDelayMs(string address, int ms) => _delaysMs[address] = ms;

            public GameObject GetInstance(string address) =>
                _instancesByAddress.TryGetValue(address, out var go) ? go : null;

            public async Task<GameObject> InstantiateAsync(string address, Transform parent = null, bool worldPositionStays = false)
            {
                if (_delaysMs.TryGetValue(address, out var ms) && ms > 0)
                    await Task.Delay(ms);

                var go = new GameObject(address, typeof(RectTransform));
                go.SetActive(false);
                if (parent != null) ((RectTransform)go.transform).SetParent(parent, worldPositionStays);

                _instancesByAddress[address] = go;
                _liveInstances.Add(go);
                return go;
            }

            public void ReleaseInstance(GameObject instance)
            {
                if (instance == null) return;
                _liveInstances.Remove(instance);
                UnityEngine.Object.Destroy(instance);
            }

            public void DestroyAllLiveInstances()
            {
                foreach (var go in _liveInstances)
                {
                    if (go != null) UnityEngine.Object.DestroyImmediate(go);
                }
                _liveInstances.Clear();
                _instancesByAddress.Clear();
            }

            public Task<T> LoadAsync<T>(string address) where T : class => throw new NotSupportedException();
            public void Release<T>(T asset) { }
        }
    }
}
