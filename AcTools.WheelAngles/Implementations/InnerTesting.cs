using System;
using System.Linq;
using System.Runtime.InteropServices;
using AcTools.Utils.Helpers;
using HidLibrary;
using JetBrains.Annotations;

namespace AcTools.WheelAngles.Implementations {
    [UsedImplicitly]
    internal class InnerTesting : IWheelSteerLockSetter {
        private static int[] KnownDeviceIDs = {
            0x1, 0x4, 0x5, 0x6, 0x7, 0x20, 0x11, 0xE03,
            0x112, 0x280B, 0x1102, 0x280A, 0x50C
        };

        public virtual string ControllerName => "InnerTesting";

        public WheelOptionsBase GetOptions() {
            return null;
        }

        public virtual IWheelSteerLockSetter Test(string productGuid) {
            if (string.Equals(productGuid.Substring(4), "0EB7-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase)) {
                AcToolsLogging.Write("F. device detected");

                var isSupported = KnownDeviceIDs.Any(x => string.Equals(productGuid.Substring(0, 4), x.ToString("X4")));
                AcToolsLogging.Write("F. device supported: " + isSupported);
                return isSupported ? this : null;
            }
            return null;
            // return string.Equals(productGuid.Substring(4), "0EB7-0000-0000-0000-504944564944", StringComparison.OrdinalIgnoreCase)
            //        && KnownDeviceIDs.Any(x => string.Equals(productGuid.Substring(0, 4), x.ToString("X4")));
        }

        public int MaximumSteerLock => 2520;
        public int MinimumSteerLock => 40;

        public bool Apply(int steerLock, bool isReset, out int appliedValue) {
            appliedValue = Math.Min(Math.Max(steerLock, MinimumSteerLock), MaximumSteerLock);
            var cmd0 = new byte[] { 0x1, 0xf5, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };
            var cmd1 = new byte[] { 0x1, 0xf8, 0x09, 0x01, 0x06, 0x01, 0x0, 0x0 };
            var cmd2 = new byte[] { 0x1, 0xf8, 0x81 }
                    .Concat(BitConverter.GetBytes((ushort)appliedValue))
                    .Concat(new byte[] { 0x0, 0x0, 0x0 }).ToArray();
            var cmdE = new byte[] { 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };
            return HidDevices.Enumerate(0xEB7, KnownDeviceIDs).Aggregate(false, (a, b) => {
                using (b) {
                    try {
                        AcToolsLogging.Write($"Set to {steerLock}: " + b.DevicePath);
                        AcToolsLogging.Write($"Device state: connected={b.IsConnected}, description={b.Description}");
                        AcToolsLogging.Write(
                                $"Device caps: Usage={b.Capabilities.Usage}, UsagePage={b.Capabilities.UsagePage}, FeatureReportByteLength={b.Capabilities.FeatureReportByteLength}");
                        AcToolsLogging.Write(
                                $"Device caps: OutputReportByteLength={b.Capabilities.OutputReportByteLength}, InputReportByteLength={b.Capabilities.InputReportByteLength}");
                        AcToolsLogging.Write(
                                $"Device caps: NumberLinkCollectionNodes={b.Capabilities.NumberLinkCollectionNodes}, NumberInputButtonCaps={b.Capabilities.NumberInputButtonCaps}");
                        AcToolsLogging.Write(
                                $"Device caps: NumberInputValueCaps={b.Capabilities.NumberInputValueCaps}, NumberInputDataIndices={b.Capabilities.NumberInputDataIndices}");
                        AcToolsLogging.Write(
                                $"Device caps: NumberOutputButtonCaps={b.Capabilities.NumberOutputButtonCaps}, NumberOutputValueCaps={b.Capabilities.NumberOutputValueCaps}");
                        AcToolsLogging.Write(
                                $"Device caps: NumberOutputDataIndices={b.Capabilities.NumberOutputDataIndices}, NumberFeatureButtonCaps={b.Capabilities.NumberFeatureButtonCaps}");
                        AcToolsLogging.Write(
                                $"Device caps: NumberFeatureValueCaps={b.Capabilities.NumberFeatureValueCaps}, NumberFeatureDataIndices={b.Capabilities.NumberFeatureDataIndices}");
                        if (b.Capabilities.Usage != 4) {
                            AcToolsLogging.Write("Unknown usage for setting a lock");
                            // return a;
                        }
                        b.OpenDevice();
                        AcToolsLogging.Write($"Device state: opened={b.IsOpen}, handle={b.WriteHandle}, read={b.ReadHandle}");
                        if (b.WriteDataThrowy(cmd0, 0)) {
                            AcToolsLogging.Write($"First command sent: {cmd0.Select(x => x.ToString("X2")).JoinToString(" ")}");
                            AcToolsLogging.Write($"Device state post-writing first command: opened={b.IsOpen}, handle={b.WriteHandle}");
                            if (b.WriteDataThrowy(cmd1, 0)) {
                                AcToolsLogging.Write($"Second command sent: {cmd1.Select(x => x.ToString("X2")).JoinToString(" ")}");
                                if (b.WriteDataThrowy(cmd2, 0)) {
                                    AcToolsLogging.Write($"Third command sent: {cmd2.Select(x => x.ToString("X2")).JoinToString(" ")}");
                                    if (b.Write(cmd2)) {
                                        AcToolsLogging.Write($"Empty command sent: {cmdE.Select(x => x.ToString("X2")).JoinToString(" ")}");
                                        AcToolsLogging.Write($"Device state post-writing: opened={b.IsOpen}, handle={b.WriteHandle}");
                                        b.CloseDevice();
                                        return true;
                                    }
                                }
                            }
                        } else {
                            AcToolsLogging.Write($"Failed to sent even the first command: {Marshal.GetLastWin32Error()}");
                        }
                        AcToolsLogging.Write($"Device state post-writing: opened={b.IsOpen}, handle={b.WriteHandle}");
                    } catch (Exception e) {
                        AcToolsLogging.Write($"Error: {e}");
                    }
                }
                return a;
            });
        }
    }
}