using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace hp55games.Mobile.Core.Architecture
{
    public interface IContentLoader
    {
        Task<T> LoadAsync<T>(string address) where T : class;
        void Release<T>(T asset);
        Task<GameObject> InstantiateAsync(string address, Transform parent = null, bool worldPositionStays = false);
        void ReleaseInstance(GameObject instance);
    }

    public sealed class AddressablesContentLoader : IContentLoader
    {
        // How long InstantiateAsync can sit pending before we warn - not a timeout, just a
        // diagnostic threshold (see InstantiateAsync's remarks).
        private const int StallWarningMs = 3000;

        public async Task<T> LoadAsync<T>(string address) where T : class
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
            T result = await handle.Task;

            if (result == null)
                Debug.LogError($"[ContentLoader] Asset null. Address='{address}' Tipo={typeof(T).Name}");

            return result;
        }
        
        public async Task<GameObject> InstantiateAsync(string address, Transform parent = null, bool worldPositionStays = false)
        {
            // parent null = instanzia in root; per UI passa il Canvas.transform
            var handle = Addressables.InstantiateAsync(address, parent);

            // Permanent diagnostic (not test-only): a blocked InstantiateAsync - e.g. queued
            // behind a scene load stuck at 90% via allowSceneActivation = false, see
            // SceneFlowService.StartGameplayPreloadAsync's remarks - previously hung completely
            // silently, indistinguishable from one that's merely slow, and took ~8 hours to
            // diagnose on device (never reproduced in Editor, so an Editor-only check would have
            // missed it). Doesn't change behavior or cancel anything - just logs once if this
            // specific call is still pending after the threshold, then keeps waiting normally.
            if (await Task.WhenAny(handle.Task, Task.Delay(StallWarningMs)) != handle.Task)
            {
                Debug.LogWarning($"[AddressablesContentLoader] InstantiateAsync('{address}') still pending after {StallWarningMs / 1000}s - check for a blocked shared loading pipeline (e.g. a scene preload with allowSceneActivation = false).");
            }

            var instance = await handle.Task;

            if (instance == null)
                Debug.LogError($"[ContentLoader] InstantiateAsync null. Address='{address}'");

            // Se vuoi forzare gli ancoraggi UI:
            if (!worldPositionStays && instance != null)
            {
                var rt = instance.transform as RectTransform;
                if (rt != null && parent != null) rt.SetParent(parent, false);
            }

            return instance;
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance != null)
                Addressables.ReleaseInstance(instance);
        }

        public void Release<T>(T asset)
        {
            if (asset != null)
                Addressables.Release(asset);
        }
    }
}