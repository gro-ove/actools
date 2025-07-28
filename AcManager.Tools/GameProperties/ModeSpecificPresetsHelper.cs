using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.PresetsPerMode;
using AcManager.Tools.Miscellaneous;
using AcTools;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using MoonSharp.Interpreter;
using Newtonsoft.Json;

namespace AcManager.Tools.GameProperties {
    public class ModeSpecificPresetsHelper : Game.RaceIniProperties, IDisposable {
        [JsonObject(MemberSerialization.OptIn)]
        public sealed class Mode : Displayable, IWithId {
            public string Id { get; }

            public string Script { get; }

            [JsonConstructor]
            public Mode(string id, string name, string script) {
                Id = id;
                DisplayName = name;
                Script = script;
            }

            [JsonProperty("group")]
            public string Category { get; set; }

            [JsonProperty("required")]
            public string Required { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("param")]
            public string Param { get; set; }

            private bool Equals(Mode other) {
                return string.Equals(Id, other.Id) && string.Equals(DisplayName, other.DisplayName) && string.Equals(Script, other.Script);
            }

            public override bool Equals(object obj) {
                return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((Mode)obj));
            }

            public override int GetHashCode() {
                unchecked {
                    var hashCode = Id?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ (DisplayName?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (Script?.GetHashCode() ?? 0);
                    return hashCode;
                }
            }
        }

        private class ModeFile {
            [JsonProperty("items")]
            public List<Mode> Items { get; set; }
        }
        
        private static List<Mode> _modes;
        private static bool _subscribed;

        public static IEnumerable<Mode> GetModes() {
            if (_modes == null) {
                if (!_subscribed) {
                    _subscribed =  true;
                    FilesStorage.Instance.Watcher(ContentCategory.PresetsPerModeConditions).Update += (sender, args) => _modes = null;
                }
                _modes = FilesStorage.Instance.GetContentFilesFiltered(@"*.json", ContentCategory.PresetsPerModeConditions)
                        .SelectMany(x => {
                            try {
                                var data = File.ReadAllText(x.Filename).Trim();
                                if (data.StartsWith(@"{")) {
                                    return JsonConvert.DeserializeObject<ModeFile>(data).Items
                                            .Where(y => !y.Required.IsVersionNewerThan(BuildInformation.AppVersion)).ToList();
                                }
                                return JsonConvert.DeserializeObject<List<Mode>>(data).Select(y => {
                                    y.Category = ToolsStrings.Session_Race;
                                    return y;
                                });
                            } catch (Exception e) {
                                Logging.Warning(e);
                                return new List<Mode>();
                            }
                        }).ToList();
            }
            return _modes;
        }

        [CanBeNull]
        public static string UseScriptParam(PresetPerMode mode) {
            return GetModes().GetByIdOrDefault(mode.ConditionId)?.Param;
        }

        public static string GetDescription(PresetPerMode mode) {
            return GetModes().GetByIdOrDefault(mode.ConditionId)?.Description;
        }

        private string GetFn(PresetPerMode mode) {
            if (mode.ConditionId != null) {
                var m = GetModes().GetByIdOrDefault(mode.ConditionId);
                if (m != null) {
                    return m.Script;
                }
            }
            return mode.ConditionFn;
        }

        public IEnumerable<PresetPerMode> GetPassedModes(IniFile file) {
            var presetsPerMode = new PresetsPerModeReadOnly().GetEntries().ToListIfItIsNot();
            foreach (var mode in presetsPerMode.Where(x => x.Enabled)) {
                bool result;

                try {
                    var script = GetFn(mode);
                    if (string.IsNullOrWhiteSpace(script)) continue;

                    var state = LuaHelper.GetExtended(true);
                    if (state == null) throw new Exception(ToolsStrings.Common_LuaFailed);

                    UserData.RegisterType<IniFile>();
                    UserData.RegisterType<IniFileSection>();
                    state.Globals[@"race"] = UserData.Create(file);
                    state.Globals[@"param"] = mode.ScriptParam ?? string.Empty;
                    script = $@"return {script}";
                    Logging.Debug(script);
                    result = state.DoString(script).AsBool();
                } catch (Exception e) {
                    Logging.Error(e.Message);
                    result = false;
                }

                if (result) {
                    yield return mode;
                }
            }
        }

        private IDisposable _set;

        public override void Set(IniFile file) {
            var result = new PresetPerModeCombined();
            foreach (var mode in GetPassedModes(file)) {
                Logging.Debug($"Mode active: {mode.ConditionId} ({mode.ConditionFn})");
                result.Extend(mode);
            }

            Logging.Debug("Custom presets active: " + result.NotEmpty);
            _set = result.Apply();
        }

        public void Dispose() {
            _set?.Dispose();
            _set = null;
        }
    }
}