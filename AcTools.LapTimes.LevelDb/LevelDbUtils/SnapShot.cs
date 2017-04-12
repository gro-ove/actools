// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SnapShot.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace AcTools.LapTimes.LevelDb.LevelDbUtils {
    /// <summary>
    /// A Snapshot is an immutable object and can therefore be safely
    /// accessed from multiple threads without any external synchronization.
    /// </summary>
    internal class SnapShot : LevelDbHandle {
        // pointer to parent so that we can call ReleaseSnapshot(this) when disposed
        public WeakReference<LevelDb> Parent;

        internal SnapShot(IntPtr handle, LevelDb parent) {
            Handle = handle;
            Parent = new WeakReference<LevelDb>(parent);
        }

        internal SnapShot(IntPtr handle) {
            Handle = handle;
            Parent = new WeakReference<LevelDb>(null);
        }

        protected override void FreeUnManagedObjects() {
            LevelDb parent;
            if (Parent.TryGetTarget(out parent)) {
                LevelDbInterop.leveldb_release_snapshot(parent.Handle, Handle);
            }
        }
    }
}
