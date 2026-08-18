using System;

namespace GritGud.Domain.Levels
{
    internal static class LevelValidationMath
    {
        public static bool Contains(LevelBoundsData bounds, Float3Data point)
        {
            float halfX = bounds.size.x * 0.5f;
            float halfY = bounds.size.y * 0.5f;
            float halfZ = bounds.size.z * 0.5f;
            return point.x >= bounds.center.x - halfX
                && point.x <= bounds.center.x + halfX
                && point.y >= bounds.center.y - halfY
                && point.y <= bounds.center.y + halfY
                && point.z >= bounds.center.z - halfZ
                && point.z <= bounds.center.z + halfZ;
        }

        public static bool IsFinite(Float3Data value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(FloatColorData value)
        {
            return IsFinite(value.r)
                && IsFinite(value.g)
                && IsFinite(value.b)
                && IsFinite(value.a);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
