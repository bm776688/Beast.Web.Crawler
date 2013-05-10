using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Advertising.Analytics.SharedService
{
    public class LruCache<TKey, TValue>// : ICache<TKey, TValue>
    {
        class Item
        {
            internal TKey key;
            internal TValue value;

            internal Item(TKey key, TValue value)
            {
                this.key = key;
                this.value = value;
            }
            public override string ToString()
            {
                return key.ToString() + " : " + value.ToString();
            }
        }

        int maxSize;
        LinkedList<Item> lruList;
        Dictionary<TKey, LinkedListNode<Item>> cache;
        object lockObj = new object();

        public LruCache(int maxSize)
        {
            this.maxSize = maxSize;
            lruList = new LinkedList<Item>();
            cache = new Dictionary<TKey, LinkedListNode<Item>>(maxSize);
        }

        public TValue Search(TKey key)
        {
            LinkedListNode<Item> node = null;
            lock (lockObj)
            {
                if (cache.TryGetValue(key, out node))
                {
                    // update LRU list.
                    MoveToHead(node);
                    return node.Value.value;
                }
            }

            return default(TValue);
        }

        public void UpdateCache(TKey key, TValue value)
        {
            lock (lockObj)
            {
                LinkedListNode<Item> node = null;
                if (cache.TryGetValue(key, out node))
                {
                    MoveToHead(node);
                    node.Value.value = value;
                }
                else
                {
                    node = lruList.AddFirst(new Item(key,value));
                    cache.Add(key, node);
                }

                if (cache.Count > maxSize)
                {
                    // remove LRU item
                    node = lruList.Last;
                    lruList.Remove(node);
                    cache.Remove(node.Value.key);
                }
            }
        }

        private void MoveToHead(LinkedListNode<Item> node)
        {
            lruList.Remove(node);
            lruList.AddFirst(node);
        }
    }
    /*
     * Dummy memory cache. used as a place holder when memory cache is disabled.
     */
/*    public class DummyMemoryCache<TKey, TValue> : ICache<TKey, TValue>
    {
        public TValue Search(TKey key)
        {
            return default(TValue);
        }
        public void UpdateCache(TKey key, TValue value)
        {
        }
    }*/
}
