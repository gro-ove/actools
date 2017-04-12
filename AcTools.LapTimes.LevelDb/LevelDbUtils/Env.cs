// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Env.cs" company="Microsoft">
//   Copyright (c) 2022 Microsoft Corporation
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
//   with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless
//   required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.See the License for
//   the specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace AcTools.LapTimes.LevelDb.LevelDbUtils {
    /// <summary>
    /// A default environment to access operating system functionality like 
    /// the filesystem etc of the current operating system.
    /// </summary>
    internal class Env : LevelDbHandle {
        public Env() {
            Handle = LevelDbInterop.leveldb_create_default_env();
        }

        protected override void FreeUnManagedObjects() {
            LevelDbInterop.leveldb_env_destroy(Handle);
        }
    }
}