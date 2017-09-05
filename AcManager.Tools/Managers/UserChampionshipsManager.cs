using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Managers {
    public class UserChampionshipsManager : AcManagerFileSpecific<UserChampionshipObject>, ICreatingManager {
        private static UserChampionshipsManager _instance;

        public static UserChampionshipsManager Instance => _instance ?? (_instance = new UserChampionshipsManager());

        public class ChampionshipProperties {
            public string ChampionshipId;
            public int RoundIndex;
        }

        private UserChampionshipsManager() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;
            UserChampionshipsProgress.Instance.PropertyChanged += Progress_Updated;
            GameWrapper.Finished += GameWrapper_Finished;
            CurrentId = UserChampionshipsProgress.Instance.Current + UserChampionshipObject.FileExtension;
        }

        private void Drive_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.DriveSettings.KunosCareerUserSkin) || SettingsHolder.Drive.KunosCareerUserSkin) return;
            foreach (var ev in LoadedOnly) {
                ev.ResetSkinToDefault();
            }
        }

        private void GameWrapper_Finished(object sender, GameFinishedArgs e) {
            Logging.Write("Race finished");

            var careerProperties = e.StartProperties.GetAdditional<ChampionshipProperties>();
            if (careerProperties == null) {
                Logging.Write("Not a championship race");
                return;
            }

            if (e.Result == null) {
                Logging.Write("Result=null");
                return;
            }

            Logging.Write($"Championship: {careerProperties.ChampionshipId}");

            var career = GetById(careerProperties.ChampionshipId);
            var ev = career?.ExtendedRounds.FirstOrDefault(x => x.Index == careerProperties.RoundIndex);
            if (ev == null) {
                Logging.Warning("Can’t find championship or round by ID.");
                return;
            }

            switch (career.Type) {
                case KunosCareerObjectType.SingleEvents:
                    throw new NotSupportedException();

                case KunosCareerObjectType.Championship:
                    if (e.Result.NumberOfSessions > 0) {
                        var places = e.Result.Sessions?.Last().GetTakenPlacesPerCar();
                        ev.TakenPlace = places?[0] + 1 ?? 0;
                        ev.IsPassed = true;

                        var nextEvent = career.ExtendedRounds.FirstOrDefault(x => x.Index == careerProperties.RoundIndex + 1);
                        if (nextEvent != null) nextEvent.IsAvailable = true;

                        var pointsPerPlace = career.Rules.Points;
                        var playerPointsDelta = pointsPerPlace.ElementAtOrDefault(places?[0] ?? -1);

                        if (career.PointsForBestLap != 0) {
                            var bestLap = e.Result.Sessions?.SelectMany(x => x.BestLaps).MinEntryOrDefault(x => x.Time);
                            if (bestLap == null) {
                                Logging.Debug("Best lap: not set");
                            } else {
                                Logging.Debug($"Best lap: set by {bestLap.CarNumber}, {bestLap.Time}");

                                var driver = career.Drivers.ElementAtOrDefault(bestLap.CarNumber);
                                if (driver == null) {
                                    Logging.Warning("Best lap: driver not found!");
                                } else {
                                    Logging.Debug($"So, {PluralizingConverter.PluralizeExt(career.PointsForBestLap, "{0} bonus point")} for {driver.Name}!");
                                    if (bestLap.CarNumber == 0) {
                                        playerPointsDelta += career.PointsForBestLap;
                                    } else {
                                        driver.Points += career.PointsForBestLap;
                                    }
                                }
                            }
                        }

                        if (career.PointsForPolePosition != 0) {
                            var bestLap = e.Result.Sessions?.Where(x => x.Type == Game.SessionType.Qualification)
                                           .SelectMany(x => x.BestLaps)
                                           .MinEntryOrDefault(x => x.Time);
                            if (bestLap == null) {
                                Logging.Debug("Pole position: not set");
                            } else {
                                Logging.Debug($"Pole position: set by {bestLap.CarNumber}, {bestLap.Time}");

                                var driver = career.Drivers.ElementAtOrDefault(bestLap.CarNumber);
                                if (driver == null) {
                                    Logging.Warning("Pole position: driver not found!");
                                } else {
                                    Logging.Debug($"So, {PluralizingConverter.PluralizeExt(career.PointsForPolePosition, "{0} bonus point")} for {driver.Name}!");
                                    if (bestLap.CarNumber == 0) {
                                        playerPointsDelta += career.PointsForPolePosition;
                                    } else {
                                        driver.Points += career.PointsForPolePosition;
                                    }
                                }
                            }
                        }

                        career.ChampionshipPoints += playerPointsDelta;

                        for (var i = career.Drivers.Count - 1; i >= 0; i--) {
                            var driver = career.Drivers[i];
                            if (driver.IsPlayer) continue;
                            driver.Points += pointsPerPlace.ElementAtOrDefault(places?.ElementAtOrDefault(i) ?? -1);
                        }

                        career.UpdateTakenPlaces();
                        career.SaveProgress(true);
                    } else {
                        throw new NotImplementedException();
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Progress_Updated(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(UserChampionshipsProgress.Current):
                    CurrentId = UserChampionshipsProgress.Instance.Current + UserChampionshipObject.FileExtension;
                    break;

                case nameof(UserChampionshipsProgress.Entries):
                    foreach (var careerObject in LoadedOnly) {
                        careerObject.LoadProgress();
                    }
                    break;
            }
        }

        public override string SearchPattern => @"*.champ";

        public override string[] AttachedExtensions => UserChampionshipObject.ExtraExtensions;

        protected override string CheckIfIdValid(string id) {
            if (!id.EndsWith(UserChampionshipObject.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return $"ID should end with “{UserChampionshipObject.FileExtension}”.";
            }

            return base.CheckIfIdValid(id);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.UserChampionshipsDirectories;

        protected override UserChampionshipObject CreateAcObject(string id, bool enabled) {
            return new UserChampionshipObject(this, id, enabled);
        }

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            return !UserChampionshipObject.ExtraExtensions.Any(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase)) &&
                   !filename.EndsWith(UserChampionshipObject.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public override IEnumerable<string> GetAttachedFiles(string location) {
            yield return location.ApartFromLast(UserChampionshipObject.FileExtension, StringComparison.OrdinalIgnoreCase) +
                    UserChampionshipObject.FileDataExtension;
            yield return location.ApartFromLast(UserChampionshipObject.FileExtension, StringComparison.OrdinalIgnoreCase) +
                    UserChampionshipObject.FilePreviewExtension;
        }

        public IAcObjectNew AddNew(string id = null) {
            var newId = Guid.NewGuid() + UserChampionshipObject.FileExtension;
            var filename = Directories.GetLocation(newId, true);

            if (File.Exists(filename)) {
                throw new InformativeException("Can’t add a new object", "This ID is already taken.");
            }

            var defaultFilename = Path.Combine(AcRootDirectory.Instance.RequireValue, @"launcher", @"themes", @"default", @"modules", @"champs", @"default.json");

            var data = File.Exists(defaultFilename) ? File.ReadAllText(defaultFilename) : @"{
""name"":""My championship"",
""rules"":{""practice"":30,""qualifying"":60,""points"":[10,8,6,3,2,1],""penalties"":true,""jumpstart"":1},
""opponents"":[{""name"":""PLAYER"",""skin"":""red_white"",""car"":""abarth500""}],
""rounds"":[{""track"":""magione"",""laps"":10,""weather"":4,""surface"":3}],
""maxCars"":18}";

            var parsed = JObject.Parse(data);
            var name = parsed.GetStringValueOnly("name");
            if (EnabledOnly.Any(x => x.Name == name)) {
                for (var i = 1; i < 999; i++) {
                    var candidate = $@"{name} ({i})";
                    if (EnabledOnly.All(x => x.Name != candidate)) {
                        name = candidate;
                        break;
                    }
                }

                parsed[@"name"] = name;
                data = parsed.ToString(Formatting.Indented);
            }

            using (IgnoreChanges()) {
                File.WriteAllText(filename, data);

                var obj = CreateAndLoadAcObject(newId, true);
                InnerWrappersList.Add(new AcItemWrapper(this, obj));
                UpdateList(true);

                return obj;
            }
        }

        #region Progress
        private string _currentId;

        public string CurrentId {
            get => _currentId;
            set {
                if (Equals(value, _currentId)) return;
                _currentId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Current));

                var progressId = value?.ApartFromLast(UserChampionshipObject.FileExtension);
                if (progressId != null && !Equals(progressId, UserChampionshipsProgress.Instance.Current)) {
                    UserChampionshipsProgress.Instance.Current = progressId;
                }
            }
        }

        public UserChampionshipObject Current {
            get => GetById(CurrentId);
            set => CurrentId = value.Id;
        }
        #endregion
    }
}