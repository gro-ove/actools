using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
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
            FilesStorage.Instance.Watcher(ContentCategory.Miscellaneous).Update += OnUpdate;

            _nameNationalities = Lazier.Create(() => NationalitiesAndNames.SelectMany(
                    x => from y in x.Value select new NameNationality { Name = y, Nationality = x.Key }).ToList());

            _countryToIds = Lazier.Create<IReadOnlyDictionary<string, string>>(() => CountryByIds.ToDictionary(x => x.Value, x => x.Key));
            _countryToKunosIds = Lazier.Create<IReadOnlyDictionary<string, string>>(() => CountryByKunosIds.ToDictionary(x => x.Value, x => x.Key));

            _carYears = Lazier.Create<IReadOnlyDictionary<string, int>>(() => Years.GetValueOrDefault("cars") ?? new Dictionary<string, int>());
            _trackYears = Lazier.Create<IReadOnlyDictionary<string, int>>(() => Years.GetValueOrDefault("tracks") ?? new Dictionary<string, int>());
            _showroomYears = Lazier.Create<IReadOnlyDictionary<string, int>>(() => Years.GetValueOrDefault("showrooms") ?? new Dictionary<string, int>());
        }

        public void RefreshData() {
            _nameNationalities.Reset();
            _countryToIds.Reset();
            _countryToKunosIds.Reset();
            _carYears.Reset();
            _trackYears.Reset();
            _showroomYears.Reset();

            _kunosContent.Reset();
            _dlcInformations.Reset();
            _kunosSkins.Reset();
            _nationalitiesAndNames.Reset();
            _tagCountries.Reset();
            _countries.Reset();
            _showroomsPreviews.Reset();
            _countryByIds.Reset();
            _countryByKunosIds.Reset();
            _brandCountries.Reset();
            _years.Reset();

            _trackParams = null;
        }

        private void OnUpdate(object sender, EventArgs e) {
            RefreshData();
        }

        #region Loading methods
        [Localizable(false), NotNull]
        private static Func<T> Load<T>(string fileName) where T : class, new() {
            return () => {
                try {
                    return JsonConvert.DeserializeObject<T>(
                            FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, fileName)) ?? throw new Exception("Can’t load data");
                } catch (Exception e) {
                    Logging.Warning($"Cannot load {fileName}: {e}");
                    return new T();
                }
            };
        }

        [Localizable(false), NotNull]
        private static Func<TResult> Load<TJson, TResult>(string fileName, Func<TJson, TResult> load) where TJson : class where TResult : new() {
            return () => {
                try {
                    return load(JsonConvert.DeserializeObject<TJson>(
                            FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, fileName)) ?? throw new Exception("Can’t load data"));
                } catch (Exception e) {
                    Logging.Warning($"Cannot load {fileName}: {e}");
                    return new TResult();
                }
            };
        }

        [NotNull]
        private static Func<T> Load<T>(string fileName, Func<JObject, T> load) where T : new() {
            return () => {
                try {
                    return load(FilesStorage.Instance.LoadJsonContentFile(ContentCategory.Miscellaneous, fileName) ?? throw new Exception("Can’t load data"));
                } catch (Exception e) {
                    Logging.Warning($"Cannot load {fileName}: {e}");
                    return new T();
                }
            };
        }
        #endregion

        #region Kunos content
        /*[NotNull]
        public IReadOnlyDictionary<string, string[]> KunosContent => _kunosContent.RequireValue;*/

        [CanBeNull]
        public string[] GetKunosContentIds([CanBeNull] string category) {
            try {
                return _kunosContent.RequireValue.GetValueOrDefault(category ?? "");
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        private readonly Lazier<Dictionary<string, string[]>> _kunosContent = Lazier.Create(
                Load("KunosContent.json", LoadKunosContent));

        private static Dictionary<string, string[]> LoadKunosContent(JObject j) {
            var layouts = new List<string>();
            if (j[@"layouts"] is JObject jLayouts) {
                foreach (var pair in jLayouts) {
                    if (pair.Value is JArray jLayoutIds) {
                        layouts.AddRange(jLayoutIds.Select(v => $@"{pair.Key}/{v}"));
                    }
                }
            }

            return new Dictionary<string, string[]> {
                [@"tracks"] = (j[@"tracks"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
                [@"layouts"] = layouts.ToArray(),
                [@"showrooms"] = (j[@"showrooms"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
                [@"drivers"] = (j[@"drivers"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
                [@"fonts"] = (j[@"fonts"] as JArray)?.Select(x => x.ToString()).ToArray() ?? new string[] { },
            };
        }

        [NotNull]
        public IReadOnlyList<KunosDlcInformation> DlcInformations => _dlcInformations.RequireValue;

        private readonly Lazier<List<KunosDlcInformation>> _dlcInformations = Lazier.Create(
                Load<List<KunosDlcInformation>>("KunosDlcs.json"));

        [NotNull]
        public IReadOnlyDictionary<string, string[]> KunosSkins => _kunosSkins.RequireValue;

        private readonly Lazier<Dictionary<string, string[]>> _kunosSkins = Lazier.Create(
                Load<Dictionary<string, string[]>>("KunosSkins.json"));
        #endregion

        #region Nationalities and names for AI drivers
        [NotNull]
        public IReadOnlyDictionary<string, string[]> NationalitiesAndNames => _nationalitiesAndNames.RequireValue;

        private readonly Lazier<Dictionary<string, string[]>> _nationalitiesAndNames = Lazier.Create(
                Load<Dictionary<string, string[]>>("NationalitiesAndNames.json"));

        private Lazier<List<NameNationality>> _nameNationalities;

        [NotNull]
        public IList<NameNationality> NationalitiesAndNamesList => _nameNationalities.RequireValue;
        #endregion

        #region Countries
        private Lazier<IReadOnlyDictionary<string, string>> _countryToIds;
        private Lazier<IReadOnlyDictionary<string, string>> _countryToKunosIds;

        [NotNull]
        public IReadOnlyDictionary<string, string> Countries => _countries.RequireValue;

        private readonly Lazier<Dictionary<string, string>> _countries = Lazier.Create(
                Load<string[], Dictionary<string, string>>("Countries.json", j => j.ToDictionary(x => x.ToLower(CultureInfo.InvariantCulture), x => x)));

        [NotNull]
        public IReadOnlyDictionary<string, string> ShowroomsPreviews => _showroomsPreviews.RequireValue;

        private readonly Lazier<Dictionary<string, string>> _showroomsPreviews = Lazier.Create(
                Load<Dictionary<string, string>>("ShowroomsPreviews.json"));

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryByIds => _countryByIds.RequireValue;

        private readonly Lazier<Dictionary<string, string>> _countryByIds = Lazier.Create(
                Load<Dictionary<string, string>>("CountryIds.json"));

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryByKunosIds => _countryByKunosIds.RequireValue;

        private readonly Lazier<Dictionary<string, string>> _countryByKunosIds = Lazier.Create(
                Load<Dictionary<string, string>>("KunosCountryIds.json"));

        public IEnumerable<string> KunosIdsCountries => CountryByKunosIds.Values;

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryToIds => _countryToIds.RequireValue;

        [NotNull]
        public IReadOnlyDictionary<string, string> CountryToKunosIds => _countryToKunosIds.RequireValue;

        [NotNull]
        public IReadOnlyDictionary<string, string> TagCountries => _tagCountries.RequireValue;

        private readonly Lazier<Dictionary<string, string>> _tagCountries = Lazier.Create(
                Load<Dictionary<string, string[]>, Dictionary<string, string>>("TagCountries.json", j => j.ManyToDictionaryK(x => x.Value, x => x.Key)));

        [NotNull]
        public IReadOnlyDictionary<string, string> BrandCountries => _brandCountries.RequireValue;

        private readonly Lazier<Dictionary<string, string>> _brandCountries = Lazier.Create(
                Load<Dictionary<string, string[]>, Dictionary<string, string>>("BrandCountries.json", j => j.ManyToDictionaryK(x => x.Value, x => x.Key)));
        #endregion

        #region Years
        [NotNull]
        public IReadOnlyDictionary<string, Dictionary<string, int>> Years => _years.RequireValue;

        private readonly Lazier<Dictionary<string, Dictionary<string, int>>> _years = Lazier.Create(
                Load<Dictionary<string, Dictionary<string, int>>>("Years.json"));

        private readonly Lazier<IReadOnlyDictionary<string, int>> _carYears;
        private readonly Lazier<IReadOnlyDictionary<string, int>> _trackYears;
        private readonly Lazier<IReadOnlyDictionary<string, int>> _showroomYears;

        [NotNull]
        public IReadOnlyDictionary<string, int> CarYears => _carYears.RequireValue;

        [NotNull]
        public IReadOnlyDictionary<string, int> TrackYears => _trackYears.RequireValue;

        [NotNull]
        public IReadOnlyDictionary<string, int> ShowroomYears => _showroomYears.RequireValue;
        #endregion

        #region Track params
        private IniFile _trackParams;

        [NotNull]
        public IniFile TrackParams => _trackParams ?? (_trackParams =
                IniFile.Parse(FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "Track Params.ini") ?? string.Empty));
        #endregion

        #region Flexible loader params
        public class UrlUnwrapping {
            public Regex Test;
            public string QueryParameter;
        }

        private List<UrlUnwrapping> _urlUnwrappings;

        [NotNull]
        public List<UrlUnwrapping> UrlUnwrappings => _urlUnwrappings ?? (_urlUnwrappings =
                Load<JArray>("UrlUnwrapping.json")()
                        .Select(x => new { i = x.GetStringValueOnly("urlIncludes"), p = x.GetStringValueOnly("queryParameter") })
                        .Where(x => !string.IsNullOrEmpty(x.i) && !string.IsNullOrEmpty(x.p))
                        .Select(x => new UrlUnwrapping {
                            Test = new Regex(@"\b" + Regex.Escape(x.i), RegexOptions.IgnoreCase),
                            QueryParameter = x.p
                        }).ToList());
        #endregion
    }
}