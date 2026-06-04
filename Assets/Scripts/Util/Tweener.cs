using System;
using System.Collections;
using UnityEngine;

namespace BlockJam.Util
{
    public delegate float EaseFunc(float t);

    /// <summary>
    /// Minimal, allocation-light tween helper built on coroutines.
    /// Provides the few motions this game needs: move, scale, jump-arc, punch-scale.
    ///
    /// Design notes:
    ///  - Every tween is a coroutine you start on a MonoBehaviour you already own,
    ///    so there is no hidden manager/singleton and no third-party dependency.
    ///  - Each routine takes an optional onComplete so callers can chain logic
    ///    (e.g. "after the unit lands in the slot, check for a match").
    /// </summary>
    public static class Tweener
    {
        public static IEnumerator Move(Transform t, Vector3 to, float duration, EaseFunc ease = null,
                                       Action onComplete = null)
        {
            if (t == null) yield break;
            ease ??= Easing.OutQuad;
            Vector3 from = t.position;

            if (duration <= 0f)
            {
                t.position = to;
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                float k = ease(Mathf.Clamp01(elapsed / duration));
                t.position = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }

            t.position = to;
            onComplete?.Invoke();
        }

        /// <summary>Move along a parabolic arc - used for the "throw into slot" feel.</summary>
        public static IEnumerator JumpTo(Transform t, Vector3 to, float height, float duration,
                                         EaseFunc ease = null, Action onComplete = null)
        {
            if (t == null) yield break;
            ease ??= Easing.InOutQuad;
            Vector3 from = t.position;

            if (duration <= 0f)
            {
                t.position = to;
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float k = ease(raw);
                Vector3 flat = Vector3.LerpUnclamped(from, to, k);
                // Parabola peaking at k = 0.5
                float arc = 4f * height * raw * (1f - raw);
                flat.y += arc;
                t.position = flat;
                yield return null;
            }

            t.position = to;
            onComplete?.Invoke();
        }

        public static IEnumerator Scale(Transform t, Vector3 to, float duration, EaseFunc ease = null,
                                        Action onComplete = null)
        {
            if (t == null) yield break;
            ease ??= Easing.OutQuad;
            Vector3 from = t.localScale;

            if (duration <= 0f)
            {
                t.localScale = to;
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                float k = ease(Mathf.Clamp01(elapsed / duration));
                t.localScale = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }

            t.localScale = to;
            onComplete?.Invoke();
        }

        /// <summary>A quick squash-and-stretch punch on scale, returning to the original.</summary>
        public static IEnumerator PunchScale(Transform t, float punch, float duration,
                                             Action onComplete = null)
        {
            if (t == null) yield break;
            Vector3 baseScale = t.localScale;
            Vector3 peak = baseScale * (1f + punch);

            float half = duration * 0.5f;
            yield return Scale(t, peak, half, Easing.OutQuad);
            yield return Scale(t, baseScale, half, Easing.OutBack);
            onComplete?.Invoke();
        }
    }
}
