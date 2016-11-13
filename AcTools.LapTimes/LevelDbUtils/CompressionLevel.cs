// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CompressionLevel.cs" company="Microsoft">
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
    /// DB contents are stored in a set of blocks, each of which holds a
    /// sequence of key,value pairs.  Each block may be compressed before
    /// being stored in a file. The following enum describes which
    /// compression method (if any) is used to compress a block.
    /// </summary>
    internal enum CompressionLevel {
        NoCompression = 0,
        SnappyCompression = 1
    }
}