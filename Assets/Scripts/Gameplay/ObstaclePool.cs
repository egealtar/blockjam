using UnityEngine;
using BlockJam.Pooling;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Thin wrapper around ObjectPool&lt;PooledObstacle&gt; for the permanent "wall box" visuals.
    /// The board asks for an obstacle view per closed cell and releases them all when a level
    /// is rebuilt. Like every pool in the project, Instantiate only happens on prewarm/growth.
    /// </summary>
    public class ObstaclePool : MonoBehaviour
    {
        [SerializeField] private PooledObstacle obstaclePrefab;

        private ObjectPool<PooledObstacle> _pool;

        public void Setup(int prewarm)
        {
            _pool = new ObjectPool<PooledObstacle>(obstaclePrefab, transform, prewarm);
        }

        public PooledObstacle Get() => _pool.Get();

        public void Release(PooledObstacle obstacle) => _pool.Release(obstacle);

        public void ReleaseAll() => _pool?.ReleaseAll();
    }
}
