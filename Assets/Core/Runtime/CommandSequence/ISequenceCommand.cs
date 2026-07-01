using hp55games.Mobile.Core.Architecture;

namespace hp55games.Mobile.Core.CommandSequence
{
    public interface ISequenceCommand
    {
        void Execute(SequenceContext context);
    }
}
