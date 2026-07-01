using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using hp55games.Mobile.Core.Architecture;

namespace hp55games.Mobile.Core.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Init()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(BootstrapSequence());
        }

        private IEnumerator BootstrapSequence()
        {
            // 1) Servizi core (compreso Save + Time)
            ServiceRegistry.InstallDefaults();

            // 2) Systems Audio
            yield return LoadSceneAdditiveCoroutine("Assets/Scenes/Additive/90_Systems_Audio.unity");

            // 3) UI Root
            yield return LoadSceneAdditiveCoroutine("Assets/Scenes/Additive/91_UI_Root.unity");

            // 4) Scena di menu
            yield return LoadSceneAdditiveCoroutine("Assets/Scenes/01_Menu.unity");

            var menuScene = SceneManager.GetSceneByPath("Assets/Scenes/01_Menu.unity");
            if (menuScene.IsValid())
                SceneManager.SetActiveScene(menuScene);
        }

        private static IEnumerator LoadSceneAdditiveCoroutine(string scenePath)
        {
            if (SceneManager.GetSceneByPath(scenePath).isLoaded)
                yield break;

            var op = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError("[GameBootstrap] Failed to load scene: " + scenePath);
                yield break;
            }

            while (!op.isDone)
                yield return null;
        }
    }
}