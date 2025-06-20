﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class KunosCareerManager : AcManagerNew<KunosCareerObject> {
        private static KunosCareerManager _instance;

        public static KunosCareerManager Instance => _instance ?? (_instance = new KunosCareerManager());

        internal class CareerProperties {
            public string CareerId, EventId;
        }

        private KunosCareerManager() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;
            KunosCareerProgress.Instance.PropertyChanged += Progress_Updated;
            GameWrapper.Finished += GameWrapper_Finished;
            ShowIntro = KunosCareerProgress.Instance.IsNew;
            CurrentId = KunosCareerProgress.Instance.CurrentSeries;
        }

        private void Drive_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.DriveSettings.KunosCareerUserSkin) || SettingsHolder.Drive.KunosCareerUserSkin) return;
            foreach (var ev in Loaded.SelectMany(x => x.Events)) {
                ev.ResetSkinToDefault();
            }
        }

        private bool _showIntro;

        public bool ShowIntro {
            get => _showIntro;
            set {
                if (Equals(value, _showIntro)) return;
                _showIntro = value;
                KunosCareerProgress.Instance.IsNew = value;
                OnPropertyChanged();
            }
        }

        private bool CheckIfCareerCompleted(KunosCareerObject career) {
            switch (career.Type) {
                case KunosCareerObjectType.Championship:
                    return career.ChampionshipPoints >= career.ChampionshipPointsGoal && (career.ChampionshipRankingGoal == 0 ||
                            career.CompletedEvents == career.EventsManager.WrappersList.Count && career.ChampionshipPlace >= career.ChampionshipRankingGoal);

                case KunosCareerObjectType.SingleEvents:
                    var first = career.FirstPlaces;
                    var second = career.SecondPlaces;
                    var third = career.ThirdPlaces;

                    first -= career.FirstPlacesGoal;
                    if (first < 0) return false;

                    second += first - career.SecondPlacesGoal;
                    if (second < 0) return false;

                    third += second - career.ThirdPlacesGoal;
                    return third >= 0;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void GameWrapper_Finished(object sender, GameFinishedArgs e) {
            Logging.Write("Race finished");

            var careerProperties = e.StartProperties.GetAdditional<CareerProperties>();
            if (careerProperties == null) {
                Logging.Write("Not a career race");
                return;
            }

            if (e.Result == null) {
                Logging.Write("Result=null");
                return;
            }

            Logging.Write($"Career: {careerProperties.CareerId}");

            var career = GetById(careerProperties.CareerId);
            var ev = career?.GetEventById(careerProperties.EventId);
            if (ev == null) {
                Logging.Warning("Can’t find career or event by ID.");
                return;
            }

            /* just for in case */
            career.EnsureEventsLoaded();
            Logging.Write($"Finished: {careerProperties.CareerId}, {careerProperties.EventId}.");

            switch (career.Type) {
                case KunosCareerObjectType.SingleEvents:
                    var conditionProperties = e.StartProperties.GetAdditional<PlaceConditions>();
                    if (conditionProperties == null) {
                        Logging.Warning("PlaceConditionsProperties are missing.");
                        return;
                    }

                    var takenPlace = conditionProperties.GetTakenPlace(e.Result);
                    if (takenPlace >= ev.TakenPlace) {
                        Logging.Warning("Taken place is worse than saved place.");
                        return;
                    }

                    Logging.Write($"Taken place is changed from {ev.TakenPlace} to {takenPlace}.");
                    ev.TakenPlace = takenPlace;
                    career.SaveProgress(true);
                    if (!career.IsCompleted && CheckIfCareerCompleted(career)) {
                        Logging.Debug("Career is completed!");
                        KunosCareerProgress.Instance.Completed = KunosCareerProgress.Instance.Completed.Append(career.Id).ToArray();
                        career.IsCompleted = true;
                        career.NextCareerObject.UpdateIfIsAvailable();
                    }
                    break;

                case KunosCareerObjectType.Championship:
                    if (e.Result.NumberOfSessions > 0) {
                        var places = e.Result.Sessions?.Last().GetTakenPlacesPerCar();
                        ev.TakenPlace = places?[0] + 1 ?? 0;
                        ev.IsPassed = true;

                        var nextEvent = career.EventsManager.GetByNumber(ev.EventNumber + 1);
                        if (nextEvent != null) nextEvent.IsAvailable = true;

                        var pointsPerPlace = career.ChampionshipPointsPerPlace;
                        career.ChampionshipPoints += pointsPerPlace.ArrayElementAtOrDefault(places?[0] ?? -1);
                        career.ChampionshipAiPoints = places?.Skip(1).Select((place, id) =>
                                pointsPerPlace.ArrayElementAtOrDefault(place) + career.ChampionshipAiPoints?.ElementAtOrDefault(id) ?? 0).ToArray();
                        career.SaveProgress(true);

                        if (!career.IsCompleted && CheckIfCareerCompleted(career)) {
                            KunosCareerProgress.Instance.Completed = KunosCareerProgress.Instance.Completed.Append(career.Id).ToArray();
                        }
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
                case nameof(KunosCareerProgress.IsNew):
                    ShowIntro = KunosCareerProgress.Instance.IsNew;
                    break;

                case nameof(KunosCareerProgress.CurrentSeries):
                    CurrentId = KunosCareerProgress.Instance.CurrentSeries;
                    break;

                case nameof(KunosCareerProgress.Completed):
                    Progress = (double)KunosCareerProgress.Instance.Completed.Length / InnerWrappersList.Count;
                    foreach (var careerObject in Loaded) {
                        careerObject.UpdateIsCompletedFlag();
                    }
                    break;

                case nameof(KunosCareerProgress.Entries):
                    foreach (var careerObject in Loaded) {
                        careerObject.LoadProgress();
                    }
                    break;
            }
        }

        private double _progress;

        public double Progress {
            get => _progress;
            set {
                if (Equals(value, _progress)) return;
                _progress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayProgress));
            }
        }

        public string DisplayProgress => $"{100d * Progress:F0}%";

        private string _currentId;

        public string CurrentId {
            get => _currentId;
            set {
                if (Equals(value, _currentId)) return;
                _currentId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Current));

                if (Equals(value, KunosCareerProgress.Instance.CurrentSeries)) return;
                KunosCareerProgress.Instance.CurrentSeries = value;
            }
        }

        public KunosCareerObject Current {
            get => GetById(CurrentId);
            set => CurrentId = value.Id;
        }

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            // TODO
            return false;
        }

        protected override bool Filter(string id, string filename) {
            return id != @"series0" && id.StartsWith(@"series");
        }

        public override void ActualScan() {
            base.ActualScan();
            Progress = (double)KunosCareerProgress.Instance.Completed.Length / InnerWrappersList.Count;
            RebuildTree();
        }

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            if (Directories == null) return new List<AcPlaceholderNew>();
            return Directories.GetContentDirectories(@"series*").Select(dir => {
                var id = Directories.GetId(dir);
                return Filter(id, dir) ? CreateAcPlaceholder(id, Directories.CheckIfEnabled(dir)) : null;
            }).NonNull();
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.KunosCareerDirectories;

        public override KunosCareerObject GetDefault() {
            var v = WrappersList.FirstOrDefault(x => x.Value.Id.Contains(@"series0"));
            return v != null ? EnsureWrapperLoaded(v) : base.GetDefault();
        }

        protected override KunosCareerObject CreateAcObject(string id, bool enabled) {
            var careerObject = new KunosCareerObject(this, id, enabled);
            careerObject.PropertyChanged += CareerObject_PropertyChanged;
            return careerObject;
        }

        protected override void OnListUpdate() {
            RebuildTree();
        }

        private void RebuildTree() {
            var list = Loaded.ToDictionary(x => x.Id, x => x);
            foreach (var careerObject in list.Values) {
                careerObject.NextCareerObject = null;
            }

            foreach (var career in list.Values) {
                var previous = (from id in career.RequiredSeries where list.ContainsKey(id) let entry = list[id] where entry.NextCareerObject == null select entry).ToList();
                if (!previous.Any()) continue;

                if (!career.RequiredAnySeries) {
                    var p = previous[0];
                    foreach (var o in previous.Skip(1)) {
                        p.NextCareerObject = o;
                    }
                }

                foreach (var previousCareer in career.RequiredAnySeries ? previous : previous.TakeLast(1)) {
                    previousCareer.NextCareerObject = career;
                }
            }
        }

        private void CareerObject_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            // TODO?
        }
    }
}
