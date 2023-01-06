using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Xml.Linq;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.WheelAngles.Implementations.Options;
using AcTools.WheelAngles.Utils;
using AcTools.Windows;
using JetBrains.Annotations;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class LogitechG25 : IWheelSteerLockSetter {
        public virtual string ControllerName => "Logitech G25";

        [NotNull]
        private readonly LogitechOptions _options;

        public LogitechG25() {
            _options = new LogitechOptions(OnGameStarted);
        }

        public virtual WheelOptionsBase GetOptions() {
            return _options;
        }

        public virtual bool Test(string productGuid) {
            return string.Equals(productGuid, "C299046D-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase);
        }

        public int MaximumSteerLock => 900;
        public int MinimumSteerLock => 40;

        public virtual bool Apply(int steerLock, bool isReset, out int appliedValue) {
            if (isReset) {
                Reset(() => SetWheelRange(steerLock));

                // Don’t need to reset, Logitech does that for you as soon as AC is closed. Now, that’s neat.
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

        #region SDK-related stuff
        private static int _applyId;

        private sealed class FormWithHandle : Form {
            public FormWithHandle() {
                CreateHandle();
            }

            public new void DestroyHandle() {
                base.DestroyHandle();
            }
        }

        protected static IntPtr CreateNewFormForHandle() {
            AcToolsLogging.Write("Creating new form to get its handle…");
            Application.Current.Dispatcher?.Invoke(() => { _form2 = new FormWithHandle(); });
            return _form2.Handle;
        }

        protected static IntPtr GetMainWindowHandle() {
            return (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(() => {
                var mainWindow = Application.Current?.MainWindow;
                return mainWindow == null ? IntPtr.Zero : new WindowInteropHelper(mainWindow).Handle;
            });
        }

        private static FormWithHandle _form2;

        protected static void Reset(Action callback) {
            if (_initialized) {
                _initialized = false;
                callback?.Invoke();
                LogiSteeringShutdown();
                AcToolsLogging.Write("Previous initialization shutdown");
            }

            if (_form2 != null) {
                _form2.DestroyHandle();
                _form2 = null;
            }
        }

        private int? _valueToSet;

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
            if (!_options.SpecifyHandle) {
                AcToolsLogging.Write("Handle won’t be specified");
                initializationHandle = null;
            } else if (_options.UseOwnHandle) {
                initializationHandle = CreateNewFormForHandle();
            } else {
                AcToolsLogging.Write("AC handle will be used");
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

        [NotNull]
        protected virtual string GetRegistryPath() {
            return @"Software\Logitech\Gaming Software\GlobalDeviceSettings\G25";
        }

        [CanBeNull]
        protected virtual string GetExeFileName() {
            return AcProcess.TryToFind()?.GetFilenameSafe();
        }

        private LogiControllerPropertiesData FromOptions() {
            var options = _options;
            return new LogiControllerPropertiesData {
                ForceFeedbackEnable = options.ForceFeedbackEnable,
                OverallGainPercentage = options.OverallGainPercentage,
                SpringGainPercentage = options.SpringGainPercentage,
                DamperGainPercentage = options.DamperGainPercentage,
                PersistentSpringEnable = options.PersistentSpringEnable,
                DefaultSpringGainPercentage = options.DefaultSpringGainPercentage,
                CombinedPedalsEnable = options.CombinedPedalsEnable,
                GameSettingsEnable = options.GameSettingsEnable,
                AllowGameSettings = options.AllowGameSettings
            };
        }

        private void SetWheelRange(int value) {
            var properties = _options.DetectSettingsAutomatically
                    ? LogiControllerPropertiesData.Load(GetRegistryPath(), GetExeFileName())
                    : FromOptions();
            properties.OperatingRange = value;
            AcToolsLogging.Write(properties);
            Log(LogiSetPreferredControllerProperties(properties));
            Apply();
        }

        private static void Log(bool value, [CallerMemberName] string m = null, [CallerLineNumber] int l = -1) {
            AcToolsLogging.Write($"Result [{m}:{l}]: {value}");
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct LogiControllerPropertiesData {
            public bool ForceFeedbackEnable;
            public int OverallGainPercentage;
            public int SpringGainPercentage;
            public int DamperGainPercentage;
            public bool PersistentSpringEnable;
            public int DefaultSpringGainPercentage;
            public bool CombinedPedalsEnable;
            public int OperatingRange;
            public bool GameSettingsEnable;
            public bool AllowGameSettings;

            public override string ToString() {
                return
                        $"(Degrees={OperatingRange}; FFB={ForceFeedbackEnable}; Overall={OverallGainPercentage}; Spring={SpringGainPercentage}; Damper={DamperGainPercentage}; DefaultSpringEnabled={PersistentSpringEnable}; DefaultSpring={DefaultSpringGainPercentage}; CombinedPedals={CombinedPedalsEnable}; GameSettingsEnable={GameSettingsEnable}; AllowGameSettings={AllowGameSettings})";
            }

            private static LogiControllerPropertiesData CreateNew() {
                return new LogiControllerPropertiesData {
                    ForceFeedbackEnable = true,
                    OverallGainPercentage = 100,
                    SpringGainPercentage = 0,
                    DamperGainPercentage = 30,
                    PersistentSpringEnable = true,
                    DefaultSpringGainPercentage = 0,
                    CombinedPedalsEnable = false,
                    OperatingRange = 900,
                    GameSettingsEnable = false,
                    AllowGameSettings = false
                };
            }

            private void ExtendWithPresets([NotNull] string processFilename) {
                if (processFilename == null) {
                    throw new ArgumentNullException(nameof(processFilename));
                }

                var presets = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Logitech", "Gaming Software");
                if (!Directory.Exists(presets)) return;

                var ns = XNamespace.Get("http://www.logitech.com/schemas/2009/gaming/game_profile");
                var specialOptions = Directory.GetFiles(presets, "*.xml").TryToSelect(XDocument.Load)
                        .FirstOrDefault(x => FileUtils.ArePathsEqual(
                                x?.Descendants(ns + "Target").FirstOrDefault()?.Element(ns + "Path")?.Value ?? "",
                                processFilename))?.Descendants(ns + "SpecialOptions")
                        .FirstOrDefault();

                var forceOptions = specialOptions?.Element(ns + "ForceOptions");
                if (forceOptions != null && (forceOptions.Attribute("Enable")?.Value).As<bool>()) {
                    OverallGainPercentage = (forceOptions.Attribute("OverallAttenuation")?.Value).As(OverallGainPercentage);
                    SpringGainPercentage = (forceOptions.Attribute("SpringAttenuation")?.Value).As(SpringGainPercentage);
                    DamperGainPercentage = (forceOptions.Attribute("DamperAttenuation")?.Value).As(DamperGainPercentage);
                    PersistentSpringEnable = (forceOptions.Attribute("DefaultSpringEnabled")?.Value).As(PersistentSpringEnable);
                    DefaultSpringGainPercentage = (forceOptions.Attribute("DefaultSpringAttenuation")?.Value).As(DefaultSpringGainPercentage);
                }

                var wheelOptions = specialOptions?.Element(ns + "WheelOptions");
                if (wheelOptions != null && (wheelOptions.Attribute("Enable")?.Value).As<bool>()) {
                    OperatingRange = (wheelOptions.Attribute("OperatingRange")?.Value).As(OperatingRange);
                    CombinedPedalsEnable = (wheelOptions.Attribute("CombinePedals")?.Value).As(CombinedPedalsEnable);
                }

                var gameOptions = specialOptions?.Element(ns + "GameOptions");
                if (gameOptions != null && (gameOptions.Attribute("Enable")?.Value).As<bool>()) {
                    AllowGameSettings = (gameOptions.Attribute("AllowGameSettings")?.Value).As(AllowGameSettings);
                }
            }

            public static LogiControllerPropertiesData Load([NotNull] string registryPath, [CanBeNull] string processFilename) {
                using (var regKey = Registry.CurrentUser.OpenSubKey(registryPath)) {
                    object o = CreateNew();
                    if (regKey != null) {
                        foreach (var f in typeof(LogiControllerPropertiesData).GetFields()) {
                            f.SetValue(o, regKey.GetValue(f.Name).As(f.FieldType, f.GetValue(o)));
                        }
                    } else {
                        AcToolsLogging.Write("Settings in registry not found");
                    }

                    var result = (LogiControllerPropertiesData)o;
                    if (processFilename != null) {
                        result.ExtendWithPresets(processFilename);
                    }

                    return result;
                }
            }
        }
        #endregion

        #region Global SDK-related stuff
        private const string LogitechSteeringWheelOld = "LogitechSteeringWheel.dll";
        private const string LogitechSteeringWheelNew = "sdk_legacy_steering_wheel_x86.dll";
        private static bool? _logitechDllInitialized;
        private static bool _logitechDllUseOld;

        private static string LocateLogitechSteeringWheelDll() {
            // For 32-bit apps

            var programFiles = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).Replace(" (x86)", ""),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            }.Distinct().Where(Directory.Exists).ToList();

            var lgHub = programFiles
                    .SelectMany(x => CombineWith(x, Path.Combine("LGHUB", "sdks", "sdk_legacy_steering_wheel_x86.dll"))).FirstOrDefault(File.Exists);
            if (lgHub != null) {
                return lgHub;
            }

            return programFiles
                    .SelectMany(x => CombineWith(x, Path.Combine("Logitech", "Gaming Software"), "Logitech Gaming Software")).Where(Directory.Exists)
                    .SelectMany(x => CombineWith(x, "SDKs", "SDK")).Where(Directory.Exists)
                    .SelectMany(x => CombineWith(x, null, "SteeringWheel")).Where(Directory.Exists)
                    .SelectMany(x => CombineWith(x, "32", "x86", "64", "x64", null)).Where(Directory.Exists)
                    .SelectMany(x => CombineWith(x, "LogitechSteeringWheel.dll")).FirstOrDefault(File.Exists);

            IEnumerable<string> CombineWith(string s, params string[] extra) {
                return extra.Select(x => x == null ? s : Path.Combine(s, x));
            }
        }

        protected static bool LoadLogitechSteeringWheelDll() {
            if (_logitechDllInitialized.HasValue) return _logitechDllInitialized.Value;

            // Library is in PATH, next to executable, somewhere in system or in a list of libraries to load, nice.
            try {
                if (Kernel32.LoadLibrary(LogitechSteeringWheelOld) != IntPtr.Zero) {
                    _logitechDllUseOld = true;
                    return (_logitechDllInitialized = true).Value;
                }
            } catch (Exception e) {
                AcToolsLogging.Write($"Failed to load: {e.Message}");
            }

            // Library is in PATH, next to executable, somewhere in system or in a list of libraries to load, nice.
            try {
                if (Kernel32.LoadLibrary(LogitechSteeringWheelNew) != IntPtr.Zero) {
                    _logitechDllUseOld = false;
                    return (_logitechDllInitialized = true).Value;
                }
            } catch (Exception e) {
                AcToolsLogging.Write($"Failed to load: {e.Message}");
            }

            // Trying to find the library in Logitech Gaming Software installation…
            AcToolsLogging.Write($"Trying to locate {LogitechSteeringWheelOld}…");

            var location = LocateLogitechSteeringWheelDll();
            if (location != null) {
                AcToolsLogging.Write($"Found: {location}");
                try {
                    Kernel32.LoadLibrary(location);
                    _logitechDllUseOld = location.EndsWith(LogitechSteeringWheelOld, StringComparison.OrdinalIgnoreCase);
                    return (_logitechDllInitialized = true).Value;
                } catch (Exception e) {
                    AcToolsLogging.Write($"Failed to load: {e.Message}");
                }
            }

            AcToolsLogging.NonFatalErrorNotifyBackground($"Failed to find “{LogitechSteeringWheelOld}”",
                    "Please, make sure you have Logitech Gaming Software installed, or simply put that library next to executable.");
            return (_logitechDllInitialized = false).Value;
        }

        protected static void Apply() {
            AcToolsLogging.Write("Applying…");
            Log(LogiUpdate());
        }

        private static bool _initialized;

        protected static void Initialize(IntPtr? mainWindowHandle) {
            if (_initialized) {
                AcToolsLogging.Write("Previous initialization shutdown…");
                Log(LogiSteeringShutdown());
            }

            _initialized = true;
            Log(mainWindowHandle.HasValue ? LogiSteeringInitializeWithWindow(false, mainWindowHandle.Value) : LogiSteeringInitialize(false));
        }

        private static bool LogiSteeringInitialize(bool ignoreXInputControllers) {
            return _logitechDllUseOld
                    ? SdkOld.LogiSteeringInitialize(ignoreXInputControllers)
                    : SdkNew.LogiSteeringInitialize(ignoreXInputControllers);
        }

        protected static bool LogiSetOperatingRange(int index, int range) {
            return _logitechDllUseOld
                    ? SdkOld.LogiSetOperatingRange(index, range)
                    : SdkNew.LogiSetOperatingRange(index, range);
        }

        private static bool LogiSteeringInitializeWithWindow(bool ignoreXInputControllers, IntPtr windowHandle) {
            return _logitechDllUseOld
                    ? SdkOld.LogiSteeringInitializeWithWindow(ignoreXInputControllers, windowHandle)
                    : SdkNew.LogiSteeringInitializeWithWindow(ignoreXInputControllers, windowHandle);
        }

        private static bool LogiSteeringShutdown() {
            return _logitechDllUseOld
                    ? SdkOld.LogiSteeringShutdown()
                    : SdkNew.LogiSteeringShutdown();
        }

        private static bool LogiUpdate() {
            return _logitechDllUseOld
                    ? SdkOld.LogiUpdate()
                    : SdkNew.LogiUpdate();
        }

        private static bool LogiSetPreferredControllerProperties(LogiControllerPropertiesData properties) {
            return _logitechDllUseOld
                    ? SdkOld.LogiSetPreferredControllerProperties(properties)
                    : SdkNew.LogiSetPreferredControllerProperties(properties);
        }

        private static bool LogiGetCurrentControllerProperties(int index, ref LogiControllerPropertiesData properties) {
            return _logitechDllUseOld
                    ? SdkOld.LogiGetCurrentControllerProperties(index, ref properties)
                    : SdkNew.LogiGetCurrentControllerProperties(index, ref properties);
        }

        private static class SdkOld {
            [DllImport(LogitechSteeringWheelOld, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSteeringInitialize(bool ignoreXInputControllers);

            [DllImport(LogitechSteeringWheelOld, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSetOperatingRange(int index, int range);

            [DllImport(LogitechSteeringWheelOld, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSteeringInitializeWithWindow(bool ignoreXInputControllers, IntPtr windowHandle);

            [DllImport(LogitechSteeringWheelOld, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSteeringShutdown();

            [DllImport(LogitechSteeringWheelOld, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiUpdate();

            [DllImport(LogitechSteeringWheelOld, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSetPreferredControllerProperties(LogiControllerPropertiesData properties);

            [DllImport(LogitechSteeringWheelOld, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiGetCurrentControllerProperties(int index, ref LogiControllerPropertiesData properties);
        }

        private static class SdkNew {
            [DllImport(LogitechSteeringWheelNew, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSteeringInitialize(bool ignoreXInputControllers);

            [DllImport(LogitechSteeringWheelNew, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSetOperatingRange(int index, int range);

            [DllImport(LogitechSteeringWheelNew, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSteeringInitializeWithWindow(bool ignoreXInputControllers, IntPtr windowHandle);

            [DllImport(LogitechSteeringWheelNew, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSteeringShutdown();

            [DllImport(LogitechSteeringWheelNew, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiUpdate();

            [DllImport(LogitechSteeringWheelNew, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSetPreferredControllerProperties(LogiControllerPropertiesData properties);

            [DllImport(LogitechSteeringWheelNew, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiGetCurrentControllerProperties(int index, ref LogiControllerPropertiesData properties);
        }
        #endregion
    }
}