using System;
using System.Runtime.InteropServices;
using AcTools.Utils;
using SystemHalf;

namespace AcManager.Tools.AcPlugins.CspCommands {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandWeatherSetV3 : ICspCommand {
        public ulong Timestamp;
        public CommandWeatherType WeatherCurrent;
        public CommandWeatherType WeatherNext;
        public ushort Transition;
        public Half TimeToApply;
        public Half TemperatureAmbient, TemperatureRoad;
        public byte TrackGripEnc;
        public byte Humidity;
        public Half WindDirectionDeg, WindSpeedKmh;
        public Half Pressure;
        public Half RainIntensity, RainWetness, RainWater;

        public double TrackGrip {
            set => TrackGripEnc = (byte)Math.Round(value.LerpInvSat(0.6, 1) * 255d);
        }

        ushort ICspCommand.GetMessageType() {
            return 1002;
        }
    }
}