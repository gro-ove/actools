using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AcTools.Utils.Helpers {
    public class LruCache<TKey, TValue> {
        private class LruCacheItem {
            public LruCacheItem(TKey k, TValue v) {
                Key = k;
                Value = v;
            }

            public readonly TKey Key;
            public readonly TValue Value;
        }

        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<LruCacheItem>> _cacheMap;
        private readonly LinkedList<LruCacheItem> _lruList;

        public LruCache(int capacity) {
            _capacity = capacity;
            _cacheMap = new Dictionary<TKey, LinkedListNode<LruCacheItem>>(_capacity);
            _lruList = new LinkedList<LruCacheItem>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TValue Get(TKey key) {
            if (_cacheMap.TryGetValue(key, out var node)) {
                var value = node.Value.Value;
                _lruList.Remove(node);
                _lruList.AddLast(node);
                return value;
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TValue Get(TKey key, Func<TValue> factory) {
            if (_cacheMap.TryGetValue(key, out var node)) {
                _lruList.Remove(node);
                _lruList.AddLast(node);
            } else {
                node = new LinkedListNode<LruCacheItem>(new LruCacheItem(key, factory()));
                _lruList.AddLast(node);
                _cacheMap.Add(key, node);
            }
            return node.Value.Value;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(TKey key, TValue val) {
            if (_cacheMap.Count >= _capacity) {
                RemoveFirst();
            }

            var cacheItem = new LruCacheItem(key, val);
            var node = new LinkedListNode<LruCacheItem>(cacheItem);
            _lruList.AddLast(node);
            _cacheMap.Add(key, node);
        }

        private void RemoveFirst() {
            var node = _lruList.First;
            _lruList.RemoveFirst();
            _cacheMap.Remove(node.Value.Key);
        }
    }
}