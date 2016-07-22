using System;
using AcTools.Processes;

namespace AcManager.Tools.Data.GameSpecific {
    public static class SessionTypeExtension {
        public static string GetDisplayName(this Game.SessionType type) {
            switch (type) {
                case Game.SessionType.Booking:
                    return ToolsStrings.Session_Booking;
                case Game.SessionType.Practice:
                    return ToolsStrings.Session_Practice;
                case Game.SessionType.Qualification:
                    return ToolsStrings.Session_Qualification;
                case Game.SessionType.Race:
                    return ToolsStrings.Session_Race;
                case Game.SessionType.Hotlap:
                    return ToolsStrings.Session_Hotlap;
                case Game.SessionType.TimeAttack:
                    return ToolsStrings.Session_TimeAttack;
                case Game.SessionType.Drift:
                    return ToolsStrings.Session_Drift;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}