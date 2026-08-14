using System.Collections.Generic;
using GritGud.Domain.Gameplay;

namespace GritGud.Application.Gameplay
{
    public interface ITargetExposureQuery
    {
        TargetExposureSnapshot Capture(
            string observerId,
            GameplayPosition observerOrigin,
            string targetId,
            IReadOnlyList<TargetRegionSample> targetRegions);
    }
}
