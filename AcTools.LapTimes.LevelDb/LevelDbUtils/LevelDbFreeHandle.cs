// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LevelDbFreeHandle.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace AcTools.LapTimes.LevelDb.LevelDbUtils {
    /// <summary>
    /// Wraps pointers to be freed with leveldb_free (e.g. returned by leveldb_get).
    /// Reference on safe handles: http://blogs.msdn.com/b/bclteam/archive/2006/06/23/644343.aspx.
    /// </summary>
    internal class LevelDbFreeHandle : SafeHandle {
        public LevelDbFreeHandle() : base(default(IntPtr), true) {}

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle() {
            if (handle != default(IntPtr)) LevelDbInterop.leveldb_free(handle);
            handle = default(IntPtr);
            return true;
        }

        public override bool IsInvalid => handle != default(IntPtr);

        public new void SetHandle(IntPtr p) {
            if (handle != default(IntPtr)) ReleaseHandle();
            base.SetHandle(p);
        }
    }
}
