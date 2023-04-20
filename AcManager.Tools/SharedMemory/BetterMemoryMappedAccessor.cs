using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace AcManager.Tools.SharedMemory {
    public class BetterMemoryMappedAccessor<T> : IDisposable {
        private readonly MemoryMappedFile _physicsMmFile;
        private readonly MemoryMappedViewAccessor _physicsView;
        private readonly IntPtr _physicsPointer;

        public unsafe BetterMemoryMappedAccessor(string filename) {
            _physicsMmFile = MemoryMappedFile.OpenExisting(filename);
            _physicsView = _physicsMmFile.CreateViewAccessor();
            var value = (byte*)IntPtr.Zero;
            _physicsView.SafeMemoryMappedViewHandle.AcquirePointer(ref value);
            _physicsPointer = (IntPtr)value;
        }

        public unsafe int GetPacketId() {
            return *(int*)_physicsPointer;
        }

        public T Get() {
            return (T)Marshal.PtrToStructure(_physicsPointer, typeof(T));
        }

        public void Dispose() {
            _physicsMmFile?.Dispose();
            _physicsView?.Dispose();
        }
    }
}