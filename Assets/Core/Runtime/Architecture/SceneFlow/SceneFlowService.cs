using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Architecture.States;
using hp55games.Mobile.Core.UI;

namespace hp55games.Mobile.Game.SceneFlow
{
    public sealed class SceneFlowService : ISceneFlowService
    {
        private const float FadeDuration    = 0.25f;
        private const int OverlayTimeoutMs  = 1000;

        private readonly IGameStateMachine  _fsm;
        private readonly IUIOverlayService  _overlay;
        private readonly ISceneFlowConfig   _config;
        private bool _isTransitioning;
        private AsyncOperation _gameplayPreload;

        private string MenuSceneName     => _config?.MenuScene     ?? "01_Menu";
        private string GameplaySceneName => _config?.GameplayScene  ?? "02_Gameplay";
        private string ResultsSceneName  => _config?.ResultsScene   ?? "03_Results";

        private string[] ContentScenes =>
            new[] { MenuSceneName, GameplaySceneName, ResultsSceneName };

        public SceneFlowService()
        {
            if (!ServiceRegistry.TryResolve<IGameStateMachine>(out _fsm))
                Debug.LogWarning("[SceneFlowService] IGameStateMachine not available. FSM integration will be disabled.");

            if (!ServiceRegistry.TryResolve<IUIOverlayService>(out _overlay))
                Debug.LogWarning("[SceneFlowService] IUIOverlayService not available. Overlay transitions will be skipped.");

            if (!ServiceRegistry.TryResolve<ISceneFlowConfig>(out _config))
                Debug.LogWarning("[SceneFlowService] ISceneFlowConfig not available. Falling back to hardcoded scene names.");
        }

        public void StartGameplayPreload()
        {
            if (_gameplayPreload != null) return;
            _gameplayPreload = SceneManager.LoadSceneAsync(GameplaySceneName, LoadSceneMode.Additive);
            _gameplayPreload.allowSceneActivation = false;
        }

        private async Task SwitchContentSceneAsync(string targetScene)
        {
            Debug.Log($"[SceneFlowService] Switching content scene to: {targetScene}");

            // Unload other content scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);

                if (!s.isLoaded)
                    continue;

                bool isContent = Array.IndexOf(ContentScenes, s.name) >= 0;
                if (!isContent)
                    continue;

                if (s.name == targetScene)
                    continue;

                Debug.Log($"[SceneFlowService] Unloading previous content scene: {s.name}");

                var op = SceneManager.UnloadSceneAsync(s);
                if (op != null)
                {
                    while (!op.isDone)
                        await Task.Yield();
                }
            }

            // Load target if not loaded
            var target = SceneManager.GetSceneByName(targetScene);
            if (!target.isLoaded)
            {
                Debug.Log($"[SceneFlowService] Loading content scene additively: {targetScene}");
                var op = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
                if (op != null)
                {
                    while (!op.isDone)
                        await Task.Yield();
                }

                target = SceneManager.GetSceneByName(targetScene);
            }

            if (target.IsValid())
            {
                SceneManager.SetActiveScene(target);
                Debug.Log($"[SceneFlowService] Active content scene set to: {targetScene}");
            }
            else
            {
                Debug.LogError($"[SceneFlowService] Loaded target scene {targetScene} is NOT valid!");
            }
        }

        private async Task UnloadSceneIfLoadedAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            Debug.Log($"[SceneFlowService] Unloading scene: {sceneName}");
            var op = SceneManager.UnloadSceneAsync(scene);
            if (op == null)
                return;

            while (!op.isDone)
                await Task.Yield();
        }

        private async Task LoadSceneIfNotLoadedAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;

            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
                return;

            Debug.Log($"[SceneFlowService] Loading scene additively: {sceneName}");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
                return;

            while (!op.isDone)
                await Task.Yield();
        }

        public async Task GoToMenuAsync()
        {
            Debug.Log("[SceneFlowService] GoToMenuAsync()");
            await RunWithOverlay(async () =>
            {
                // If a gameplay preload is in progress (allowSceneActivation = false),
                // activate it so Unity can finalise the load, then immediately unload it.
                // Leaving it blocked at 90% with allowSceneActivation=false leaks memory.
                if (_gameplayPreload != null)
                {
                    _gameplayPreload.allowSceneActivation = true;
                    while (!_gameplayPreload.isDone) await Task.Yield();
                    _gameplayPreload = null;
                }

                await UnloadSceneIfLoadedAsync(GameplaySceneName);
                await UnloadSceneIfLoadedAsync(ResultsSceneName);
                await LoadSceneIfNotLoadedAsync(MenuSceneName);

                var menuScene = SceneManager.GetSceneByName(MenuSceneName);
                if (menuScene.IsValid()) SceneManager.SetActiveScene(menuScene);

                if (_fsm != null)
                {
                    try { await _fsm.ChangeStateAsync(new MainMenuState()); }
                    catch (Exception ex) { Debug.LogError($"[SceneFlowService] FSM transition error in GoToMenuAsync: {ex}"); }
                }
            });
        }

        public async Task GoToGameplayAsync(string levelId = null)
        {
            Debug.Log($"[SceneFlowService] GoToGameplayAsync(levelId: {levelId})");
            await RunWithOverlay(async () =>
            {
                await UnloadSceneIfLoadedAsync(ResultsSceneName);

                if (_gameplayPreload != null)
                {
                    _gameplayPreload.allowSceneActivation = true;
                    while (!_gameplayPreload.isDone) await Task.Yield();
                    _gameplayPreload = null;
                }
                else
                {
                    await LoadSceneIfNotLoadedAsync(GameplaySceneName);
                }

                var gameplayScene = SceneManager.GetSceneByName(GameplaySceneName);
                if (gameplayScene.IsValid()) SceneManager.SetActiveScene(gameplayScene);

                if (_fsm != null)
                {
                    try { await _fsm.ChangeStateAsync(new GameplayState()); }
                    catch (Exception ex) { Debug.LogError($"[SceneFlowService] FSM transition error in GoToGameplayAsync: {ex}"); }
                }
            });
        }

        public async Task GoToResultsAsync()
        {
            Debug.Log("[SceneFlowService] GoToResultsAsync()");
            await RunWithOverlay(async () =>
            {
                await UnloadSceneIfLoadedAsync(GameplaySceneName);
                await LoadSceneIfNotLoadedAsync(ResultsSceneName);

                var resultsScene = SceneManager.GetSceneByName(ResultsSceneName);
                if (resultsScene.IsValid()) SceneManager.SetActiveScene(resultsScene);

                if (_fsm != null)
                {
                    try { await _fsm.ChangeStateAsync(new ResultState()); }
                    catch (Exception ex) { Debug.LogError($"[SceneFlowService] FSM transition error in GoToResultsAsync: {ex}"); }
                }
            });
        }

        public async Task GoToPauseAsync()
        {
            if (_fsm == null) return;

            if (_isTransitioning)
            {
                Debug.LogWarning("[SceneFlowService] GoToPauseAsync called but a transition is already in progress.");
                return;
            }

            _isTransitioning = true;
            try
            {
                var currentState = _fsm.Current;
                try { await _fsm.ChangeStateAsync(new PauseState(currentState)); }
                catch (Exception ex) { Debug.LogError($"[SceneFlowService] FSM transition error in GoToPauseAsync: {ex}"); }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        public async Task ResumeFromPauseAsync()
        {
            if (_fsm == null) return;

            if (_isTransitioning)
            {
                Debug.LogWarning("[SceneFlowService] ResumeFromPauseAsync called but a transition is already in progress.");
                return;
            }

            if (!(_fsm.Current is PauseState pauseState))
            {
                Debug.LogWarning("[SceneFlowService] ResumeFromPauseAsync called but current state is not PauseState.");
                return;
            }

            _isTransitioning = true;
            try
            {
                var previousState = pauseState.GetPreviousState();
                
                if (previousState is GameplayState)
                {
                    try { await _fsm.ChangeStateAsync(new GameplayState(isResuming: true)); }
                    catch (Exception ex) { Debug.LogError($"[SceneFlowService] FSM transition error in ResumeFromPauseAsync: {ex}"); }
                }
                else if (previousState != null)
                {
                    try { await _fsm.ChangeStateAsync(previousState); }
                    catch (Exception ex) { Debug.LogError($"[SceneFlowService] FSM transition error in ResumeFromPauseAsync: {ex}"); }
                }
                else
                {
                    Debug.LogError("[SceneFlowService] ResumeFromPauseAsync: previous state is null. Cannot resume.");
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async Task RunWithOverlay(Func<Task> action)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[SceneFlowService] Transition already in progress, request ignored.");
                return;
            }

            _isTransitioning = true;
            try
            {
                if (_overlay != null) await SafeFadeInAsync();
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SceneFlowService] Error during scene flow action: {ex}");
                }
                finally
                {
                    if (_overlay != null) await SafeFadeOutAsync();
                }
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async Task SafeFadeInAsync()
        {
            try
            {
                var fadeTask = _overlay.FadeInAsync(FadeDuration);
                var completed = await Task.WhenAny(fadeTask, Task.Delay(OverlayTimeoutMs));
                if (completed != fadeTask) Debug.LogWarning("[SceneFlowService] Overlay FadeIn timed out.");
                else await fadeTask;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneFlowService] Overlay FadeIn failed: {ex.Message}");
            }
        }

        private async Task SafeFadeOutAsync()
        {
            try
            {
                var fadeTask = _overlay.FadeOutAsync(FadeDuration);
                var completed = await Task.WhenAny(fadeTask, Task.Delay(OverlayTimeoutMs));
                if (completed != fadeTask) Debug.LogWarning("[SceneFlowService] Overlay FadeOut timed out.");
                else await fadeTask;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneFlowService] Overlay FadeOut failed: {ex.Message}");
            }
        }
    }
}