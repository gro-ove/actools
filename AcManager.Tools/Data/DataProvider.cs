using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Data {
    public class DataProvider : NotifyPropertyChanged {
        private static DataProvider _instance;

        public static DataProvider Instance => _instance ?? (_instance = new DataProvider());

        public static void Initialize() {
            Debug.Assert(_instance == null);
            _instance = new DataProvider();
        }

        private DataProvider() {
            FilesStorage.Instance.Watcher(ContentCategory.Miscellaneous).Update += DataProvider_Update;
        }

        public void RefreshData() {
            _dlcInformations = null;
            _kunosContent = null;
            _tagCountries = null;
            _brandCountries = null;
            _countries = null;
        }

        private void DataProvider_Update(object sender, EventArgs e) {
            RefreshData();
        }

        private KunosDlcInformation[] _dlcInformations;

        public KunosDlcInformation[] DlcInformations => _dlcInformations ?? (_dlcInformations =
                FilesStorage.Instance.LoadJsonContentFile<KunosDlcInformation[]>(ContentCategory.Miscellaneous, "KunosDlcs.json") ??
                        new KunosDlcInformation[0]);

        private Dictionary<string, string[]> _kunosContent;

        [NotNull]
        public IReadOnlyDictionary<string, string[]> KunosContent {
            get {
                if (_kunosContent != null) return _kunosContent;

                var j = FilesStorage.Instance.LoadJsonContentFile(ContentCategory.Miscellaneous, "KunosContent.json");
                if (j != null) {
                    _kunosContent = new Dictionary<string, string[]> {
                        [@"tracks"] = (j[@"tracks"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
                        [@"showrooms"] = (j[@"showrooms"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
                        [@"drivers"] = (j[@"drivers"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
                        [@"fonts"] = (j[@"fonts"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
                    };
                } else {
                    _kunosContent = new Dictionary<string, string[]> {
                        [@"tracks"] = new string[] { },
                        [@"showrooms"] = new string[] { },
                        [@"drivers"] = new string[] { },
                        [@"fonts"] = new string[] { },
                    };

                    Logging.Warning("Cannot load KunosContent.json");
                }
                return _kunosContent;
            }
        }

        private Dictionary<string, string[]> _kunosSkins;
        private readonly object _kunosSkinsSync = new object();

        public IReadOnlyDictionary<string, string[]> KunosSkins {
            get {
                if (_kunosSkins != null) return _kunosSkins;

                lock (_kunosSkinsSync) {
                    _kunosSkins = FilesStorage.Instance.LoadJsonContentFile<Dictionary<string, string[]>>(ContentCategory.Miscellaneous,
                            "KunosSkins.json") ?? new Dictionary<string, string[]>();
                    return _kunosSkins;
                }
            }
        }

        private Dictionary<string, string[]> _nationalitiesAndNames;
        private List<NameNationality> _nameNationalities;

        [NotNull]
        public IReadOnlyDictionary<string, string[]> NationalitiesAndNames {
            get {
                if (_nationalitiesAndNames != null) return _nationalitiesAndNames;

                _nationalitiesAndNames = FilesStorage.Instance.LoadJsonContentFile<Dictionary<string, string[]>>(ContentCategory.Miscellaneous,
                        "NationalitiesAndNames.json");
                if (_nationalitiesAndNames != null) return _nationalitiesAndNames;

                _nationalitiesAndNames = new Dictionary<string, string[]>();
                Logging.Warning("Cannot load NationalitiesAndNames.json");
                return _nationalitiesAndNames;
            }
        }

        [NotNull]
        public IList<NameNationality> NationalitiesAndNamesList {
            get {
                if (_nameNationalities != null) return _nameNationalities;
                _nameNationalities = NationalitiesAndNames.SelectMany(
                                x => from y in x.Value select new NameNationality { Name = y, Nationality = x.Key }).ToList();
                return _nameNationalities;
            }
        }

        private Dictionary<string, string> _tagCountries;

        [NotNull]
        public IReadOnlyDictionary<string, string> TagCountries {
            get {
                if (_tagCountries != null) return _tagCountries;

                try {
                    _tagCountries = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(
                            FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "TagCountries.json"))
                            .ManyToDictionaryK(x => x.Value, x => x.Key);
                } catch (Exception e) {
                    Logging.Warning("Cannot load TagCountries.json: " + e);
                    _tagCountries = new Dictionary<string, string>();
                }

                return _tagCountries;
            }
        }

        private Dictionary<string, string> _countries;

        [NotNull]
        public IReadOnlyDictionary<string, string> Countries {
            get {
                if (_countries != null) return _countries;

                try {
                    _countries = JsonConvert.DeserializeObject<string[]>(
                        FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "Countries.json"))
                        .ToDictionary(x => x.ToLower(CultureInfo.InvariantCulture), x => x);
                } catch (Exception e) {
                    Logging.Warning("Cannot load Countries.json: " + e);
                    _countries = new Dictionary<string, string>();
                }

                return _countries;
            }
        }

        private Dictionary<string, string> _showroomsPreviews;

        [NotNull]
        public IReadOnlyDictionary<string, string> ShowroomsPreviews {
            get {
                if (_showroomsPreviews != null) return _showroomsPreviews;

                try {
                    _showroomsPreviews = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "ShowroomsPreviews.json"));
                } catch (Exception e) {
                    Logging.Warning("Cannot load ShowroomsPreviews.json: " + e);
                    _showroomsPreviews = new Dictionary<string, string>();
                }

                return _showroomsPreviews;
            }
        }

        private Dictionary<string, string> _countryByIds;

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryByIds {
            get {
                if (_countryByIds != null) return _countryByIds;

                try {
                    _countryByIds = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                            FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "CountryIds.json"));
                } catch (Exception e) {
                    Logging.Warning("Cannot load CountryIds.json: " + e);
                    _countryByIds = new Dictionary<string, string>();
                }

                return _countryByIds;
            }
        }

        private Dictionary<string, string> _countryByKunosIds;

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryByKunosIds {
            get {
                if (_countryByKunosIds != null) return _countryByKunosIds;

                try {
                    _countryByKunosIds = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                            FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "KunosCountryIds.json"));
                } catch (Exception e) {
                    Logging.Warning("Cannot load KunosCountryIds.json: " + e);
                    _countryByKunosIds = new Dictionary<string, string>();
                }

                return _countryByKunosIds;
            }
        }

        private Dictionary<string, string> _countryToIds;

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryToIds {
            get {
                if (_countryToIds != null) return _countryToIds;

                _countryToIds = new Dictionary<string, string>(CountryByIds.Count);
                foreach (var pair in CountryByIds) {
                    _countryToIds[pair.Value] = pair.Key;
                }
                return _countryToIds;
            }
        }

        private Dictionary<string, string> _countryToKunosIds;

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryToKunosIds {
            get {
                if (_countryToKunosIds != null) return _countryToKunosIds;

                _countryToKunosIds = new Dictionary<string, string>(CountryByKunosIds.Count);
                foreach (var pair in CountryByKunosIds) {
                    _countryToKunosIds[pair.Value] = pair.Key;
                }
                return _countryToKunosIds;
            }
        }

        private Dictionary<string, string> _brandCountries;

        [NotNull]
        public IReadOnlyDictionary<string, string> BrandCountries {
            get {
                if (_brandCountries != null) return _brandCountries;

                try {
                    _brandCountries = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(
                            FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "BrandCountries.json"))
                            .ManyToDictionaryK(x => x.Value, x => x.Key);
                } catch(Exception e) {
                    Logging.Warning("Cannot load BrandCountries.json: " + e);
                    _brandCountries = new Dictionary<string, string>();
                }

                return _brandCountries;
            }
        }

        private Dictionary<string, Dictionary<string, int>> _years;

        [NotNull]
        private IReadOnlyDictionary<string, Dictionary<string, int>> Years {
            get {
                if (_years != null) return _years;

                try {
                    _years = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(
                        FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "Years.json"));
                } catch (Exception e) {
                    Logging.Warning("Cannot load Years.json: " + e);
                    _years = new Dictionary<string, Dictionary<string, int>>();
                }

                return _years;
            }
        }

        private Dictionary<string, int> _carYears;

        [NotNull]
        public IReadOnlyDictionary<string, int> CarYears => _carYears ?? (_carYears = Years.GetValueOrDefault("cars") ?? new Dictionary<string, int>());

        private Dictionary<string, int> _trackYears;

        [NotNull]
        public IReadOnlyDictionary<string, int> TrackYears => _trackYears ?? (_trackYears = Years.GetValueOrDefault("tracks") ?? new Dictionary<string, int>());

        private Dictionary<string, int> _showroomYears;

        [NotNull]
        public IReadOnlyDictionary<string, int> ShowroomYears
            => _showroomYears ?? (_showroomYears = Years.GetValueOrDefault("showrooms") ?? new Dictionary<string, int>());
    }
}
