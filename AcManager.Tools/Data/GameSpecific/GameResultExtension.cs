using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Data.GameSpecific {
    /// <summary>
    /// In case some more virtual session types will be added later.
    /// </summary>
    public interface IGameSessionNameProvider {
        [CanBeNull]
        string GetSessionName(Game.ResultSession parsedData);
    }

    public static class GameResultExtension {
        private static List<IGameSessionNameProvider> _nameProviders = new List<IGameSessionNameProvider>();

        public static void RegisterNameProvider(IGameSessionNameProvider provider) {
            _nameProviders.Add(provider);
        }

        [ContractAnnotation("null => null; notnull => notnull")]
        private static string LocalizeSessionName(string name) {
            switch (name?.ToLowerInvariant()) {
                case "practice":
                    return ToolsStrings.Session_Practice;
                case "qualifying":
                    return ToolsStrings.Session_Qualification;
                case "race":
                case "quick race":
                    return ToolsStrings.Session_Race;
                case "track day":
                    return ToolsStrings.Session_TrackDay;
                case "hotlap":
                    return ToolsStrings.Session_Hotlap;
                case "time attack":
                    return ToolsStrings.Session_TimeAttack;
                case "drift session":
                    return ToolsStrings.Session_Drift;
                case "drag":
                case "drag race":
                    return ToolsStrings.Session_Drag;
                default:
                    return name;
            }
        }

        [ContractAnnotation("null => null; notnull => notnull")]
        public static string GetSessionName(this Game.ResultSession parsedData) {
            if (parsedData == null) return null;

            var provided = _nameProviders.Select(x => x.GetSessionName(parsedData)).NonNull().FirstOrDefault();
            if (provided != null) {
                return provided;
            }

            if (parsedData.Name == Game.TrackDaySessionName) {
                return ToolsStrings.Session_TrackDay;
            }

            return parsedData.Type.GetDisplayName() ?? LocalizeSessionName(parsedData.Name);
        }

        [ContractAnnotation("null => false")]
        public static bool IsWeekendSessions(this Game.ResultSession[] sessions) {
            if (sessions == null) return false;
            return sessions.Length == 3 && sessions[0].Type == Game.SessionType.Practice &&
                    sessions[1].Type == Game.SessionType.Qualification && sessions[2].Type == Game.SessionType.Race;
        }
    }
}