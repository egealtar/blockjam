using UnityEngine;
using BlockJam.Pooling;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Thin wrapper around ObjectPool&lt;Unit&gt; so the rest of the game asks for units
    /// without touching pooling internals. Lives on a GameObject so the pooled units have
    /// a tidy parent in the hierarchy.
    /// </summary>
    public class UnitPool : MonoBehaviour
    {
        [SerializeField] private Unit unitPrefab;

        private ObjectPool<Unit> _pool;

        public void Setup(int prewarm)
        {
            _pool = new ObjectPool<Unit>(unitPrefab, transform, prewarm);
        }

        public Unit Get() => _pool.Get();

        public void Release(Unit unit) => _pool.Release(unit);

        public void ReleaseAll() => _pool.ReleaseAll();

        public void Clear() => _pool.Clear();
    }
}
