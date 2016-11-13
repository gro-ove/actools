// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Iterator.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AcTools.LapTimes.LevelDbUtils {
    /// <summary>
    /// An iterator yields a sequence of key/value pairs from a database.
    /// </summary>
    internal class Iterator : LevelDbHandle {
        internal Iterator(IntPtr handle) {
            Handle = handle;
        }

        /// <summary>
        /// An iterator is either positioned at a key/value pair, or
        /// not valid.  
        /// </summary>
        /// <returns>This method returns true iff the iterator is valid.</returns>
        public bool IsValid() {
            return LevelDbInterop.leveldb_iter_valid(Handle) != 0;
        }

        /// <summary>
        /// Position at the first key in the source.  
        /// The iterator is Valid() after this call iff the source is not empty.
        /// </summary>
        public void SeekToFirst() {
            LevelDbInterop.leveldb_iter_seek_to_first(Handle);
            Throw();
        }

        /// <summary>
        /// Position at the last key in the source.  
        /// The iterator is Valid() after this call iff the source is not empty.
        /// </summary>
        public void SeekToLast() {
            LevelDbInterop.leveldb_iter_seek_to_last(Handle);
            Throw();
        }

        /// <summary>
        /// Position at the first key in the source that at or past target
        /// The iterator is Valid() after this call iff the source contains
        /// an entry that comes at or past target.
        /// </summary>
        public void Seek(byte[] key) {
            LevelDbInterop.leveldb_iter_seek(Handle, key, key.Length);
            Throw();
        }

        /// <summary>
        /// Position at the first key in the source that at or past target
        /// The iterator is Valid() after this call iff the source contains
        /// an entry that comes at or past target.
        /// </summary>
        public void Seek(string key) {
            Seek(Encoding.ASCII.GetBytes(key));
        }

        /// <summary>
        /// Position at the first key in the source that at or past target
        /// The iterator is Valid() after this call iff the source contains
        /// an entry that comes at or past target.
        /// </summary>
        public void Seek(int key) {
            LevelDbInterop.leveldb_iter_seek(Handle, ref key, 4);
            Throw();
        }

        /// <summary>
        /// Moves to the next entry in the source.  
        /// After this call, Valid() is true iff the iterator was not positioned at the last entry in the source.
        /// REQUIRES: Valid()
        /// </summary>
        public void Next() {
            LevelDbInterop.leveldb_iter_next(Handle);
            Throw();
        }

        /// <summary>
        /// Moves to the previous entry in the source.  
        /// After this call, Valid() is true iff the iterator was not positioned at the first entry in source.
        /// REQUIRES: Valid()
        /// </summary>
        public void Prev() {
            LevelDbInterop.leveldb_iter_prev(Handle);
            Throw();
        }


        /// <summary>
        /// Return the key for the current entry.  
        /// REQUIRES: Valid()
        /// </summary>
        public int KeyAsInt() {
            int length;
            var key = LevelDbInterop.leveldb_iter_key(Handle, out length);
            Throw();

            if (length != 4) throw new Exception("Key is not an integer");
            return Marshal.ReadInt32(key);
        }

        /// <summary>
        /// Return the key for the current entry.  
        /// REQUIRES: Valid()
        /// </summary>
        public string KeyAsString() {
            return Encoding.ASCII.GetString(Key());
        }

        /// <summary>
        /// Return the key for the current entry.  
        /// REQUIRES: Valid()
        /// </summary>
        public byte[] Key() {
            int length;
            var key = LevelDbInterop.leveldb_iter_key(Handle, out length);
            Throw();

            var bytes = new byte[length];
            Marshal.Copy(key, bytes, 0, length);
            return bytes;
        }

        /// <summary>
        /// Return the value for the current entry.  
        /// REQUIRES: Valid()
        /// </summary>
        public int[] ValueAsInts() {
            int length;
            var value = LevelDbInterop.leveldb_iter_value(Handle, out length);
            Throw();

            var bytes = new int[length / 4];
            Marshal.Copy(value, bytes, 0, length / 4);
            return bytes;
        }

        /// <summary>
        /// Return the value for the current entry.  
        /// REQUIRES: Valid()
        /// </summary>
        public string ValueAsString() {
            return Encoding.ASCII.GetString(Value());
        }

        /// <summary>
        /// Return the value for the current entry.  
        /// REQUIRES: Valid()
        /// </summary>
        public byte[] Value() {
            int length;
            var value = LevelDbInterop.leveldb_iter_value(Handle, out length);
            Throw();

            var bytes = new byte[length];
            Marshal.Copy(value, bytes, 0, length);
            return bytes;
        }

        /// <summary>
        /// If an error has occurred, throw it.  
        /// </summary>
        void Throw() {
            Throw(msg => new Exception(msg));
        }

        /// <summary>
        /// If an error has occurred, throw it.  
        /// </summary>
        void Throw(Func<string, Exception> exception) {
            IntPtr error;
            LevelDbInterop.leveldb_iter_get_error(Handle, out error);
            if (error != IntPtr.Zero) throw exception(Marshal.PtrToStringAnsi(error));
        }

        protected override void FreeUnManagedObjects() {
            LevelDbInterop.leveldb_iter_destroy(Handle);
        }
    }
}