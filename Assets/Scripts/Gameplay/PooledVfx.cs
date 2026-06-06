using System.Collections;
using UnityEngine;
using BlockJam.Pooling;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// A pooled one-shot particle effect. Auto-returns to its pool after its lifetime,
    /// so VFX also obey the "no Instantiate/Destroy at runtime" rule.
    /// Must live in its own file named PooledVfx.cs so Unity exposes it in Add Component.
    /// </summary>
    public class PooledVfx : MonoBehaviour, IPoolable
    {
        [SerializeField] private ParticleSystem particles;
        [Tooltip("Seconds before auto-recycle. Defaults to the particle system's duration.")]
        [SerializeField] private float lifetime = 1.2f;

        private System.Action<PooledVfx> _onDone;

        public void Bind(System.Action<PooledVfx> onDone) => _onDone = onDone;

        /// <summary>Tints the effect to a given color (used so the running dust matches the unit).
        /// Sets the particle system's start color; child systems are tinted too.</summary>
        public void SetColor(Color color)
        {
            if (particles == null) return;
            var main = particles.main;
            main.startColor = color;
            foreach (var child in GetComponentsInChildren<ParticleSystem>())
            {
                var m = child.main;
                m.startColor = color;
            }
        }

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
}