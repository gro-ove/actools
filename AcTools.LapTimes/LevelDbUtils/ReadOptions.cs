// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Comparator.cs" company="Microsoft">
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
    /// Options that control read operations.
    /// </summary>
    internal class ReadOptions : LevelDbHandle {
        public ReadOptions() {
            Handle = LevelDbInterop.leveldb_readoptions_create();
        }

        /// <summary>
        /// If true, all data read from underlying storage will be
        /// verified against corresponding checksums.
        /// </summary>
        public bool VerifyCheckSums {
            set { LevelDbInterop.leveldb_readoptions_set_verify_checksums(Handle, value ? (byte)1 : (byte)0); }
        }

        /// <summary>
        /// Should the data read for this iteration be cached in memory?
        /// Callers may wish to set this field to false for bulk scans.
        /// Default: true
        /// </summary>
        public bool FillCache {
            set { LevelDbInterop.leveldb_readoptions_set_fill_cache(Handle, value ? (byte)1 : (byte)0); }
        }

        /// <summary>
        /// If "snapshot" is provides, read as of the supplied snapshot
        /// (which must belong to the DB that is being read and which must
        /// not have been released).  
        /// If "snapshot" is not set, use an implicit
        /// snapshot of the state at the beginning of this read operation.
        /// </summary>
        public SnapShot Snapshot {
            set { LevelDbInterop.leveldb_readoptions_set_snapshot(Handle, value.Handle); }
        }

        protected override void FreeUnManagedObjects() {
            LevelDbInterop.leveldb_readoptions_destroy(Handle);
        }
    }
}
