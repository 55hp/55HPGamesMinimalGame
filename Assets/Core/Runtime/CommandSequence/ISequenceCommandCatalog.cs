using System.Collections.Generic;

namespace hp55games.Mobile.Core.CommandSequence
{
    public interface ISequenceCommandCatalog
    {
        IEnumerable<ISequenceCommand> GetCommands(SequenceContext context);
        int GetCommandCount();
    }
}
