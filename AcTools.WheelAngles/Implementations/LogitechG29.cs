using System;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.WheelAngles.Implementations.Options;
using AcTools.Windows;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal abstract class LogitechG29 : LogitechG25 {
        public override string ControllerName => "Logitech G29";

        [NotNull]
        private readonly LogitechG29Options _options;

        public LogitechG29() {
            _options = new LogitechG29Options(OnGameStarted);
        }

        public override WheelOptionsBase GetOptions() {
            return _options;
        }

        public override bool Test(string productGuid) {
            return string.Equals(productGuid, "C24F046D-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        protected override string GetRegistryPath() {
            return null;
        }

        public override bool Apply(int steerLock, bool isReset, out int appliedValue) {
            if (isReset) {
                Reset(() => SetWheelRange(steerLock));
                appliedValue = steerLock;
                return true;
            }

            if (!LoadLogitechSteeringWheelDll()) {
                appliedValue = 0;
                return false;
            }

            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);

            // Actual range will be changed as soon as game is started, we need to wait first.
            // Why not run .Apply() then? Because configs should be prepared BEFORE game is started,
            // and they depend on a steer lock app has set. Of course, it’s not a perfect solution
            // (what if .ApplyLater() will fail?), but it’s the only option I see at the moment.
            _valueToSet = appliedValue;
            return true;
        }

        private int? _valueToSet;
        private static int _applyId;

        private async void OnGameStarted() {
            var id = ++_applyId;

            if (!_valueToSet.HasValue) return;
            var value = _valueToSet.Value;

            var process = AcProcess.TryToFind();
            if (process == null) {
                AcToolsLogging.NonFatalErrorNotifyBackground($"Can’t set {ControllerName} steer lock", "Failed to find game process");
                return;
            }

            IntPtr? initializationHandle;
            if (_options.Handle == LogitechG29HandleOptions.NoHandle) {
                AcToolsLogging.Write("Handle won’t be specified");
                initializationHandle = null;
            } else if (_options.Handle == LogitechG29HandleOptions.MainHandle) {
                AcToolsLogging.Write("Main CM handle will be used");
                initializationHandle = GetMainWindowHandle();
            } else if (_options.Handle == LogitechG29HandleOptions.FakeHandle) {
                AcToolsLogging.Write("Fake CM handle will be used");
                initializationHandle = CreateNewFormForHandle();
            } else if (_options.Handle == LogitechG29HandleOptions.AcHandle) {
                AcToolsLogging.Write("AC handle will be used");
                initializationHandle = process.MainWindowHandle;
            } else {
                AcToolsLogging.Write("Unknown value! Fallback to AC handle");
                initializationHandle = process.MainWindowHandle;
            }

            Initialize(initializationHandle);

            await Task.Delay(500);
            AcToolsLogging.Write("Waited for half a second, moving on…");

            if (_applyId != id) {
                AcToolsLogging.Write("Obsolete run, terminating");
                return;
            }

            SetWheelRange(value);

            var isForeground = true;
            var setIndex = 1;
            var windows = process.GetWindowsHandles().ToArray();

            while (!process.HasExitedSafe()) {
                var isForegroundNow = Array.IndexOf(windows, User32.GetForegroundWindow()) != -1;
                if (isForegroundNow != isForeground) {
                    if (isForegroundNow) {
                        SetWheelRange(value + setIndex);
                        setIndex = 1 - setIndex;
                    }

                    isForeground = isForegroundNow;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private static bool SetValue(int value) {
            var handle = GetMainWindowHandle();
            if (handle == IntPtr.Zero) {
                AcToolsLogging.Write("Main window not found, cancel");
                return false;
            }

            Initialize(handle);
            SetWheelRange(value);
            return true;
        }

        private static void SetWheelRange(int value) {
            for (var i = 0; i < 4; i++) {
                var gotOldValue = LogiGetOperatingRange(i, out var oldValue);
                var result = LogiSetOperatingRange(i, value);
                AcToolsLogging.Write($"Set operating range {value} for #{i}: {result} (old value: {oldValue}; {gotOldValue})");
            }

            Apply();
        }
    }
}