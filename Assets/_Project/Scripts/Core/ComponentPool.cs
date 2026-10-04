using System.Collections.Generic;
using UnityEngine;

namespace MobaTrainer.Core
{
    /// <summary>
    /// Allocation-free (after pre-warm) pool for scene components.
    /// Objects are deactivated when returned; <see cref="Active"/> lists everything currently out.
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly Transform parent;
        private readonly Stack<T> free;
        private readonly List<T> active;

        public IReadOnlyList<T> Active => active;
        public int CountActive => active.Count;
        public int CountTotal => active.Count + free.Count;

        public ComponentPool(T prefab, Transform parent, int prewarm)
        {
            this.prefab = prefab;
            this.parent = parent;
            free = new Stack<T>(prewarm);
            active = new List<T>(prewarm);
            for (int i = 0; i < prewarm; i++)
            {
                free.Push(Create());
            }
        }

        private T Create()
        {
            T item = Object.Instantiate(prefab, parent);
            item.gameObject.SetActive(false);
            return item;
        }

        public T Get()
        {
            T item = free.Count > 0 ? free.Pop() : Create();
            active.Add(item);
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>Returns the item at <paramref name="activeIndex"/> (swap-remove, O(1)).</summary>
        public void ReleaseAt(int activeIndex)
        {
            T item = active[activeIndex];
            int last = active.Count - 1;
            active[activeIndex] = active[last];
            active.RemoveAt(last);
            item.gameObject.SetActive(false);
            free.Push(item);
        }

        public void Release(T item)
        {
            int index = active.IndexOf(item);
            if (index >= 0) ReleaseAt(index);
        }

        public void ReleaseAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) ReleaseAt(i);
        }
    }
}
