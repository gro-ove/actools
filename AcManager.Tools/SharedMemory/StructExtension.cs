using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using AcManager.Tools.AcPlugins.Helpers;
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

        [Pure, NotNull]
        public static T ToStruct<T>(this TimestampedBytes timestampedBytes) {
            var handle = GCHandle.Alloc(timestampedBytes.RawData, GCHandleType.Pinned);
            var data = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return data;
        }

        [Pure, NotNull]
        public static TimestampedBytes ToTimestampedBytes<T>(this T s, byte[] buffer) where T : struct {
            var ptr = Marshal.AllocHGlobal(buffer.Length);
            Marshal.StructureToPtr(s, ptr, false);
            Marshal.Copy(ptr, buffer, 0, buffer.Length);
            Marshal.FreeHGlobal(ptr);
            return new TimestampedBytes(buffer, DateTime.Now);
        }
    }
}