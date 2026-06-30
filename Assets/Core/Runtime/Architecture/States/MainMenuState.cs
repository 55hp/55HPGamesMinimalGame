using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.Core.UI;

namespace hp55games.Mobile.Core.Architecture.States
{
    /// <summary>
    /// Entry state for the template: shows the generic main menu page
    /// and starts the menu BGM.
    /// </summary>
    public sealed class MainMenuState : IGameState
    {
        private readonly IUINavigationService _nav;
        private readonly IMusicService _music;
        private readonly ISceneFlowService _sceneFlow;

        public MainMenuState()
        {
            if (!ServiceRegistry.TryResolve<IUINavigationService>(out _nav))
            {
                Debug.LogError("[MainMenuState] IUINavigationService not registered. " +
                               "Check that 91_UI_Root + UIServiceInstaller are loaded before starting the FSM.");
            }

            ServiceRegistry.TryResolve<IMusicService>(out _music);
            ServiceRegistry.TryResolve<ISceneFlowService>(out _sceneFlow);
        }

        public async Task EnterAsync(CancellationToken ct)
        {
            Debug.Log("[MainMenuState] Enter");

            // 1) Mostra la pagina di main menu (Addressable prefab).
            // Il preload di 02_Gameplay parte DOPO: su mobile un AsyncOperation bloccata
            // al 90% (allowSceneActivation=false) compete col caricamento Addressables
            // della UI e causa schermata nera.
            if (_nav != null)
            {
                await _nav.ReplaceAsync(global::hp55games.Addr.Content.UI.Pages.Main_Menu_Page);
            }

            // 2) Musica di menu, se il servizio è disponibile.
            if (_music != null)
            {
                await _music.CrossfadeToAsync(
                    global::hp55games.Addr.Content.Audio.Bgm.MenuTheme,
                    0.5f
                );
            }

            // 3) Solo ora avvia il preload silenzioso di 02_Gameplay.
            // La UI è già visibile, quindi il caricamento in background non blocca nulla.
            _sceneFlow?.StartGameplayPreload();
        }

        public Task ExitAsync(CancellationToken ct)
        {
            // Se vuoi, qui potresti fare pop della page o fermare la musica
            return Task.CompletedTask;
        }
    }
}