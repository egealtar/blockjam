using UnityEngine;
using BlockJam.Pooling;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Owns pools of one-shot VFX prefabs and exposes simple "play at position" calls.
    /// The board/slot systems call PlayMatch / PlayWin / PlayBoxBreak / PlayDust without caring
    /// about pooling.
    /// </summary>
    public class VfxPool : MonoBehaviour
    {
        [SerializeField] private PooledVfx matchEffectPrefab;
        [SerializeField] private PooledVfx winEffectPrefab;
        [SerializeField] private PooledVfx boxBreakEffectPrefab;
        [SerializeField] private PooledVfx dustEffectPrefab;

        private ObjectPool<PooledVfx> _matchPool;
        private ObjectPool<PooledVfx> _winPool;
        private ObjectPool<PooledVfx> _boxBreakPool;
        private ObjectPool<PooledVfx> _dustPool;

        public void Setup(int prewarm)
        {
            if (matchEffectPrefab != null)
                _matchPool = new ObjectPool<PooledVfx>(matchEffectPrefab, transform, prewarm);
            if (winEffectPrefab != null)
                _winPool = new ObjectPool<PooledVfx>(winEffectPrefab, transform, 1);
            if (boxBreakEffectPrefab != null)
                _boxBreakPool = new ObjectPool<PooledVfx>(boxBreakEffectPrefab, transform, prewarm);
            if (dustEffectPrefab != null)
                _dustPool = new ObjectPool<PooledVfx>(dustEffectPrefab, transform, Mathf.Max(prewarm, 8));
        }

        public void PlayMatch(Vector3 worldPos)
        {
            if (_matchPool == null) return;
            var fx = _matchPool.Get();
            fx.transform.position = worldPos;
            fx.Bind(f => _matchPool.Release(f));
        }

        public void PlayWin(Vector3 worldPos)
        {
            if (_winPool == null) return;
            var fx = _winPool.Get();
            fx.transform.position = worldPos;
            fx.Bind(f => _winPool.Release(f));
        }

        public void PlayBoxBreak(Vector3 worldPos)
        {
            if (_boxBreakPool == null) return;
            var fx = _boxBreakPool.Get();
            fx.transform.position = worldPos;
            fx.Bind(f => _boxBreakPool.Release(f));
        }

        /// <summary>Small kick-up under a unit's feet while it runs, tinted to the unit's color.</summary>
        public void PlayDust(Vector3 worldPos, Color tint)
        {
            if (_dustPool == null) return;
            var fx = _dustPool.Get();
            fx.transform.position = worldPos;
            fx.SetColor(tint);
            fx.Bind(f => _dustPool.Release(f));
        }
    }
}