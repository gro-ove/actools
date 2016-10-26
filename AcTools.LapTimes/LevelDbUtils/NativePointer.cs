// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NativePointer.cs" company="Microsoft">
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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AcTools.LapTimes.LevelDbUtils {
    // note: sizeof(Ptr<>) == sizeof(IntPtr) allows you to create Ptr<Ptr<>> of arbitrary depth and "it just works"
    // IntPtr severely lacks appropriate arithmetic operators; up-promotions to ulong used instead.
    internal struct Ptr<T> where T : struct {
        private IntPtr _addr;

        public Ptr(IntPtr addr) {
            _addr = addr;
        }

        // cannot use 'sizeof' operator on generic type parameters
        public static readonly uint SizeofT = (uint)Marshal.SizeOf(typeof(T));
        private static readonly IDeref<T> DerefInstance = GetDeref();

        private static IDeref<T> GetDeref() {
            if (typeof(T) == typeof(int)) return (IDeref<T>)new IntDeref();

            // TODO: other concrete implementations of IDeref.
            // (can't be made generic; will not type check)

            // fallback
            return new MarshalDeref<T>();
        }

        public static explicit operator Ptr<T>(IntPtr p) {
            return new Ptr<T>(p);
        }

        public static explicit operator IntPtr(Ptr<T> p) {
            return p._addr;
        }

        // operator Ptr<U>(Ptr<T>)
        public static Ptr<TU> Cast<TU>(Ptr<T> p)
                where TU : struct {
            return new Ptr<TU>(p._addr);
        }

        public void Inc() {
            Advance((IntPtr)1);
        }

        public void Dec() {
            Advance((IntPtr)(-1));
        }

        public void Advance(IntPtr d) {
            _addr = (IntPtr)((ulong)_addr + SizeofT * (ulong)d);
        }

        public IntPtr Diff(Ptr<T> p2) {
            var diff = (long)((ulong)_addr - (ulong)p2._addr);
            Debug.Assert(diff % SizeofT == 0);
            return (IntPtr)(diff / SizeofT);
        }

        public T Deref() {
            return DerefInstance.Deref(_addr);
        }

        public void DerefWrite(T newValue) {
            DerefInstance.DerefWrite(_addr, newValue);
        }

        // C-style pointer arithmetic. IntPtr is used in place of C's ptrdiff_t
        #region Pointer/IntPtr arithmetic
        public static Ptr<T> operator ++(Ptr<T> p) {
            p.Inc();
            return p;
        }

        public static Ptr<T> operator --(Ptr<T> p) {
            p.Dec();
            return p;
        }

        public static Ptr<T> operator +(Ptr<T> p, IntPtr offset) {
            p.Advance(offset);
            return p;
        }

        public static Ptr<T> operator +(IntPtr offset, Ptr<T> p) {
            p.Advance(offset);
            return p;
        }

        public static Ptr<T> operator -(Ptr<T> p, IntPtr offset) {
            p.Advance((IntPtr)(0 - (ulong)offset));
            return p;
        }

        public static IntPtr operator -(Ptr<T> p, Ptr<T> p2) {
            return p.Diff(p2);
        }

        public T this[IntPtr offset] {
            get { return (this + offset).Deref(); }
            set { (this + offset).DerefWrite(value); }
        }
        #endregion

        #region Comparisons
        public override bool Equals(object obj) {
            return obj is Ptr<T> && this == (Ptr<T>)obj;
        }

        public override int GetHashCode() {
            return (int)_addr ^ (int)(IntPtr)((long)_addr >> 6);
        }

        public static bool operator ==(Ptr<T> p, Ptr<T> p2) {
            return (IntPtr)p == (IntPtr)p2;
        }

        public static bool operator !=(Ptr<T> p, Ptr<T> p2) {
            return (IntPtr)p != (IntPtr)p2;
        }

        public static bool operator <(Ptr<T> p, Ptr<T> p2) {
            return (ulong)(IntPtr)p < (ulong)(IntPtr)p2;
        }

        public static bool operator >(Ptr<T> p, Ptr<T> p2) {
            return (ulong)(IntPtr)p > (ulong)(IntPtr)p2;
        }

        public static bool operator <=(Ptr<T> p, Ptr<T> p2) {
            return (ulong)(IntPtr)p <= (ulong)(IntPtr)p2;
        }

        public static bool operator >=(Ptr<T> p, Ptr<T> p2) {
            return (ulong)(IntPtr)p >= (ulong)(IntPtr)p2;
        }
        #endregion

        #region pointer/int/long arithmetic (convenience)
        public static Ptr<T> operator +(Ptr<T> p, long offset) {
            return p + (IntPtr)offset;
        }

        public static Ptr<T> operator +(long offset, Ptr<T> p) {
            return p + (IntPtr)offset;
        }

        public static Ptr<T> operator -(Ptr<T> p, long offset) {
            return p - (IntPtr)offset;
        }

        public T this[long offset] {
            get { return this[(IntPtr)offset]; }
            set { this[(IntPtr)offset] = value; }
        }
        #endregion
    }

    public struct NativeArray : IDisposable {
        public IntPtr BaseAddr;
        public IntPtr ByteLength;

        public SafeHandle Handle;

        public void Dispose() {
            Handle?.Dispose();
        }

        public static NativeArray<T> FromArray<T>(T[] arr, long start = 0, long count = -1)
                where T : struct {
            if (count < 0) count = arr.LongLength - start;

            var h = new PinnedSafeHandle<T>(arr);
            return new NativeArray<T>(h.Ptr + start, (IntPtr)count, h);
        }
    }

    public struct NativeArray<T> : IReadOnlyList<T>, IDisposable where T : struct {
        internal Ptr<T> BaseAddr;
        internal IntPtr Count;
        internal SafeHandle Handle;

        public static implicit operator NativeArray(NativeArray<T> arr) {
            return new NativeArray {
                BaseAddr = (IntPtr)arr.BaseAddr,
                ByteLength = (IntPtr)((ulong)(IntPtr)(arr.BaseAddr + arr.Count) - (ulong)(IntPtr)arr.BaseAddr),
                Handle = arr.Handle
            };
        }

        internal NativeArray(Ptr<T> baseAddr, IntPtr count, SafeHandle handle) {
            BaseAddr = baseAddr;
            Count = count;
            Handle = handle;
        }

        public static explicit operator NativeArray<T>(NativeArray arr) {
            var baseAddr = (Ptr<T>)arr.BaseAddr;
            var count = (Ptr<T>)(IntPtr)((ulong)arr.BaseAddr + (ulong)arr.ByteLength) - baseAddr;
            return new NativeArray<T>(baseAddr, count, arr.Handle);
        }

        #region IEnumerable
        public IEnumerator<T> GetEnumerator() {
            return new Enumerator(BaseAddr, BaseAddr + Count, Handle);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        private class Enumerator : IEnumerator<T> {
            private readonly Ptr<T> _end;
            private readonly SafeHandle _handle;

            private Ptr<T> _current;
            private int _state;

            public Enumerator(Ptr<T> start, Ptr<T> end, SafeHandle handle) {
                _current = start;
                _end = end;
                _state = 0;
                _handle = handle;
            }

            public void Dispose() {
                GC.KeepAlive(_handle);
            }

            public T Current {
                get {
                    if (_handle != null && _handle.IsClosed) throw new InvalidOperationException("Dereferencing a closed handle");
                    if (_state != 1) throw new InvalidOperationException("Attempt to invoke Current on invalid enumerator");
                    return _current.Deref();
                }
            }

            public bool MoveNext() {
                switch (_state) {
                    case 0:
                        _state = 1;
                        return _current != _end;
                    case 1:
                        ++_current;
                        if (_current == _end) _state = 2;
                        return _current != _end;
                    // case 2:
                    default:
                        return false;
                }
            }

            public void Reset() {
                throw new NotSupportedException();
            }

            object IEnumerator.Current => Current;
        }
        #endregion

        public T this[IntPtr offset] {
            get {
                if ((ulong)offset >= (ulong)Count) throw new IndexOutOfRangeException("offest");
                var val = BaseAddr[offset];
                GC.KeepAlive(this);
                return val;
            }
            set {
                if ((ulong)offset >= (ulong)Count) throw new IndexOutOfRangeException("offest");
                BaseAddr[offset] = value;
                GC.KeepAlive(this);
            }
        }

        public T this[long offset] {
            get { return this[(IntPtr)offset]; }
            set { this[(IntPtr)offset] = value; }
        }

        public void Dispose() {
            Handle?.Dispose();
        }

        int IReadOnlyCollection<T>.Count => (int)Count;

        T IReadOnlyList<T>.this[int index] => this[(IntPtr)index];
    }

    #region Dereferencing abstraction
    internal interface IDeref<T> {
        T Deref(IntPtr addr);
        void DerefWrite(IntPtr addr, T newValue);
    }

    internal unsafe class IntDeref : IDeref<int> {
        public int Deref(IntPtr addr) {
            var p = (int*)addr;
            return *p;
        }

        public void DerefWrite(IntPtr addr, int newValue) {
            var p = (int*)addr;
            *p = newValue;
        }
    }

    internal class MarshalDeref<T> : IDeref<T> {
        public T Deref(IntPtr addr) {
            return (T)Marshal.PtrToStructure(addr, typeof(T));
        }

        public void DerefWrite(IntPtr addr, T newValue) {
            Marshal.StructureToPtr(newValue, addr, false);
        }
    }
    #endregion
}
