using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class LogitechG29 : LogitechG25 {
        public override string ControllerName => "Logitech G29";

        public override bool Test(string productGuid) {
            return string.Equals(productGuid, "C24F046D-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        public override bool Apply(int steerLock, bool isReset, out int appliedValue) {
            if (!LoadLogitechSteeringWheelDll()) {
                appliedValue = 0;
                return false;
            }

            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);
            return SetValue(appliedValue);
        }

        private static IntPtr GetHandle() {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(() => {
                var mainWindow = Application.Current?.MainWindow;
                return mainWindow == null ? IntPtr.Zero : new WindowInteropHelper(mainWindow).Handle;
            });
        }

        private static bool SetValue(int value) {
            var handle = GetHandle();
            if (handle == IntPtr.Zero) {
                AcToolsLogging.Write("Main window not found, cancel");
                return false;
            }

            Initialize(handle);
            for (var i = 0; i < 4; i++) {
                var result = LogiSetOperatingRange(i, value);
                AcToolsLogging.Write($"Set operating range for #{i}: {result}");
            }

            Apply();
            return true;
        }

        [DllImport(LogitechSteeringWheel, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool LogiSetOperatingRange(int index, int range);
    }
}