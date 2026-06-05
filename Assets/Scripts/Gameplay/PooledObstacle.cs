using UnityEngine;
using BlockJam.Pooling;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Visual for a permanent obstacle cell ("wall box" / closed-area marker, like the grass
    /// border in Block Jam). It is pooled and reused across levels - never Instantiated during
    /// gameplay actions, only when the pool must grow. It carries no logic; the board decides
    /// where obstacles live and toggles these views on/off via the pool.
    ///
    /// Must live in its own file (class name == file name) so Unity exposes it in Add Component.
    /// </summary>
    public class PooledObstacle : MonoBehaviour, IPoolable
    {
        public void OnSpawned() { }
        public void OnDespawned() { }
    }
}
