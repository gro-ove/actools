using System;
using System.Runtime.InteropServices;

namespace AcManager.Tools.AcPlugins.CspCommands {
    public static class CspCommandsUtils {
        private static byte[] GetBytes<T>(T str) where T : struct, ICspCommand {
            var size = Marshal.SizeOf(str);
            var arr = new byte[size + 2]; // reserving two extra bytes for type
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 2, size); // writing structure with two bytes offset
            Marshal.FreeHGlobal(ptr);
            var typeBytes = BitConverter.GetBytes(str.GetMessageType());
            arr[0] = typeBytes[0]; // copying two bytes
            arr[1] = typeBytes[1]; // there should definitely be a better way, but I can’t remember it at the moment
            return arr;
        }

        public static string Serialize<T>(this T cmd) where T : struct, ICspCommand {
            var bytes = GetBytes(cmd);
            return "\t\t\t\t$CSP0:" + Convert.ToBase64String(bytes).TrimEnd('=');
        }
    }
}