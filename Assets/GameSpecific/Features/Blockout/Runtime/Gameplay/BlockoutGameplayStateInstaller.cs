using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Architecture.States;
using hp55games.Blockout.Achievements;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Gameplay
{
    // Registers BlockoutGameplayStateFactory so SceneFlowService.GoToGameplayAsync (and the
    // pause/resume path) construct BlockoutGameplayState instead of the template's generic
    // GameplayState for this game's FSM "gameplay" slot. Also registers IBlockoutSkinService
    // (Technical Doc Phase 3) and IBlockoutAchievementService (Shop Technical Doc Phase 2) - same
    // placement rationale for both: they resolve their own dependencies
    // (IConfigCatalogService/ISaveService/ITimeService) lazily, so it doesn't matter whether this
    // installer's Awake() runs before or after ConfigCatalogInstaller's in this scene. Placed in
    // 01_Menu.unity - the single point (per the ConfigCatalogInstaller consolidation) guaranteed
    // to run before Play is ever pressed.
    public sealed class BlockoutGameplayStateInstaller : MonoBehaviour
    {
        private void Awake()
        {
            ServiceRegistry.Register<IGameplayStateFactory>(new BlockoutGameplayStateFactory());
            ServiceRegistry.Register<IBlockoutSkinService>(new BlockoutSkinService());
            ServiceRegistry.Register<IBlockoutAchievementService>(new BlockoutAchievementService());
        }
    }
}
