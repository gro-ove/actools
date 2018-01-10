using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AcManager.Tools.SharedMemory {
    internal static class StructExtension {
        [Pure, NotNull]
        public static T ToStruct<T>(this MemoryMappedFile file, byte[] buffer) {
            using (var stream = file.CreateViewStream()) {
                stream.Read(buffer, 0, buffer.Length);
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                var data = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                handle.Free();
                return data;
            }
        }
    }
}