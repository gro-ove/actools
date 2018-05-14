using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public static class ScreenInterrogatory {
        private const int ErrorSuccess = 0;

        #region Enums
        private enum QueryDeviceConfigFlags : uint {
            QdcAllPaths = 0x00000001,
            QdcOnlyActivePaths = 0x00000002,
            QdcDatabaseCurrent = 0x00000004
        }

        private enum DisplayConfigVideoOutputTechnology : uint {
            Other = 0xFFFFFFFF,
            Hd15 = 0,
            Svideo = 1,
            CompositeVideo = 2,
            ComponentVideo = 3,
            Dvi = 4,
            Hdmi = 5,
            Lvds = 6,
            DJpn = 8,
            Sdi = 9,
            DisplayPortExternal = 10,
            DisplayPortEmbedded = 11,
            UdiExternal = 12,
            UdiEmbedded = 13,
            Sdtvdongle = 14,
            Miracast = 15,
            Internal = 0x80000000,
            ForceUint32 = 0xFFFFFFFF
        }

        private enum DisplayConfigScanlineOrdering : uint {
            Unspecified = 0,
            Progressive = 1,
            Interlaced = 2,
            InterlacedUpperfieldFirst = Interlaced,
            InterlacedLowerfieldFirst = 3,
            ForceUint32 = 0xFFFFFFFF
        }

        private enum DisplayConfigRotation : uint {
            Identity = 1,
            Rotate90 = 2,
            Rotate180 = 3,
            Rotate270 = 4,
            ForceUint32 = 0xFFFFFFFF
        }

        private enum DisplayConfigScaling : uint {
            Identity = 1,
            Centered = 2,
            Stretched = 3,
            AspectRatioCenteredMax = 4,
            Custom = 5,
            Preferred = 128,
            ForceUint32 = 0xFFFFFFFF
        }

        private enum DisplayConfigPixelformat : uint {
            Format8Bpp = 1,
            Format16Bpp = 2,
            Format24Bpp = 3,
            Format32Bpp = 4,
            FormatNongdi = 5,
            FormatForceUint32 = 0xffffffff
        }

        private enum DisplayConfigModeInfoType : uint {
            Source = 1,
            Target = 2,
            ForceUint32 = 0xFFFFFFFF
        }

        private enum DisplayConfigDeviceInfoType : uint {
            GetSourceName = 1,
            GetTargetName = 2,
            GetTargetPreferredMode = 3,
            GetAdapterName = 4,
            SetTargetPersistence = 5,
            GetTargetBaseType = 6,
            ForceUint32 = 0xFFFFFFFF
        }
        #endregion

        #region structs
        [StructLayout(LayoutKind.Sequential)]
        private struct Luid {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathSourceInfo {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathTargetInfo {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIdx;
            private DisplayConfigVideoOutputTechnology OutputTechnology;
            private DisplayConfigRotation Rotation;
            private DisplayConfigScaling Scaling;
            private DisplayConfigRational RefreshRate;
            private DisplayConfigScanlineOrdering ScanLineOrdering;
            public bool TargetAvailable;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigRational {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathInfo {
            public DisplayConfigPathSourceInfo SourceInfo;
            public DisplayConfigPathTargetInfo TargetInfo;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfig2DRegion {
            public uint Cx;
            public uint Cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigVideoSignalInfo {
            public ulong PixelRate;
            public DisplayConfigRational HSyncFreq;
            public DisplayConfigRational VSyncFreq;
            public DisplayConfig2DRegion ActiveSize;
            public DisplayConfig2DRegion TotalSize;
            public uint VideoStandard;
            public DisplayConfigScanlineOrdering ScanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigTargetMode {
            public DisplayConfigVideoSignalInfo targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Pointl {
            private int X;
            private int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigSourceMode {
            public uint Width;
            public uint Height;
            public DisplayConfigPixelformat PixelFormat;
            public Pointl Position;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DisplayConfigModeInfoUnion {
            [FieldOffset(0)]
            public DisplayConfigTargetMode targetMode;

            [FieldOffset(0)]
            public DisplayConfigSourceMode sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigModeInfo {
            public DisplayConfigModeInfoType infoType;
            public uint id;
            public Luid adapterId;
            public DisplayConfigModeInfoUnion modeInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigTargetDeviceNameFlags {
            public uint Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigDeviceInfoHeader {
            public DisplayConfigDeviceInfoType Type;
            public uint Size;
            public Luid AdapterId;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayConfigTargetDeviceName {
            public DisplayConfigDeviceInfoHeader Header;
            public DisplayConfigTargetDeviceNameFlags Flags;
            public DisplayConfigVideoOutputTechnology OutputTechnology;
            public ushort EdIdManufactureId;
            public ushort EdIdProductCodeId;
            public uint ConnectorInstance;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string MonitorFriendlyDeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string MonitorDevicePath;
        }
        #endregion

        #region DLL-Imports
        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
                QueryDeviceConfigFlags flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
                QueryDeviceConfigFlags flags,
                ref uint numPathArrayElements, [Out] DisplayConfigPathInfo[] pathInfoArray,
                ref uint numModeInfoArrayElements, [Out] DisplayConfigModeInfo[] modeInfoArray,
                IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName deviceName);
        #endregion

        private static string MonitorFriendlyName(Luid adapterId, uint targetId) {
            var deviceName = new DisplayConfigTargetDeviceName {
                Header = {
                    Size = (uint)Marshal.SizeOf(typeof(DisplayConfigTargetDeviceName)),
                    AdapterId = adapterId,
                    Id = targetId,
                    Type = DisplayConfigDeviceInfoType.GetTargetName
                }
            };
            var error = DisplayConfigGetDeviceInfo(ref deviceName);
            if (error != ErrorSuccess) {
                throw new Win32Exception(error);
            }

            return deviceName.MonitorFriendlyDeviceName;
        }

        private static IEnumerable<string> GetAllMonitorsFriendlyNames() {
            var error = GetDisplayConfigBufferSizes(QueryDeviceConfigFlags.QdcOnlyActivePaths, out var pathCount, out var modeCount);
            if (error != ErrorSuccess) {
                throw new Win32Exception(error);
            }

            var displayPaths = new DisplayConfigPathInfo[pathCount];
            var displayModes = new DisplayConfigModeInfo[modeCount];
            error = QueryDisplayConfig(QueryDeviceConfigFlags.QdcOnlyActivePaths,
                    ref pathCount, displayPaths, ref modeCount, displayModes, IntPtr.Zero);
            if (error != ErrorSuccess) {
                throw new Win32Exception(error);
            }

            for (var i = 0; i < modeCount; i++) {
                if (displayModes[i].infoType == DisplayConfigModeInfoType.Target) {
                    yield return MonitorFriendlyName(displayModes[i].adapterId, displayModes[i].id);
                }
            }
        }

        [CanBeNull]
        public static string GetFriendlyName([NotNull] this Screen screen) {
            try {
                var allFriendlyNames = GetAllMonitorsFriendlyNames();
                for (var index = 0; index < Screen.AllScreens.Length; index++) {
                    if (Equals(screen, Screen.AllScreens[index])) {
                        return allFriendlyNames.ToArray()[index];
                    }
                }
            } catch (Exception e) {
                Logging.Warning($"Can’t get friendly name for {screen}: {e}");
            }
            return null;
        }

        [CanBeNull]
        public static Dictionary<Screen, string> GetFriendlyNames() {
            try {
                return GetAllMonitorsFriendlyNames().Zip(Screen.AllScreens, (x, y) => Tuple.Create(y, x)).ToDictionary(x => x.Item1, x => x.Item2);
            } catch (Exception e) {
                Logging.Warning($"Can’t get friendly name: {e}");
            }
            return null;
        }
    }
}