using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Tools.Tyres {
    public sealed class TyresEntry : Displayable, IDraggable {
        public static bool OptionRoundNicely = true;

        [CanBeNull]
        public CarObject Source => _carObjectLazy.Value;

        [NotNull]
        private readonly Lazy<CarObject> _carObjectLazy;

        [CanBeNull]
        public string SourceCarId { get; }

        public int Version { get; }

        [NotNull]
        public IniFileSection MainSection { get; }

        [NotNull]
        public IniFileSection ThermalSection { get; }

        public bool RearTyres { get; }

        private bool _bothTyres;

        public bool BothTyres {
            get => _bothTyres;
            set {
                if (value == _bothTyres) return;
                _bothTyres = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayPosition));
            }
        }

        public string DisplayPosition => BothTyres ? "Both tyres" : RearTyres ? "Rear tyres" : "Front tyres";

        public string DisplaySource => $@"{Source?.Name ?? SourceCarId} ({DisplayPosition.ToLower(CultureInfo.CurrentUICulture)})";

        public string DisplayParams { get; }

        public string DisplayParamsExtra { get; }

        public string DisplayParamsExtended { get; }

        private string _name;

        [NotNull]
        public string Name {
            get => _name;
            set {
                if (string.IsNullOrWhiteSpace(value)) value = @"?";
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
                DisplayName = $@"{Name} {DisplayParams}";
            }
        }

        private string _shortName;

        [NotNull]
        public string ShortName {
            get => _shortName;
            set => Apply(value, ref _shortName);
        }

        [CanBeNull]
        public readonly string WearCurveData;

        [CanBeNull]
        public readonly string PerformanceCurveData;

        [CanBeNull]
        public TyresEntry OtherEntry { get; set; }

        public TyresEntry([CanBeNull] string sourceCarId, int version, IniFileSection mainSection, IniFileSection thermalSection,
                [CanBeNull] string wearCurveData, [CanBeNull] string performanceCurveData,
                bool rearTyres, [CanBeNull] Lazy<double?> rimRadiusLazy) {
            SourceCarId = sourceCarId;
            _carObjectLazy = new Lazy<CarObject>(() => SourceCarId == null ? null : CarsManager.Instance.GetById(SourceCarId));

            Version = version;
            MainSection = mainSection;
            ThermalSection = thermalSection;
            RearTyres = rearTyres;

            WearCurveData = wearCurveData;
            PerformanceCurveData = performanceCurveData;

            ReadParameters(rimRadiusLazy);
            DisplayParams = $@"{DisplayWidth}/{DisplayProfile}/R{DisplayRimRadius}";
            DisplayParamsExtra = $"Radius: {Radius * 100:F1} cm, profile: {(Radius - RimRadius) * 100:F1} cm";
            DisplayParamsExtended = $@"{DisplayParams} ({DisplayParamsExtra.ToSentenceMember()})";
            Name = mainSection.GetNonEmpty("NAME") ?? @"?";
            ShortName = mainSection.GetNonEmpty("SHORT_NAME") ?? (Name.Length == 0 ? @"?" : Name.Substring(0, 1));
        }

        public TyresEntry Clone() {
            return new TyresEntry(SourceCarId, Version, MainSection, ThermalSection, WearCurveData, PerformanceCurveData, RearTyres, null);
        }

        public double Radius { get; private set; }
        public double RimRadius { get; private set; }
        public double Width { get; private set; }

        private void ReadParameters([CanBeNull] Lazy<double?> rimRadiusLazy) {
            Radius = MainSection.GetDouble("RADIUS", 0);
            RimRadius = MainSection.GetDoubleNullable("RIM_RADIUS") ?? rimRadiusLazy?.Value ?? -1;
            Width = MainSection.GetDouble("WIDTH", 0);
        }

        public string DisplayWidth => GetDisplayWidth(Width);
        public string DisplayProfile => GetDisplayProfile(Radius, RimRadius, Width);
        public string DisplayRimRadius => GetDisplayRimRadius(RimRadius);

        #region Unique stuff
        public string Footprint => _footprint ?? (_footprint = new IniFile {
            ["main"] = new IniFileSection(null, MainSection) { ["NAME"] = "", ["SHORT_NAME"] = "", ["WEAR_CURVE"] = "" },
            ["thermal"] = new IniFileSection(null, ThermalSection) { ["PERFORMANCE_CURVE"] = "" }
        }.ToString());

        private string _footprint;

        private sealed class TyresEntryEqualityComparer : IEqualityComparer<TyresEntry> {
            public bool Equals(TyresEntry x, TyresEntry y) {
                return ReferenceEquals(x, y) || !ReferenceEquals(x, null) && !ReferenceEquals(y, null) && x.GetType() == y.GetType() &&
                        string.Equals(x.WearCurveData, y.WearCurveData, StringComparison.Ordinal) &&
                        string.Equals(x.PerformanceCurveData, y.PerformanceCurveData, StringComparison.Ordinal) &&
                        string.Equals(x.Footprint, y.Footprint, StringComparison.Ordinal) &&
                        x.Version == y.Version;
            }

            public int GetHashCode(TyresEntry obj) {
                unchecked {
                    var hashCode = obj.WearCurveData?.GetHashCode() ?? 0;
                    hashCode = (hashCode * 397) ^ (obj.PerformanceCurveData?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ (obj.Footprint?.GetHashCode() ?? 0);
                    hashCode = (hashCode * 397) ^ obj.Version;
                    return hashCode;
                }
            }
        }

        public static IEqualityComparer<TyresEntry> TyresEntryComparer { get; } = new TyresEntryEqualityComparer();
        #endregion

        #region Some static methods to display values nicely
        public static double GetProfile(double radius, double rimRadius, double width) {
            return rimRadius <= 0 ? double.NaN : (radius - (rimRadius + 0.0127)) / width;
        }

        public static string GetDisplayProfile(double radius, double rimRadius, double width, bool? roundNicely = null) {
            var profile = GetProfile(radius, rimRadius, width);
            return double.IsNaN(profile) ? @"?" : (100d * profile).Round((roundNicely ?? OptionRoundNicely) ? 5d : 0.5d).ToString(CultureInfo.CurrentUICulture);
        }

        public static string GetDisplayWidth(double width, bool? roundNicely = null) {
            return (width * 1000).Round((roundNicely ?? OptionRoundNicely) ? 1d : 0.1d).ToString(CultureInfo.CurrentUICulture);
        }

        public static string GetDisplayRimRadius(double rimRadius, bool? roundNicely = null) {
            return rimRadius <= 0 || double.IsNaN(rimRadius) ? @"?" :
                    (rimRadius * 100 / 2.54 * 2 - 1).Round((roundNicely ?? OptionRoundNicely) ? 0.1d : 0.01d).ToString(CultureInfo.CurrentUICulture);
        }
        #endregion

        #region Appropriate levels
        public void SetAppropriateLevel(TyresSet original) {
            var front = GetAppropriateLevel(original.Front);
            AppropriateLevelFront = front.Item1;
            DisplayOffsetFront = front.Item2;

            var rear = GetAppropriateLevel(original.Rear);
            AppropriateLevelRear = rear.Item1;
            DisplayOffsetRear = rear.Item2;
        }

        private static string OffsetOut(double offset) {
            return $@"{(offset >= 0 ? @"+" : @"−")}{(offset * 1000d).Abs().Round()} mm";
        }

        private Tuple<TyresAppropriateLevel, string> GetAppropriateLevel(TyresEntry entry) {
            var radiusOffset = (Radius - entry.Radius).Abs() * 1000d;

            var rimRadiusOffset = entry.RimRadius <= 0d ? 0d : (RimRadius - entry.RimRadius).Abs() * 1000d;
            var widthOffset = (Width - entry.Width).Abs() * 1000d;
            var displayOffset = string.Format("• Radius: {0};\n• Rim radius: {1};\n• Width: {2}",
                    OffsetOut(Radius - entry.Radius),
                    entry.RimRadius <= 0d ? @"?" : OffsetOut(RimRadius - entry.RimRadius),
                    OffsetOut(Width - entry.Width));

            if (radiusOffset < 1 && rimRadiusOffset < 1 && widthOffset < 1) {
                return Tuple.Create(TyresAppropriateLevel.A, displayOffset);
            }

            if (radiusOffset < 3 && rimRadiusOffset < 5 && widthOffset < 12) {
                return Tuple.Create(TyresAppropriateLevel.B, displayOffset);
            }

            if (radiusOffset < 7 && rimRadiusOffset < 10 && widthOffset < 26) {
                return Tuple.Create(TyresAppropriateLevel.C, displayOffset);
            }

            if (radiusOffset < 10 && rimRadiusOffset < 16 && widthOffset < 32) {
                return Tuple.Create(TyresAppropriateLevel.D, displayOffset);
            }

            if (radiusOffset < 15 && rimRadiusOffset < 24 && widthOffset < 50) {
                return Tuple.Create(TyresAppropriateLevel.E, displayOffset);
            }

            return Tuple.Create(TyresAppropriateLevel.F, displayOffset);
        }

        private TyresAppropriateLevel _appropriateLevelFront;

        public TyresAppropriateLevel AppropriateLevelFront {
            get => _appropriateLevelFront;
            set {
                if (Equals(value, _appropriateLevelFront)) return;
                _appropriateLevelFront = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayOffsetFrontDescription));
            }
        }

        private string _displayOffsetFront;

        public string DisplayOffsetFront {
            get => _displayOffsetFront;
            set {
                if (Equals(value, _displayOffsetFront)) return;
                _displayOffsetFront = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayOffsetFrontDescription));
            }
        }

        public string DisplayOffsetFrontDescription => AppropriateLevelFront.GetDescription() + ':' + Environment.NewLine + DisplayOffsetFront;

        private TyresAppropriateLevel _appropriateLevelRear;

        public TyresAppropriateLevel AppropriateLevelRear {
            get => _appropriateLevelRear;
            set {
                if (Equals(value, _appropriateLevelRear)) return;
                _appropriateLevelRear = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayOffsetRearDescription));
            }
        }

        private string _displayOffsetRear;

        public string DisplayOffsetRear {
            get => _displayOffsetRear;
            set {
                if (Equals(value, _displayOffsetRear)) return;
                _displayOffsetRear = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayOffsetRearDescription));
            }
        }

        public string DisplayOffsetRearDescription => AppropriateLevelRear.GetDescription() + ':' + Environment.NewLine + DisplayOffsetRear;
        #endregion

        #region Draggable
        public const string DraggableFormat = "X-TyresEntry";

        string IDraggable.DraggableFormat => DraggableFormat;
        #endregion

        #region Create
        [CanBeNull]
        public static TyresEntry Create(CarObject car, string id, bool ignoreDamaged) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.ContainsKey(id) != true) return null;

            var section = tyres[id];
            var thermal = tyres[Regex.Replace(id, @"(?=FRONT|REAR)", @"THERMAL_")];

            var wearCurve = section.GetNonEmpty("WEAR_CURVE");
            var performanceCurve = thermal.GetNonEmpty("PERFORMANCE_CURVE");
            if (ignoreDamaged && (section.GetNonEmpty("NAME") == null || wearCurve == null || performanceCurve == null)) return null;

            var sourceId = section.GetNonEmpty("__CM_SOURCE_ID") ?? car.Id;
            var rear = id.Contains(@"REAR");
            return new TyresEntry(sourceId, tyres["HEADER"].GetInt("VERSION", -1), section, thermal,
                    wearCurve == null ? null : car.AcdData.GetRawFile(wearCurve).Content,
                    performanceCurve == null ? null : car.AcdData.GetRawFile(performanceCurve).Content,
                    rear, GetRimRadiusLazy(tyres, rear));
        }

        private static Lazy<double?> GetRimRadiusLazy(IniFile tyresIni, bool rear) {
            return new Lazy<double?>(() => tyresIni.GetSections(rear ? @"REAR" : @"FRONT", -1)
                                                   .Select(x => x.GetDoubleNullable("RIM_RADIUS")).FirstOrDefault(x => x != null));
        }

        [CanBeNull]
        public static TyresEntry CreateFront(CarObject car, int index, bool ignoreDamaged) {
            return Create(car, IniFile.GetSectionNames(@"FRONT", -1).Skip(index).First(), ignoreDamaged);
        }

        [CanBeNull]
        public static TyresEntry CreateRear(CarObject car, int index, bool ignoreDamaged) {
            return Create(car, IniFile.GetSectionNames(@"REAR", -1).Skip(index).First(), ignoreDamaged);
        }
        #endregion

        public static IEnumerable<Tuple<TyresEntry, TyresEntry>> GetTyres(CarObject car) {
            var tyres = car.AcdData?.GetIniFile("tyres.ini");
            if (tyres?.IsEmptyOrDamaged() != false) yield break;

            for (var i = 0; i < 9999; i++) {
                var front = CreateFront(car, i, false);
                var rear = CreateRear(car, i, false);

                if (front == null && rear == null) break;

                if (front != null) front.OtherEntry = rear;
                if (rear != null) rear.OtherEntry = front;
                yield return Tuple.Create(front, rear);
            }
        }

        [ItemCanBeNull]
        public static async Task<List<TyresEntry>> GetList(string filter, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var filterObj = Filter.Create(CarObjectTester.Instance, filter);

            var wrappers = CarsManager.Instance.WrappersList.ToList();
            var list = new List<TyresEntry>(wrappers.Count);

            for (var i = 0; i < wrappers.Count; i++) {
                if (cancellation.IsCancellationRequested) return null;

                var wrapper = wrappers[i];
                var car = (CarObject)await wrapper.LoadedAsync();
                progress?.Report(car.DisplayName, i, wrappers.Count);

                if (!filterObj.Test(car) || car.AcdData == null) continue;
                var tyres = car.AcdData.GetIniFile("tyres.ini");

                var version = tyres["HEADER"].GetInt("VERSION", -1);
                if (version < 4) continue;

                foreach (var tuple in GetTyres(car)) {
                    if (list.Contains(tuple.Item1, TyresEntryComparer)) {
                        if (!list.Contains(tuple.Item2, TyresEntryComparer)) {
                            list.Add(tuple.Item2);
                        }
                    } else {
                        if (TyresEntryComparer.Equals(tuple.Item1, tuple.Item2)) {
                            list.Add(tuple.Item1);
                            tuple.Item1.BothTyres = true;
                        } else if (!list.Contains(tuple.Item2, TyresEntryComparer)) {
                            list.Add(tuple.Item1);
                            list.Add(tuple.Item2);
                        }
                    }
                }

                if (i % 3 == 0) {
                    await Task.Yield();
                }
            }

            return list;
        }

        [ItemCanBeNull]
        public static async Task<List<TyresEntry>> GetList(string filter) {
            using (var waiting = new WaitingDialog("Getting a list of tyres…")) {
                return await GetList(filter, waiting, waiting.CancellationToken);
            }
        }
    }
}