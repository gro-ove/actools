using AcTools.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcTools.Processes {
    public partial class Game {
        public static bool OptionEnableRaceIniRestoration = false;

        private static void ClearUpIniFile(IniFile file) {
            file["BENCHMARK"].Set("ACTIVE", false);
            file["REPLAY"].Set("ACTIVE", false);
            file["REMOTE"].Set("ACTIVE", false);

            file.RemoveSections("CAR", 1); // because CAR_0 is a player's car
            file.RemoveSections("SESSION");
        }

        private static void SetDefaultProperies(IniFile file) {
            var lapInvalidatorSection = file["LAP_INVALIDATOR"];
            lapInvalidatorSection.Set("ALLOWED_TYRES_OUT", -1);
        }

        public static bool OptionDebugMode = false;

        [CanBeNull]
        public static Result GetResult() {
            try {
                var filename = OptionDebugMode ? FileUtils.GetResultJsonFilename().Replace("race_out", "race_out_debug") : FileUtils.GetResultJsonFilename();
                if (!File.Exists(filename)) return null;
                var result = JsonConvert.DeserializeObject<Result>(FileUtils.ReadAllText(filename));
                return result?.IsNotCancelled == true ? result : null;
            } catch (Exception e) {
                throw new Exception("Can't parse “race_out.json”", e);
            }
        }

        private static void RemoveResultJson() {
            var filename = FileUtils.GetResultJsonFilename();
            if (File.Exists(filename)) {
                File.Delete(filename);
            }
        }

        private static bool _busy;

        [CanBeNull]
        public static Result Start([NotNull] IAcsStarter starter, [NotNull] StartProperties properties) {
            if (starter == null) throw new ArgumentNullException(nameof(starter));
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            if (_busy) return null;
            _busy = true;

            RemoveResultJson();

            try {
                properties.Set();
                starter.Run();
                starter.WaitUntilGame();
                starter.WaitGame();
            } finally {
                starter.CleanUp();

                _busy = false;
                properties.RevertChanges();
            }

            return GetResult();
        }

        public enum ProgressState {
            Preparing,
            Launching,
            Waiting,
            Finishing
        }

        [ItemCanBeNull]
        public static async Task<Result> StartAsync(IAcsStarter starter, StartProperties properties, IProgress<ProgressState> progress, CancellationToken cancellation) {
            if (_busy) return null;
            _busy = true;

            if (OptionDebugMode) {
                progress?.Report(ProgressState.Waiting);
                await Task.Delay(500, cancellation);
                _busy = false;
                return GetResult();
            }

            RemoveResultJson();

            try {
                progress?.Report(ProgressState.Preparing);
                await Task.Run(() => properties.Set(), cancellation);
                if (cancellation.IsCancellationRequested) return null;

                progress?.Report(ProgressState.Launching);
                await starter.RunAsync(cancellation);
                if (cancellation.IsCancellationRequested) return null;

                await starter.WaitUntilGameAsync(cancellation);
                await Task.Run(() => properties.SetGame(), cancellation);
                if (cancellation.IsCancellationRequested) return null;

                progress?.Report(ProgressState.Waiting);
                await starter.WaitGameAsync(cancellation);
                if (cancellation.IsCancellationRequested) return null;
            } finally {
                progress?.Report(ProgressState.Finishing);
                await starter.CleanUpAsync(cancellation);

                _busy = false;
                properties.RevertChanges();
            }

            return GetResult();
        }

        public abstract class AdditionalProperties {
            /// <summary>
            /// Set properties.
            /// </summary>
            /// <returns>Something disposable what will revert back all changes (optionally)</returns>
            public abstract IDisposable Set();
        }

        public abstract class GameHandler {
            /// <summary>
            /// Do something with runned game.
            /// </summary>
            /// <returns>Something disposable what will revert back all changes (optionally)</returns>
            public abstract IDisposable Set();
        }

        public abstract class RaceIniProperties {
            /// <summary>
            /// Set properties.
            /// </summary>
            /// <param name="file">Main ini-file (race.ini)</param>
            public abstract void Set(IniFile file);
        }

        public class StartProperties {
            public IniFile PreparedConfig;

            public BasicProperties BasicProperties;
            public AssistsProperties AssistsProperties;
            public ConditionProperties ConditionProperties;
            public TrackProperties TrackProperties;
            public BaseModeProperties ModeProperties;
            public ReplayProperties ReplayProperties;

            public List<object> AdditionalPropertieses = new List<object>();

            public void SetAdditional<T>(T properties) {
                AdditionalPropertieses.Remove(GetAdditional<T>());
                if (properties == null) return;
                AdditionalPropertieses.Add(properties);
            }

            [CanBeNull]
            public T GetAdditional<T>() {
                return AdditionalPropertieses.OfType<T>().FirstOrDefault();
            }

            private List<IDisposable> _disposeLater;
            private List<string> _removeLater;

            public StartProperties() {
            }

            public StartProperties(ReplayProperties replayProperties) {
                ReplayProperties = replayProperties;
            }

            public StartProperties(BasicProperties basicProperties, AssistsProperties assistsProperties, ConditionProperties conditionProperties,
                    TrackProperties trackProperties, BaseModeProperties modeProperties) {
                BasicProperties = basicProperties;
                AssistsProperties = assistsProperties;
                ConditionProperties = conditionProperties;
                TrackProperties = trackProperties;
                ModeProperties = modeProperties;
            }

            internal void Set() {
                _disposeLater = new List<IDisposable>();
                _removeLater = new List<string>();

                var iniFilename = FileUtils.GetRaceIniFilename();
                if (OptionEnableRaceIniRestoration) {
                    _disposeLater.Add(FileUtils.RestoreLater(iniFilename));
                }

                var iniFile = PreparedConfig;
                if (iniFile == null) {
                    iniFile = new IniFile(iniFilename);

                    ClearUpIniFile(iniFile);
                    SetDefaultProperies(iniFile);

                    if (BasicProperties != null) {
                        BasicProperties.Set(iniFile);
                        ModeProperties?.Set(iniFile);
                        ConditionProperties?.Set(iniFile);
                        TrackProperties?.Set(iniFile);
                    } else if (ReplayProperties != null) {
                        if (ReplayProperties.Name == null && ReplayProperties.Filename != null) {
                            var dir = FileUtils.GetReplaysDirectory();
                            if (Path.GetDirectoryName(ReplayProperties.Filename)?.Equals(dir, StringComparison.OrdinalIgnoreCase) == true) {
                                ReplayProperties.Name = Path.GetFileName(ReplayProperties.Filename);
                            } else {
                                var removeLaterFilename = FileUtils.GetTempFileName(dir, ".acreplay");
                                ReplayProperties.Name = Path.GetFileName(removeLaterFilename);
                                File.Copy(ReplayProperties.Filename, removeLaterFilename);
                                _removeLater.Add(removeLaterFilename);
                            }
                        }

                        ReplayProperties.Set(iniFile);
                    } else {
                        throw new NotSupportedException();
                    }
                }

                foreach (var properties in AdditionalPropertieses.OfType<RaceIniProperties>()) {
                    properties.Set(iniFile);
                }

                iniFile.Save(iniFilename);

                _disposeLater.Add(AssistsProperties?.Set());
                _disposeLater.AddRange(AdditionalPropertieses.OfType<AdditionalProperties>().Select(x => x.Set()));
            }

            internal void SetGame() {
                _disposeLater.AddRange(AdditionalPropertieses.OfType<GameHandler>().Select(x => x.Set()));
            }

            internal void RevertChanges() {
                _disposeLater?.DisposeEverything();
                if (_removeLater == null) return;

                foreach (var filename in _removeLater) {
                    try {
                        File.Delete(filename);
                    } catch (Exception) {
                        // ignored
                    }
                }
                _removeLater.Clear();
            }
        }
    }
}
