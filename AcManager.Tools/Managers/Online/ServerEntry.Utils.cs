using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        public static readonly string ExtendedSeparator = @"ℹ";
        private static readonly string TrashSymbols = @")|/#☆★.:=<>+_-";

        private static readonly Regex SpacesCollapseRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex SortingFix1Regex = new Regex($@"[{TrashSymbols}]{{2,}}|^[{TrashSymbols}]", RegexOptions.Compiled);

        private static readonly Regex SortingFix2Regex = new Regex(
                @"^(?:AA+|[ !-]+|A?(?![b-zB-Z0-9)])+)+| ?-$",
                RegexOptions.Compiled);

        private static readonly Regex SimpleCleanUpRegex = new Regex(@"^AA+\s*", RegexOptions.Compiled);

        private static string GetSortingName(string name) {
            if (string.IsNullOrEmpty(name)) {
                return @"zzzz";
            }

            if (IsUnlikelyToBeCheat(char.ToLowerInvariant(name[0]))) {
                return name.ToLowerInvariant();
            }

            var r = new StringBuilder(name.Length);
            var l = 0;

            var p = '\0';
            var pFits = false;

            for (var i = 0; i < name.Length; i++) {
                var c = name[i];
                if (c == '.') continue;

                c = char.ToLowerInvariant(c);
                if (c == 'v') {
                    var skip = false;
                    for (var j = i + 1; j < name.Length; j++) {
                        if (char.IsDigit(name[j]) || name[j] == '.') {
                            skip = true;
                            i++;
                        } else {
                            break;
                        }
                    }

                    if (skip) {
                        continue;
                    }
                }

                var cFits = (char.IsLetter(c) || pFits && char.IsDigit(c))
                        && (c != p || l > 0 || IsUnlikelyToBeCheat(c));

                if (pFits) {
                    if (l < 2 && cFits && p == 'a' && c == 'c' && !NextIsLetter()) {
                        pFits = false;
                        continue;
                    }

                    if (cFits || l > 0) {
                        l++;
                        r.Append(p);
                    }
                }

                if (l > 5) {
                    return r.Append(name.Substring(i)).ToString();
                }

                p = c;
                pFits = cFits;

                bool NextIsLetter() {
                    return i + 1 < name.Length && char.IsLetter(name[i + 1]);
                }
            }

            if (pFits) {
                r.Append(p);
            }

            if (l > 0) {
                return r.ToString();
            }

            var lettersOnly = LettersOnly(name);
            return lettersOnly.Length > 0 ? lettersOnly : @"zzz:" + name;

            bool IsUnlikelyToBeCheat(char c) {
                return c > 'c' && c <= 'z';
            }

            string LettersOnly(string s) {
                var o = new StringBuilder(s.Length);
                for (var i = 0; i < s.Length; i++) {
                    if (char.IsLetterOrDigit(s[i])) {
                        o.Append(char.ToLowerInvariant(s[i]));
                    }
                }
                return o.ToString();
            }
        }

        private static string InvisibleCleanUp(string s) {
            var r = new StringBuilder();
            int j = 0, i = 0;
            for (; i < s.Length; i++) {
                var insert = '\0';

                var c = s[i];
                switch (c) {
                    case '\t':
                        insert = ' ';
                        break;
                    default:
                        if (IsControl(c)) {
                            break;
                        } else {
                            continue;
                        }
                }

                if (i > j) {
                    r.Append(s.Substring(j, i - j));
                }

                if (insert != '\0') {
                    r.Append(insert);
                }

                j = i + 1;
            }

            if (j == 0) return s;
            r.Append(s.Substring(j));
            return r.ToString();

            bool IsControl(char c) {
                return c <= '\u0008' || c >= '\u000A' && c <= '\u000C' || c >= '\u000A' && c <= '\u001F';
            }
        }

        private static string CleanUp(string name, [CanBeNull] string oldName, out int? extPort, out string detailsId) {
            var originalName = name;
            name = ServerDetailsUtils.ExtractDetailsId(name, out detailsId);

            var specialIndex = name.IndexOf(ExtendedSeparator, StringComparison.InvariantCulture);
            if (specialIndex != -1) {
                extPort = FlexibleParser.TryParseInt(name.Substring(specialIndex + ExtendedSeparator.Length));
                name = name.Substring(0, specialIndex);
            } else {
                extPort = null;
            }

            name = InvisibleCleanUp(name.Trim());
            name = SpacesCollapseRegex.Replace(name, " ");

            var fixMode = SettingsHolder.Online.FixNamesMode.IntValue ?? 0;
            if (fixMode != 0) {
                name = SortingFix2Regex.Replace(name, "");

                if (fixMode == 2) {
                    var v = SortingFix1Regex.Replace(name, " ");
                    if (v != name) {
                        name = SpacesCollapseRegex.Replace(v, " ").Trim();
                    }
                }
            } else if (oldName != null && SimpleCleanUpRegex.IsMatch(name) && !SimpleCleanUpRegex.IsMatch(oldName)) {
                name = SimpleCleanUpRegex.Replace(name, "");
            }

            return string.IsNullOrWhiteSpace(name) ? originalName : name;
        }

        private void CheckPostUpdate() {
            UpdateMissingContent();

            var missingSomething = _missingCarsError != null || _missingTrackError != null;
            if (_missingContentReferences != null) {
                missingSomething |= UpdateMissingContentExtended(missingSomething) == ServerStatus.MissingContent;
            }

            if (CspRequiredMissing) {
                Status = ServerStatus.Error;
            } else if (Status == ServerStatus.Ready || Status == ServerStatus.MissingContent) {
                Status = missingSomething ? ServerStatus.MissingContent : ServerStatus.Ready;
            }

            UpdateErrorsList();
            AvailableUpdate();
        }

        public bool CheckCars() {
            var cars = Cars;
            if (cars == null) return false;

            if (SettingsHolder.Online.LoadServersWithMissingContent) {
                // In this mode, data is loaded no matter if data is here or not, so we can just update
                // entries without reloading whole thing.

                if (cars.Aggregate(false, (current, car) => current | car.UpdateCarObject())) {
                    if (CurrentDrivers != null) {
                        foreach (var currentDriver in CurrentDrivers) {
                            currentDriver.ResetCar();
                        }
                    }

                    CheckPostUpdate();

                    // Specially for OnlineItem to update list of cars.
                    OnPropertyChanged(nameof(Cars));
                }

                return false;
            }

            for (var i = cars.Count - 1; i >= 0; i--) {
                var car = cars[i];
                if (car.CarWrapper != CarsManager.Instance.GetWrapperById(car.Id)) {
                    goto Dirty;
                }
            }

            return false;

            Dirty:

            Cars = null;
            SetSelectedCarEntry(null);

            if (CurrentDrivers != null) {
                foreach (var currentDriver in CurrentDrivers) {
                    currentDriver.ResetCar();
                }
            }

            Status = ServerStatus.Unloaded;
            return true;
        }

        public void CheckCarSkins(string carId) {
            if (Cars?.GetByIdOrDefault(carId) != null) {
                CheckPostUpdate();
            }
        }

        private bool _carVersionIsWrong;

        public void OnCarVersionChanged(CarObject car) {
            if (!_carVersionIsWrong || !HasDetails ||
                    Cars?.Any(x => x.CarWrapper?.Value == car) != true) return;
            CheckPostUpdate();
        }

        public bool CheckTrack() {
            if (TrackId == null) return false;

            var track = GetTrack(TrackId);
            if (track == Track) return false;

            Track = track;

            if (SettingsHolder.Online.LoadServersWithMissingContent) {
                // In this mode, data is loaded no matter if data is here or not, so we can just update
                // entries without reloading whole thing.
                CheckPostUpdate();
                return false;
            }

            Status = ServerStatus.Unloaded;
            return true;
        }

        private bool _trackVersionIsWrong;

        public void OnTrackVersionChanged(TrackObjectBase track) {
            if (!_trackVersionIsWrong || !HasDetails || Track != track) return;
            CheckPostUpdate();
        }

        public bool CheckWeather() {
            if (!_weatherObjectSet || WeatherId == null) return false;

            var weather = WeatherManager.Instance.GetById(WeatherId);
            if (weather == _weatherObject) return false;

            if (SettingsHolder.Online.LoadServersWithMissingContent) {
                // In this mode, data is loaded no matter if data is here or not, so we can just update
                // entries without reloading whole thing.
                _weatherObject = weather;
                CheckPostUpdate();
                return false;
            }

            Status = ServerStatus.Unloaded;
            return true;
        }

        public void UpdateMissing() {
            if (Status == ServerStatus.MissingContent || Status == ServerStatus.Error) {
                CheckPostUpdate();
            }
        }
    }
}