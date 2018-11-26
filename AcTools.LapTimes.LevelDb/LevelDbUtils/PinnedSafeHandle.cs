// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PinnedSafeHandle.cs" company="Microsoft">
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

namespace AcTools.LapTimes.LevelDb.LevelDbUtils {
    internal class PinnedSafeHandle<T> : SafeHandle where T : struct {
        private GCHandle _pinnedRawData;

        public PinnedSafeHandle(T[] arr) : base(default, true) {
            _pinnedRawData = GCHandle.Alloc(arr, GCHandleType.Pinned);

            // initialize handle last; ensure we only free initialized GCHandles.
            handle = _pinnedRawData.AddrOfPinnedObject();
        }

        public Ptr<T> Ptr => (Ptr<T>)handle;

        public override bool IsInvalid => handle == default;

        protected override bool ReleaseHandle() {
            if (handle != default) {
                _pinnedRawData.Free();
                handle = default;
            }
            return true;
        }
    }
}