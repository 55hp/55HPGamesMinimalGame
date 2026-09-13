using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Architecture.States;

namespace hp55games.Blockout.Gameplay
{
    // Registers BlockoutGameplayStateFactory so SceneFlowService.GoToGameplayAsync (and the
    // pause/resume path) construct BlockoutGameplayState instead of the template's generic
    // GameplayState for this game's FSM "gameplay" slot. Placed in 01_Menu.unity - the single
    // point (per the ConfigCatalogInstaller consolidation) guaranteed to run before Play is
    // ever pressed.
    public sealed class BlockoutGameplayStateInstaller : MonoBehaviour
    {
        private void Awake()
        {
            ServiceRegistry.Register<IGameplayStateFactory>(new BlockoutGameplayStateFactory());
        }
    }
}
