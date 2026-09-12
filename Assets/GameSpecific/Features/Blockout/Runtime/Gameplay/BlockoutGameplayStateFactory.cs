using hp55games.Mobile.Core.Architecture.States;

namespace hp55games.Blockout.Gameplay
{
    public sealed class BlockoutGameplayStateFactory : IGameplayStateFactory
    {
        public IGameplayState Create(bool isResuming) => new BlockoutGameplayState(isResuming);
    }
}
