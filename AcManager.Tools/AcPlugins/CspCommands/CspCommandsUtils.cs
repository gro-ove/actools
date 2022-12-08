using System;
using System.Linq;
using System.Runtime.InteropServices;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcPlugins.CspCommands {
    public static class CspCommandsUtils {
        private static byte[] GetBytes<T>(T str) where T : struct, ICspCommand {
            var size = Marshal.SizeOf(str);
            var arr = new byte[size + 2]; // reserving two extra bytes for type
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 2, size); // writing structure with two bytes offset
            Marshal.FreeHGlobal(ptr);
            var typeBytes = BitConverter.GetBytes(str.GetMessageType());
            arr[0] = typeBytes[0]; // copying two bytes
            arr[1] = typeBytes[1]; // there should definitely be a better way, but I can’t remember it at the moment

            if (typeof(ICspVariableCommand).IsAssignableFrom(typeof(T))) {
                var i = arr.Length;
                for (; i > 0 && arr[i - 1] == 0; --i) { }
                if (i != arr.Length) {
                    arr = arr.Slice(0, i);
                }
            }
            
            return arr;
        }

        public static string Serialize<T>(this T cmd) where T : struct, ICspCommand {
            var bytes = GetBytes(cmd);
            return "\t\t\t\t$CSP0:" + Convert.ToBase64String(bytes).TrimEnd('=');
        }

        public class PreparsedMessage {
            public ushort Key;
            public byte[] Data;

            public bool TryParse<T>(out T ret) where T : struct, ICspCommand {
                var size = Marshal.SizeOf<T>();
                var arr = Data.Slice(2, Data.Length - 2);
                
                if (typeof(ICspVariableCommand).IsAssignableFrom(typeof(T))) {
                    if (arr.Length > size) {
                        ret = new T();
                        return false;
                    }
                    arr = arr.Length < size ? arr.Concat(new byte[size - arr.Length]).ToArray() : arr;
                }
                
                var handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
                ret = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                handle.Free();
                return true;
            }
        }

        [CanBeNull]
        public static PreparsedMessage TryDeserialize(string cspChatMessage) {
            if (!cspChatMessage.StartsWith("\t\t\t\t$CSP0:")) return null;
            var data = cspChatMessage.Substring("\t\t\t\t$CSP0:".Length).FromCutBase64();
            if (data == null) return null;
            var id = BitConverter.ToUInt16(data, 0);
            return new PreparsedMessage { Key = id, Data = data };
        }

        public static bool IsMessage(string cspChatMessage) {
            return cspChatMessage.StartsWith("\t\t\t\t$CSP0:");
        }
    }
}