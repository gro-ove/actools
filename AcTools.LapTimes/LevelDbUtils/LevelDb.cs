// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LevelDb.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AcTools.LapTimes.LevelDbUtils {
    /// <summary>
    /// A DB is a persistent ordered map from keys to values.
    /// A DB is safe for concurrent access from multiple threads without any external synchronization.
    /// </summary>
    internal class LevelDb : LevelDbHandle, IEnumerable<KeyValuePair<string, string>>, IEnumerable<KeyValuePair<byte[], byte[]>>, 
            IEnumerable<KeyValuePair<int, int[]>> {
        private readonly Cache _cache;
        private readonly Comparator _comparator;

        static void Throw(IntPtr error) {
            Throw(error, msg => new Exception(msg));
        }

        static void Throw(IntPtr error, Func<string, Exception> exception) {
            if (error != IntPtr.Zero) {
                try {
                    var msg = Marshal.PtrToStringAnsi(error);
                    throw exception(msg);
                } finally {
                    LevelDbInterop.leveldb_free(error);
                }
            }
        }

        /// <summary>
        /// Open the database with the specified "name".
        /// </summary>
        public LevelDb(Options options, string name) {
            IntPtr error;
            _cache = options.Cache;
            _comparator = options.Comparator;
            Handle = LevelDbInterop.leveldb_open(options.Handle, name, out error);
            Throw(error, msg => new UnauthorizedAccessException(msg));
        }

        public void Close() {
            (this as IDisposable).Dispose();
        }

        /// <summary>
        /// Set the database entry for "key" to "value".  
        /// Note: consider setting new WriteOptions{ Sync = true }.
        /// </summary>
        public void Put(string key, string value, WriteOptions options) {
            Put(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(value), options);
        }

        /// <summary>
        /// Set the database entry for "key" to "value". 
        /// </summary>
        public void Put(string key, string value) {
            Put(key, value, new WriteOptions());
        }

        /// <summary>
        /// Set the database entry for "key" to "value".  
        /// </summary>
        public void Put(byte[] key, byte[] value) {
            Put(key, value, new WriteOptions());
        }

        /// <summary>
        /// Set the database entry for "key" to "value".  
        /// Note: consider setting new WriteOptions{ Sync = true }.
        /// </summary>
        public void Put(byte[] key, byte[] value, WriteOptions options) {
            IntPtr error;
            LevelDbInterop.leveldb_put(Handle, options.Handle, key, (IntPtr)key.LongLength, value, (IntPtr)value.LongLength, out error);
            Throw(error);
        }

        /// <summary>
        /// Set the database entry for "key" to "value".  
        /// Note: consider setting new WriteOptions{ Sync = true }.
        /// </summary>
        public void Put(int key, int[] value) {
            Put(key, value, new WriteOptions());
        }

        /// <summary>
        /// Set the database entry for "key" to "value".  
        /// Note: consider setting new WriteOptions{ Sync = true }.
        /// </summary>
        public void Put(int key, int[] value, WriteOptions options) {
            IntPtr error;
            LevelDbInterop.leveldb_put(Handle, options.Handle, ref key, (IntPtr)sizeof(int), value, checked((IntPtr)(value.LongLength * 4)), out error);
            Throw(error);
        }

        /// <summary>
        /// Remove the database entry (if any) for "key".  
        /// It is not an error if "key" did not exist in the database.
        /// </summary>
        public void Delete(string key) {
            Delete(key, new WriteOptions());
        }

        /// <summary>
        /// Remove the database entry (if any) for "key".  
        /// It is not an error if "key" did not exist in the database.
        /// Note: consider setting new WriteOptions{ Sync = true }.
        /// </summary>
        public void Delete(string key, WriteOptions options) {
            Delete(Encoding.ASCII.GetBytes(key), options);
        }

        /// <summary>
        /// Remove the database entry (if any) for "key".  
        /// It is not an error if "key" did not exist in the database.
        /// </summary>
        public void Delete(byte[] key) {
            Delete(key, new WriteOptions());
        }

        /// <summary>
        /// Remove the database entry (if any) for "key".  
        /// It is not an error if "key" did not exist in the database.
        /// Note: consider setting new WriteOptions{ Sync = true }.
        /// </summary>
        public void Delete(byte[] key, WriteOptions options) {
            IntPtr error;
            LevelDbInterop.leveldb_delete(Handle, options.Handle, key, (IntPtr)key.LongLength, out error);
            Throw(error);
        }

        public void Write(WriteBatch batch) {
            Write(batch, new WriteOptions());
        }

        public void Write(WriteBatch batch, WriteOptions options) {
            IntPtr error;
            LevelDbInterop.leveldb_write(Handle, options.Handle, batch.Handle, out error);
            Throw(error);
        }

        /// <summary>
        /// If the database contains an entry for "key" return the value,
        /// otherwise return null.
        /// </summary>
        public string Get(string key, ReadOptions options) {
            var value = Get(Encoding.ASCII.GetBytes(key), options);
            if (value != null) return Encoding.ASCII.GetString(value);
            return null;
        }

        /// <summary>
        /// If the database contains an entry for "key" return the value,
        /// otherwise return null.
        /// </summary>
        public string Get(string key) {
            return Get(key, new ReadOptions());
        }

        /// <summary>
        /// If the database contains an entry for "key" return the value,
        /// otherwise return null.
        /// </summary>
        public byte[] Get(byte[] key) {
            return Get(key, new ReadOptions());
        }

        /// <summary>
        /// If the database contains an entry for "key" return the value,
        /// otherwise return null.
        /// </summary>
        public byte[] Get(byte[] key, ReadOptions options) {
            IntPtr error;
            IntPtr length;
            var v = LevelDbInterop.leveldb_get(Handle, options.Handle, key, (IntPtr)key.LongLength, out length, out error);
            Throw(error);

            if (v == IntPtr.Zero) return null;

            try {
                var bytes = new byte[(long)length];

                // TODO: Consider copy loop, as Marshal.Copy has 2GB-1 limit, or native pointers
                Marshal.Copy(v, bytes, 0, (int)length);
                return bytes;
            } finally {
                LevelDbInterop.leveldb_free(v);
            }
        }

        /// <summary>
        /// If the database contains an entry for "key" return the value,
        /// otherwise return null.
        /// </summary>
        public int[] Get(int key) {
            return Get(key, new ReadOptions());
        }

        /// <summary>
        /// If the database contains an entry for "key" return the value,
        /// otherwise return null.
        /// </summary>
        public int[] Get(int key, ReadOptions options) {
            IntPtr error;
            IntPtr length;
            IntPtr v;
            v = LevelDbInterop.leveldb_get(Handle, options.Handle, ref key, (IntPtr)sizeof(int), out length, out error);
            Throw(error);

            if (v == IntPtr.Zero) return null;

            try {
                var bytes = new int[(long)length / 4];

                // TODO: consider >2GB-1
                Marshal.Copy(v, bytes, 0, checked((int)bytes.LongLength));
                return bytes;
            } finally {
                LevelDbInterop.leveldb_free(v);
            }
        }

        public NativeArray<T> GetRaw<T>(NativeArray key) where T : struct {
            return GetRaw<T>(key, new ReadOptions());
        }

        public NativeArray<T> GetRaw<T>(NativeArray key, ReadOptions options) where T : struct {
            IntPtr error;
            IntPtr length;

            var handle = new LevelDbFreeHandle();

            // TODO: Remove typecast to int
            var v = (Ptr<T>)LevelDbInterop.leveldb_get(
                    Handle,
                    options.Handle,
                    key.BaseAddr,
                    key.ByteLength,
                    out length,
                    out error);

            handle.SetHandle((IntPtr)v);

            // round down, truncating the array slightly if needed
            var count = (IntPtr)((ulong)length / Ptr<T>.SizeofT);
            return new NativeArray<T>(v, count, handle);
        }

        /// <summary>
        /// Return an iterator over the contents of the database.
        /// The result of CreateIterator is initially invalid (caller must
        /// call one of the Seek methods on the iterator before using it).
        /// </summary>
        public Iterator CreateIterator() {
            return CreateIterator(new ReadOptions());
        }

        /// <summary>
        /// Return an iterator over the contents of the database.
        /// The result of CreateIterator is initially invalid (caller must
        /// call one of the Seek methods on the iterator before using it).
        /// </summary>
        public Iterator CreateIterator(ReadOptions options) {
            return new Iterator(LevelDbInterop.leveldb_create_iterator(Handle, options.Handle));
        }

        /// <summary>
        /// Return a handle to the current DB state.  
        /// Iterators and Gets created with this handle will all observe a stable snapshot of the current DB state.  
        /// </summary>
        public SnapShot CreateSnapshot() {
            return new SnapShot(LevelDbInterop.leveldb_create_snapshot(Handle), this);
        }

        /// <summary>
        /// DB implementations can export properties about their state
        /// via this method.  If "property" is a valid property understood by this
        /// DB implementation, fills "*value" with its current value and returns
        /// true.  Otherwise returns false.
        ///
        /// Valid property names include:
        ///
        ///  "leveldb.num-files-at-level<N>" - return the number of files at level <N>,
        ///     where <N> is an ASCII representation of a level number (e.g. "0").
        ///  "leveldb.stats" - returns a multi-line string that describes statistics
        ///     about the internal operation of the DB.
        /// </summary>
        public string PropertyValue(string name) {
            var ptr = LevelDbInterop.leveldb_property_value(Handle, name);
            if (ptr == IntPtr.Zero) return null;

            try {
                return Marshal.PtrToStringAnsi(ptr);
            } finally {
                LevelDbInterop.leveldb_free(ptr);
            }
        }

        /// <summary>
        /// If a DB cannot be opened, you may attempt to call this method to
        /// resurrect as much of the contents of the database as possible.
        /// Some data may be lost, so be careful when calling this function
        /// on a database that contains important information.
        /// </summary>
        public static void Repair(Options options, string name) {
            IntPtr error;
            LevelDbInterop.leveldb_repair_db(options.Handle, name, out error);
            Throw(error);
        }

        /// <summary>
        /// Destroy the contents of the specified database.
        /// Be very careful using this method.
        /// </summary>
        public static void Destroy(Options options, string name) {
            IntPtr error;
            LevelDbInterop.leveldb_destroy_db(options.Handle, name, out error);
            Throw(error);
        }

        protected override void FreeUnManagedObjects() {
            if (Handle != default(IntPtr)) LevelDbInterop.leveldb_close(Handle);

            // it's critical that the database be closed first, as the logger and cache may depend on it.
            _cache?.Dispose();
            _comparator?.Dispose();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() {
            using (var sn = CreateSnapshot())
            using (var iterator = CreateIterator(new ReadOptions { Snapshot = sn })) {
                iterator.SeekToFirst();
                while (iterator.IsValid()) {
                    yield return new KeyValuePair<string, string>(iterator.KeyAsString(), iterator.ValueAsString());
                    iterator.Next();
                }
            }
        }

        public IEnumerator<KeyValuePair<byte[], byte[]>> GetEnumerator() {
            using (var sn = CreateSnapshot())
            using (var iterator = CreateIterator(new ReadOptions { Snapshot = sn })) {
                iterator.SeekToFirst();
                while (iterator.IsValid()) {
                    yield return new KeyValuePair<byte[], byte[]>(iterator.Key(), iterator.Value());
                    iterator.Next();
                }
            }
        }

        IEnumerator<KeyValuePair<int, int[]>> IEnumerable<KeyValuePair<int, int[]>>.GetEnumerator() {
            using (var sn = CreateSnapshot())
            using (var iterator = CreateIterator(new ReadOptions { Snapshot = sn })) {
                iterator.SeekToFirst();
                while (iterator.IsValid()) {
                    yield return new KeyValuePair<int, int[]>(iterator.KeyAsInt(), iterator.ValueAsInts());
                    iterator.Next();
                }
            }
        }
    }
}