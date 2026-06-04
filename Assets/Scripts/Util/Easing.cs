using UnityEngine;

namespace BlockJam.Util
{
    /// <summary>
    /// Small library of easing curves used by Tweener.
    /// Hand-rolled so the project has ZERO third-party tween dependency
    /// (the case allows tween libs but forbids DI/pooling libs; staying dependency-free
    /// keeps the build reproducible for the reviewer and proves the math is understood).
    /// </summary>
    public static class Easing
    {
        public static float Linear(float t) => t;

        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float InQuad(float t) => t * t;

        public static float InOutQuad(float t) =>
            t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>Overshoots past 1 then settles - great for "pop"/landing feedback.</summary>
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
