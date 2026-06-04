using UnityEngine;
using BlockJam.Pooling;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Owns pools of one-shot VFX prefabs and exposes simple "play at position" calls.
    /// The board/slot systems call PlayMatch / PlayWin without caring about pooling details.
    /// </summary>
    public class VfxPool : MonoBehaviour
    {
        [SerializeField] private PooledVfx matchEffectPrefab;
        [SerializeField] private PooledVfx winEffectPrefab;

        private ObjectPool<PooledVfx> _matchPool;
        private ObjectPool<PooledVfx> _winPool;

        public void Setup(int prewarm)
        {
            if (matchEffectPrefab != null)
                _matchPool = new ObjectPool<PooledVfx>(matchEffectPrefab, transform, prewarm);
            if (winEffectPrefab != null)
                _winPool = new ObjectPool<PooledVfx>(winEffectPrefab, transform, 1);
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
    }
}