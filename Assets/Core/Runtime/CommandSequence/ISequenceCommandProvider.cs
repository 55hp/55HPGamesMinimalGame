namespace hp55games.Mobile.Core.CommandSequence
{
    public interface ISequenceCommandProvider
    {
        ISequenceCommand CreateCommand();
        string GetDisplayName();
    }
}
