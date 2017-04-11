using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.PresetsPerMode;
using AcManager.Tools.Miscellaneous;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using MoonSharp.Interpreter;
using Newtonsoft.Json;

namespace AcManager.Tools.GameProperties {
    public class ModeSpecificPresetsHelper : Game.RaceIniProperties, IDisposable {
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class Mode : IWithId {
            public string Id { get; }

            public string Script;

            [JsonConstructor]
            public Mode(string id, string script) {
                Id = id;
                Script = script;
            }
        }

        private List<Mode> _modes;

        private IEnumerable<Mode> InitializeModes() {
            if (_modes == null) {
                _modes = FilesStorage.Instance.GetContentFilesFiltered(@"*.json", ContentCategory.PresetsPerModeConditions)
                                     .SelectMany(x => {
                                         try {
                                             return JsonConvert.DeserializeObject<Mode[]>(File.ReadAllText(x.Filename));
                                         } catch (Exception e) {
                                             Logging.Warning(e);
                                             return new Mode[0];
                                         }
                                     }).ToList();
            }

            return _modes;
        }

        private string GetFn(PresetPerMode mode) {
            if (mode.ConditionId != null) {
                var m = InitializeModes().GetByIdOrDefault(mode.ConditionId);
                if (m != null) {
                    return m.Script;
                }
            }

            return mode.ConditionFn;
        }

        public IEnumerable<PresetPerMode> GetPassedModes(IniFile file) {
            var presetsPerMode = new PresetsPerModeReadOnly().GetEntries().ToListIfItIsNot();
            foreach (var mode in presetsPerMode.Where(x => x.Enabled)) {
                var script = GetFn(mode);
                if (string.IsNullOrWhiteSpace(script)) continue;

                var state = LuaHelper.GetExtended();
                if (state == null) throw new Exception(ToolsStrings.Common_LuaFailed);

                UserData.RegisterType<IniFile>();
                UserData.RegisterType<IniFileSection>();

                state.Globals[@"race"] = UserData.Create(file);

                script = $@"return {script}";
                Logging.Debug(script);
                if (state.DoString(script).AsBool()) {
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