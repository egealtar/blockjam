using System.Collections.Generic;
using UnityEngine;

namespace BlockJam.Pooling
{
    /// <summary>
    /// Implement this on a pooled MonoBehaviour to react to being spawned/recycled.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// A hand-written generic pool for MonoBehaviour prefabs. Satisfies the case's hard rule:
    /// "Units must be reused via object pooling instead of Instantiate/Destroy at runtime,
    ///  and no third-party pooling library may be used."
    ///
    /// Instantiate is called ONLY here, and ONLY when the pool needs to grow (prewarm + overflow).
    /// At runtime, Get()/Release() move objects between an inactive stack and the live set by
    /// toggling activeness - never destroying them. Destroy happens only on Clear() (scene teardown).
    /// </summary>
    /// <typeparam name="T">A component on the pooled prefab, e.g. Unit.</typeparam>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _inactive = new Stack<T>();
        private readonly HashSet<T> _active = new HashSet<T>();

        public int CountInactive => _inactive.Count;
        public int CountActive => _active.Count;

        public ObjectPool(T prefab, Transform parent, int prewarm = 0)
        {
            _prefab = prefab;
            _parent = parent;
            Prewarm(prewarm);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T instance = CreateNew();
                instance.gameObject.SetActive(false);
                _inactive.Push(instance);
            }
        }

        private T CreateNew()
        {
            // The ONLY Instantiate call in the gameplay loop.
            T instance = Object.Instantiate(_prefab, _parent);
            return instance;
        }

        public T Get()
        {
            T instance = _inactive.Count > 0 ? _inactive.Pop() : CreateNew();
            _active.Add(instance);
            instance.gameObject.SetActive(true);

            if (instance is IPoolable poolable) poolable.OnSpawned();
            return instance;
        }

        public void Release(T instance)
        {
            if (instance == null) return;
            if (!_active.Remove(instance)) return; // not from this pool / already released

            if (instance is IPoolable poolable) poolable.OnDespawned();

            instance.transform.SetParent(_parent, worldPositionStays: false);
            instance.gameObject.SetActive(false);
            _inactive.Push(instance);
        }

        /// <summary>Releases every currently-active instance back to the pool.</summary>
        public void ReleaseAll()
        {
            // Copy to avoid mutating the set while iterating.
            var temp = new List<T>(_active);
            for (int i = 0; i < temp.Count; i++) Release(temp[i]);
        }

        /// <summary>Destroys all pooled objects. Only used on teardown, never mid-gameplay.</summary>
        public void Clear()
        {
            ReleaseAll();
            while (_inactive.Count > 0)
            {
                T instance = _inactive.Pop();
                if (instance != null) Object.Destroy(instance.gameObject);
            }
        }
    }
}
