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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AcTools.LapTimes.LevelDb.LevelDbUtils {
    internal class Comparator : LevelDbHandle {
        private sealed class Inner : IDisposable {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate void Destructor(IntPtr gcHandleThis);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate int Compare(IntPtr gcHandleThis,
                    IntPtr data1, IntPtr size1,
                    IntPtr data2, IntPtr size2);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate IntPtr Name(IntPtr gcHandleThis);


            private static readonly Destructor DestructorInstance = gcHandleThis => {
                var h = GCHandle.FromIntPtr(gcHandleThis);
                var This = (Inner)h.Target;

                This.Dispose();

                // TODO: At the point 'Free' is entered, this delegate may become elligible to be GC'd.
                // Need to look whether GC might run between then, and when this delegate returns.
                h.Free();
            };

            private static readonly Compare CompareInstance = (gcHandleThis, data1, size1, data2, size2) => {
                var This = (Inner)GCHandle.FromIntPtr(gcHandleThis).Target;
                return This._cmp(new NativeArray { BaseAddr = data1, ByteLength = size1 },
                        new NativeArray { BaseAddr = data2, ByteLength = size2 });
            };

            private static readonly Name NameAccessor = gcHandleThis => {
                var This = (Inner)GCHandle.FromIntPtr(gcHandleThis).Target;
                return This.NameValue;
            };

            private Func<NativeArray, NativeArray, int> _cmp;
            private GCHandle _namePinned;

            public IntPtr Init(string name, Func<NativeArray, NativeArray, int> cmp) {
                // TODO: Complete member initialization
                _cmp = cmp;

                _namePinned = GCHandle.Alloc(
                        Encoding.ASCII.GetBytes(name),
                        GCHandleType.Pinned);

                var thisHandle = GCHandle.Alloc(this);
                var chandle = LevelDbInterop.leveldb_comparator_create(
                        GCHandle.ToIntPtr(thisHandle),
                        Marshal.GetFunctionPointerForDelegate(DestructorInstance),
                        Marshal.GetFunctionPointerForDelegate(CompareInstance),
                        Marshal.GetFunctionPointerForDelegate(NameAccessor));
                if (chandle == default) {
                    thisHandle.Free();
                }

                return chandle;
            }

#if UNSAFE
            private unsafe IntPtr NameValue {
                get {
                    // TODO: this is probably not the most effective way to get a pinned string
                    var s = (byte[])_namePinned.Target;
                    fixed (byte* p = s) {
                        // Note: pinning the GCHandle ensures this value should remain stable
                        // outside of the 'fixed' block.
                        return (IntPtr)p;
                    }
                }
            }
#else
            private IntPtr NameValue => _namePinned.AddrOfPinnedObject();
#endif


            public void Dispose() {
                if (_namePinned.IsAllocated) _namePinned.Free();
            }
        }

        private Comparator(string name, Func<NativeArray, NativeArray, int> cmp) {
            var inner = new Inner();
            try {
                Handle = inner.Init(name, cmp);
            } finally {
                if (Handle == default) inner.Dispose();
            }
        }

        public static Comparator Create(string name, Func<NativeArray, NativeArray, int> cmp) {
            return new Comparator(name, cmp);
        }

        public static Comparator Create(string name, IComparer<NativeArray> cmp) {
            return new Comparator(name, cmp.Compare);
        }

        protected override void FreeUnManagedObjects() {
            if (Handle != default) {
                // indirectly invoked CleanupInner
                LevelDbInterop.leveldb_comparator_destroy(Handle);
            }
        }
    }
}