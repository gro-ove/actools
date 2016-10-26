// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Cache.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace AcTools.LapTimes.LevelDbUtils {
    /// <summary>
    /// A Cache is an interface that maps keys to values.  It has internal
    /// synchronization and may be safely accessed concurrently from
    /// multiple threads.  It may automatically evict entries to make room
    /// for new entries.  Values have a specified charge against the cache
    /// capacity.  For example, a cache where the values are variable
    /// length strings, may use the length of the string as the charge for
    /// the string.
    ///
    /// A builtin cache implementation with a least-recently-used eviction
    /// policy is provided.  Clients may use their own implementations if
    /// they want something more sophisticated (like scan-resistance, a
    /// custom eviction policy, variable cache sizing, etc.)
    /// </summary>
    internal class Cache : LevelDbHandle {
        /// <summary>
        /// Create a new cache with a fixed size capacity.  This implementation
        /// of Cache uses a LRU eviction policy.
        /// </summary>
        public Cache(int capacity) {
            Handle = LevelDbInterop.leveldb_cache_create_lru(capacity);
        }

        protected override void FreeUnManagedObjects() {
            LevelDbInterop.leveldb_cache_destroy(Handle);
        }
    }
}