using System.Runtime.InteropServices;

namespace AcManager.Tools.AcPlugins.CspCommands {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommandWeatherSetV1 : ICspCommand {
        /**
         * Timestamp in unix format (number of seconds since 1/1/1970).
         */
        public ulong Timestamp;

        /**
         * Current weather type.
         */
        public CommandWeatherType WeatherCurrent;

        /**
         * Upcoming weather type. Please note: after finishing transition from A to B, next pair should be Current=B and Next=C. If
         * both current and next weather would change to something new, transition will not be smooth.
         */
        public CommandWeatherType WeatherNext;

        /**
         * Transition between current and next weather, from 0 to 1. For smoother transition might make sense to apply some sort of
         * smoothstep() function instead of sending linear value.
         */
        public float Transition;

        /**
         * If non-zero, upon receiving package CSP will apply conditions smoothly, using this time (in seconds) for transition. Best to
         * use the same value as your update period.
         * For sharp change (let’s say, upon session reset or switch) set to 0.
         */
        public float TimeToApply;

        ushort ICspCommand.GetMessageType() {
            return 1000;
        }
    }
}