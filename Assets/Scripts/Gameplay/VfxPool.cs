using System.Collections;
using UnityEngine;
using BlockJam.Pooling;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// A pooled one-shot particle effect. Auto-returns to its pool after its lifetime,
    /// so VFX also obey the "no Instantiate/Destroy at runtime" rule.
    /// </summary>
    public class PooledVfx : MonoBehaviour, IPoolable
    {
        [SerializeField] private ParticleSystem particles;
        [Tooltip("Seconds before auto-recycle. Defaults to the particle system's duration.")]
        [SerializeField] private float lifetime = 1.2f;

        private System.Action<PooledVfx> _onDone;

        public void Bind(System.Action<PooledVfx> onDone) => _onDone = onDone;

        public void OnSpawned()
        {
            if (particles != null)
            {
                particles.Clear(true);
                particles.Play(true);
            }
            StartCoroutine(LifeRoutine());
        }

        public void OnDespawned()
        {
            if (particles != null) particles.Stop(true);
            StopAllCoroutines();
        }

        private IEnumerator LifeRoutine()
        {
            yield return new WaitForSeconds(lifetime);
            _onDone?.Invoke(this);
        }
    }

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
