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
    // EnsureFadeGoAsync used to be able to race: PrewarmAsync (fired eagerly from
    // UIServiceInstaller) and FadeInAsync (the first real fade of a session) could both observe
    // _fadeGo == null while the other's Addressables instantiate was still in flight, each
    // starting its own and orphaning a duplicate overlay instance. UIOverlayService now caches
    // the in-flight instantiate task (_fadeGoTask) so concurrent callers await the same one.
    public class UIOverlayServiceTests
    {
        private GameObject _uiRootGo;
        private FakeContentLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _uiRootGo = new GameObject("UIRoot", typeof(RectTransform), typeof(UIRoot));
            var overlays = new GameObject("Overlays", typeof(RectTransform));
            overlays.transform.SetParent(_uiRootGo.transform, false);
            _uiRootGo.GetComponent<UIRoot>().overlays = (RectTransform)overlays.transform;

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
        public IEnumerator PrewarmAsync_And_FadeInAsync_StartedTogether_ShareTheSameInstantiate()
        {
            // Slow enough that both calls are guaranteed to still be in flight when the second
            // one starts - the same overlap a real PrewarmAsync (fire-and-forget from
            // UIServiceInstaller) vs. a real FadeInAsync (Play tap) can produce.
            _loader.SetDelayMs(hp55games.Addr.Content.UI.Overlays.FadeFull, 300);

            var service = new UIOverlayService();

            var prewarm = service.PrewarmAsync();
            var fadeIn = service.FadeInAsync(0.05f);

            yield return AwaitTask(prewarm);
            yield return AwaitTask(fadeIn);

            Assert.AreEqual(1, _loader.LiveInstanceCount,
                "PrewarmAsync and FadeInAsync racing each other must share one instantiate - no orphaned overlay instance");
        }

        private static IEnumerator AwaitTask(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception?.InnerException ?? task.Exception;
        }

        private sealed class FakeContentLoader : IContentLoader
        {
            private readonly Dictionary<string, int> _delaysMs = new();
            private readonly List<GameObject> _liveInstances = new();

            public int LiveInstanceCount => _liveInstances.Count;

            public void SetDelayMs(string address, int ms) => _delaysMs[address] = ms;

            public async Task<GameObject> InstantiateAsync(string address, Transform parent = null, bool worldPositionStays = false)
            {
                if (_delaysMs.TryGetValue(address, out var ms) && ms > 0)
                    await Task.Delay(ms);

                var go = new GameObject(address, typeof(RectTransform));
                if (parent != null) ((RectTransform)go.transform).SetParent(parent, worldPositionStays);

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
            }

            public Task<T> LoadAsync<T>(string address) where T : class => throw new NotSupportedException();
            public void Release<T>(T asset) { }
        }
    }
}
