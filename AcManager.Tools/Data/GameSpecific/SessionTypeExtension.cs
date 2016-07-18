using System;
using AcTools.Processes;

namespace AcManager.Tools.Data.GameSpecific {
    public static class SessionTypeExtension {
        public static string GetDisplayName(this Game.SessionType type) {
            switch (type) {
                case Game.SessionType.Booking:
                    return Resources.Session_Booking;
                case Game.SessionType.Practice:
                    return Resources.Session_Practice;
                case Game.SessionType.Qualification:
                    return Resources.Session_Qualification;
                case Game.SessionType.Race:
                    return Resources.Session_Race;
                case Game.SessionType.Hotlap:
                    return Resources.Session_Hotlap;
                case Game.SessionType.TimeAttack:
                    return Resources.Session_TimeAttack;
                case Game.SessionType.Drift:
                    return Resources.Session_Drift;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}