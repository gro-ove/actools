// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WriteBatch.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Text;

namespace AcTools.LapTimes.LevelDbUtils {
    /// <summary>
    /// WriteBatch holds a collection of updates to apply atomically to a DB.
    ///
    /// The updates are applied in the order in which they are added
    /// to the WriteBatch.  For example, the value of "key" will be "v3"
    /// after the following batch is written:
    ///
    ///    batch.Put("key", "v1");
    ///    batch.Delete("key");
    ///    batch.Put("key", "v2");
    ///    batch.Put("key", "v3");
    /// </summary>
    internal class WriteBatch : LevelDbHandle {
        public WriteBatch() {
            Handle = LevelDbInterop.leveldb_writebatch_create();
        }

        /// <summary>
        /// Clear all updates buffered in this batch.
        /// </summary>
        public void Clear() {
            LevelDbInterop.leveldb_writebatch_clear(Handle);
        }

        /// <summary>
        /// Store the mapping "key->value" in the database.
        /// </summary>
        public WriteBatch Put(string key, string value) {
            return Put(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(value));
        }

        /// <summary>
        /// Store the mapping "key->value" in the database.
        /// </summary>
        public WriteBatch Put(byte[] key, byte[] value) {
            LevelDbInterop.leveldb_writebatch_put(Handle, key, key.Length, value, value.Length);
            return this;
        }

        /// <summary>
        /// If the database contains a mapping for "key", erase it.  
        /// Else do nothing.
        /// </summary>
        public WriteBatch Delete(string key) {
            return Delete(Encoding.ASCII.GetBytes(key));
        }

        /// <summary>
        /// If the database contains a mapping for "key", erase it.  
        /// Else do nothing.
        /// </summary>
        public WriteBatch Delete(byte[] key) {
            LevelDbInterop.leveldb_writebatch_delete(Handle, key, key.Length);
            return this;
        }

        /// <summary>
        /// Support for iterating over a batch.
        /// </summary>
        public void Iterate(object state, Action<object, byte[], int, byte[], int> put, Action<object, byte[], int> deleted) {
            LevelDbInterop.leveldb_writebatch_iterate(Handle, state, put, deleted);
        }

        protected override void FreeUnManagedObjects() {
            LevelDbInterop.leveldb_writebatch_destroy(Handle);
        }

    }
}