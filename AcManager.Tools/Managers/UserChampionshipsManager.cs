using System;
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
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Managers {
    public class UserChampionshipsManager : AcManagerFileSpecific<UserChampionshipObject> {
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
                        var places = e.Result.Sessions.Last().GetTakenPlacesPerCar();
                        ev.TakenPlace = places[0] + 1;
                        ev.IsPassed = true;

                        var nextEvent = career.ExtendedRounds.FirstOrDefault(x => x.Index == careerProperties.RoundIndex + 1);
                        if (nextEvent != null) nextEvent.IsAvailable = true;

                        var pointsPerPlace = career.Rules.Points;
                        var playerPointsDelta = pointsPerPlace.ElementAtOrDefault(places[0]);

                        if (career.PointsForBestLap != 0) {
                            var bestLap = e.Result.Sessions.SelectMany(x => x.BestLaps).MinEntryOrDefault(x => x.Time);
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
                            var bestLap = e.Result.Sessions.Where(x => x.Type == Game.SessionType.Qualification)
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
                            driver.Points += pointsPerPlace.ElementAtOrDefault(places.ElementAtOrDefault(i));
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

        public override IAcDirectories Directories => AcRootDirectory.Instance.UserChampionshipsDirectories;

        protected override UserChampionshipObject CreateAcObject(string id, bool enabled) {
            return new UserChampionshipObject(this, id, enabled);
        }

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            return !UserChampionshipObject.ExtraExtensions.Any(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase)) &&
                   !filename.EndsWith(UserChampionshipObject.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        protected override string GetObjectLocation(string filename, out bool inner) {
            var minLength = Math.Min(Directories.EnabledDirectory.Length,
                    Directories.DisabledDirectory?.Length ?? int.MaxValue);

            inner = false;
            while (filename.Length > minLength) {
                var parent = Path.GetDirectoryName(filename);
                if (parent == null) return null;

                if (parent == Directories.EnabledDirectory || parent == Directories.DisabledDirectory) {
                    var special = UserChampionshipObject.ExtraExtensions.FirstOrDefault(x => filename.EndsWith(x, StringComparison.OrdinalIgnoreCase));
                    if (special == null) return filename;

                    inner = true;
                    return filename.ApartFromLast(special, StringComparison.OrdinalIgnoreCase) + UserChampionshipObject.FileExtension;
                }

                inner = true;
                filename = parent;
            }

            return null;
        }

        protected override void MoveInner(string id, string newId, string oldLocation, string newLocation, bool newEnabled) {
            throw new NotSupportedException();
        }

        protected override void DeleteInner(string id, string location) {
            throw new NotSupportedException();
        }

        public override void Rename(string id, string newFileName, bool newEnabled) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));

            var currentLocation = ((AcCommonObject)wrapper.Value).Location;
            var currentExtended = ((UserChampionshipObject)wrapper.Value).ExtendedFilename;
            var currentPreview = ((UserChampionshipObject)wrapper.Value).PreviewImage;

            var path = newEnabled ? Directories.EnabledDirectory : Directories.DisabledDirectory;
            if (path == null) throw new ToggleException(ToolsStrings.AcObject_CannotBeMoved);

            if (!File.Exists(currentExtended)) {
                currentExtended = null;
            }

            if (!File.Exists(currentPreview)) {
                currentPreview = null;
            }

            var newLocation = Path.Combine(path, newFileName);
            var newBasePart = Path.GetFileName(newLocation).ApartFromLast(UserChampionshipObject.FileExtension);
            var newExtended = currentExtended == null ? null : Path.Combine(path, newBasePart + UserChampionshipObject.FileDataExtension);
            var newPreview = currentPreview == null ? null : Path.Combine(path, newBasePart + UserChampionshipObject.FilePreviewExtension);
            if (FileUtils.Exists(newLocation) ||
                    currentExtended != null && File.Exists(newExtended) ||
                    currentPreview != null && File.Exists(newPreview)) throw new ToggleException(ToolsStrings.AcObject_PlaceIsTaken);

            try {
                using (IgnoreChanges()) {
                    FileUtils.Move(currentLocation, newLocation);

                    if (currentExtended != null) {
                        FileUtils.Move(currentExtended, newExtended);
                    }

                    if (currentPreview != null) {
                        FileUtils.Move(currentPreview, newPreview);
                    }

                    var obj = CreateAndLoadAcObject(newFileName, Directories.CheckIfEnabled(newLocation));
                    obj.PreviousId = id;
                    ReplaceInList(id, new AcItemWrapper(this, obj));

                    UpdateList();
                }
            } catch (Exception e) {
                throw new ToggleException(e.Message);
            }
        }

        public override void Delete(string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var obj = GetById(id);
            if (obj == null) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));

            using (IgnoreChanges()) {
                FileUtils.Recycle(obj.Location,
                        File.Exists(obj.ExtendedFilename) ? obj.ExtendedFilename : null,
                        File.Exists(obj.PreviewImage) ? obj.PreviewImage : null);
                if (!FileUtils.Exists(obj.Location)) {
                    RemoveFromList(id);
                }
            }
        }

        public void AddNew() {
            try {
                var newId = Guid.NewGuid() + UserChampionshipObject.FileExtension;
                var filename = Path.Combine(Directories.EnabledDirectory, newId);
                var defaultFilename = Path.Combine(AcRootDirectory.Instance.RequireValue, "launcher", "themes", "default", "modules", "champs", "default.json");

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

                File.WriteAllText(filename, data);

                using (IgnoreChanges()) {
                    var obj = CreateAndLoadAcObject(newId, true);
                    InnerWrappersList.Add(new AcItemWrapper(this, obj));
                    UpdateList();
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t add a new object", e);
            }
        }

        #region Progress
        private string _currentId;

        public string CurrentId {
            get { return _currentId; }
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
            get { return GetById(CurrentId); }
            set { CurrentId = value.Id; }
        }
        #endregion
    }
}