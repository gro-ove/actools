// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LevelDbHandle.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace AcTools.LapTimes.LevelDbUtils {
    /// <summary>
    /// Base class for all LevelDB objects
    /// Implement IDisposable as prescribed by http://msdn.microsoft.com/en-us/library/b1yfkh5e.aspx by overriding the two additional virtual methods
    /// </summary>
    internal abstract class LevelDbHandle : IDisposable {
        public IntPtr Handle { protected set; get; }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void FreeManagedObjects() {}

        protected virtual void FreeUnManagedObjects() {}

        private bool _disposed;

        void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                FreeManagedObjects();
            }

            if (Handle != IntPtr.Zero) {
                FreeUnManagedObjects();
                Handle = IntPtr.Zero;
            }

            _disposed = true;
        }

        ~LevelDbHandle() {
            Dispose(false);
        }
    }
}