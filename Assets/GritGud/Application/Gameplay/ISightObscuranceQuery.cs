using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public interface ISightObscuranceQuery
    {
        long Revision { get; }

        bool BlocksSight(
            GameplayPosition origin,
            GameplayPosition destination);
    }
}
