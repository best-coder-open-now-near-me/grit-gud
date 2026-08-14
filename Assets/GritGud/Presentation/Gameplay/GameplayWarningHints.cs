using System;
using System.Collections.Generic;
using GritGud.Application.Gameplay;

namespace GritGud.Presentation.Gameplay
{
    public interface IGameplayWarningHintSource
    {
        GameplayWarningHintModel CurrentWarningHint { get; }
    }

    public static class GameplayWarningHintSelector
    {
        public static GameplayWarningHintModel Select(
            IEnumerable<IGameplayWarningHintSource> sources)
        {
            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            GameplayWarningHintModel selected = null;
            foreach (IGameplayWarningHintSource source in sources)
            {
                GameplayWarningHintModel candidate = source?.CurrentWarningHint;
                if (candidate != null
                    && (selected == null
                        || candidate.Priority > selected.Priority))
                {
                    selected = candidate;
                }
            }

            return selected;
        }
    }
}
