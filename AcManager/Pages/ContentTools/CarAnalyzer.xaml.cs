using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.ContentRepair;
using AcManager.Controls.Dialogs;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.ContentRepairUi;
using AcManager.Tools.Filters.Testers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataAnalyzer;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.ContentTools {
    public partial class CarAnalyzer {
        public static double OptionSimilarThreshold = 0.95;
        public static string[] OptionSimilarAdditionalSourceIds = null;

        static CarAnalyzer() {
            CarRepair.AddType<CarObsoleteTyresRepair>();
            CarRepair.AddType<CarWeightRepair>();
            CarRepair.AddType<CarTorqueRepair>();
            CarRepair.AddType<CarWronglyTakenSoundRepair>();
            CarRepair.AddType<CarObsoleteSoundRepair>();
        }

        public class BrokenDetails : NotifyPropertyChanged {
            public CarObject Car { get; }

            public ChangeableObservableCollection<ContentRepairSuggestion> Aspects { get; }

            private int _leftUnsolved;

            public int LeftUnsolved {
                get => _leftUnsolved;
                set => Apply(value, ref _leftUnsolved);
            }

            public BrokenDetails(CarObject car, IEnumerable<ContentRepairSuggestion> aspects, bool ratingMode) {
                Car = car;
                RatingMode = ratingMode;
                Aspects = new ChangeableObservableCollection<ContentRepairSuggestion>(aspects);
                LeftUnsolved = Aspects.Count;

                Aspects.ItemPropertyChanged += OnItemPropertyChanged;
            }

            private void UpdateLeftUnsolved() {
                Aspects.ReplaceIfDifferBy(Aspects.Where(x => !x.IsHidden));
                LeftUnsolved = Aspects.Count(x => !x.IsSolved && !x.IsHidden);
            }

            private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(ContentRepairSuggestion.IsSolved):
                    case nameof(ContentRepairSuggestion.IsHidden):
                        UpdateLeftUnsolved();
                        break;
                }
            }

            private AsyncCommand _reloadCommand;

            public AsyncCommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new AsyncCommand(async () => {
                try {
                    var list = await Task.Run(() => CarRepair.GetRepairSuggestions(Car, true).ToList());
                    Aspects.ReplaceEverythingBy(list);
                    UpdateLeftUnsolved();
                } catch (Exception e) {
                    NonfatalError.Notify($"Can’t check {Car.DisplayName}", e);
                }
            }));

            private CommandBase _replaceSoundCommand;

            public ICommand ReplaceSoundCommand => _replaceSoundCommand ?? (_replaceSoundCommand = new AsyncCommand(() => {
                var donor = SelectCarDialog.Show();
                return donor == null ? Task.Delay(0) : Car.ReplaceSound(donor);
            }));

            #region Open In Showroom
            private DelegateCommand _openInShowroomCommand;

            public DelegateCommand OpenInShowroomCommand => _openInShowroomCommand ?? (_openInShowroomCommand = new DelegateCommand(() => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
                    OpenInCustomShowroomCommand.ExecuteAsync().Ignore();
                    return;
                }

                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !CarOpenInShowroomDialog.Run(Car, Car.SelectedSkin?.Id)) {
                    OpenInShowroomOptionsCommand.Execute();
                }
            }, () => Car.Enabled && Car.SelectedSkin != null));

            private DelegateCommand _openInShowroomOptionsCommand;

            public DelegateCommand OpenInShowroomOptionsCommand => _openInShowroomOptionsCommand ?? (_openInShowroomOptionsCommand = new DelegateCommand(() => {
                new CarOpenInShowroomDialog(Car, Car.SelectedSkin?.Id).ShowDialog();
            }, () => Car.Enabled && Car.SelectedSkin != null));

            private AsyncCommand _openInCustomShowroomCommand;

            public AsyncCommand OpenInCustomShowroomCommand => _openInCustomShowroomCommand ??
                    (_openInCustomShowroomCommand = new AsyncCommand(() => CustomShowroomWrapper.StartAsync(Car, Car.SelectedSkin)));

            private AsyncCommand _driveCommand;

            public AsyncCommand DriveCommand => _driveCommand ?? (_driveCommand = new AsyncCommand(async () => {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ||
                        !await QuickDrive.RunAsync(Car, Car.SelectedSkin?.Id)) {
                    DriveOptionsCommand.Execute();
                }
            }, () => Car.Enabled));

            private DelegateCommand _driveOptionsCommand;

            public DelegateCommand DriveOptionsCommand => _driveOptionsCommand ?? (_driveOptionsCommand = new DelegateCommand(() => {
                QuickDrive.Show(Car, Car.SelectedSkin?.Id);
            }, () => Car.Enabled));
            #endregion

            #region Rating
            public bool RatingMode { get; }

            private bool _ratingLoading;

            public bool RatingLoading {
                get => _ratingLoading;
                set => Apply(value, ref _ratingLoading);
            }

            public class SimularEntry {
                public SimularEntry(HashStorage.Simular simular, CarObject car, double interest) {
                    Simular = simular;
                    Car = car;
                    Interest = interest;
                }

                public HashStorage.Simular Simular {get;}

                public CarObject Car { get; }

                public double Interest { get; }
            }

            [ItemCanBeNull]
            private static async Task<SimularEntry> Wrap(CarObject car, HashStorage.Simular baseSimular) {
                var found = await CarsManager.Instance.GetByIdAsync(baseSimular.CarId);
                if (found == null) return null;

                var parent = car.Parent ?? car;
                if (parent == (found.Parent ?? found)) {
                    // let’s just ignore cases when cars are in the same group
                    return null;
                }

                var interest = baseSimular.Value;

                if (found.Author != car.Author) {
                    // different authors, but the same suspension? hmm…
                    interest += 1d;
                }

                if (found.Brand != car.Brand) {
                    // usually, manufacturer reuses params, but shares it rarely
                    interest += 3d;
                }

                if (found.Country != car.Country) {
                    // I think, shared params might be more popular in bounds of one country?
                    interest += 0.5;
                }

                if (found.CarClass != car.CarClass) {
                    // and definitely in bounds of one class
                    interest += 0.5;
                }

                if (found.Year.HasValue && car.Year.HasValue) {
                    // not sure if somebody would reuse values for too ling
                    var ageDifference = (found.Year.Value - car.Year.Value).Abs();
                    interest += ageDifference / 10d;
                }

                return new SimularEntry(baseSimular, found, interest);
            }

            private static async Task<List<SimularEntry>> FindSimilar(CarObject car, RulesWrapper rules, string id, double threshold) {
                return (await rules.FindSimular(car.AcdData, id, false, threshold).Select(x => Wrap(car, x)).WhenAll(5).ConfigureAwait(false))
                        .NonNull().OrderByDescending(x => x.Interest).ToList();
            }

            [ItemNotNull]
            private static async Task<IReadOnlyList<RatingEntry>> FindSimilarEngine(CarObject car, RulesWrapper rules) {
                var similar = await FindSimilar(car, rules, "engine", OptionSimilarThreshold);
                if (similar.Count <= 0) return new RatingEntry[0];

                return new[] {
                    new RatingEntry(
                            $"Engine is similar to {similar[0].Car}",
                            $"{similar[0].Simular.Value * 100:F1}% match.", null,
                            () => {
                                return $@"It doesn’t really mean anything… Just a bit of information.

All found similarities:
{similar.Select(x => $" • {x.Car.DisplayName}: {x.Simular.Value * 100:F1}%").JoinToString(";\n")}.";
                            })
                };
            }

            [ItemNotNull]
            private static async Task<IReadOnlyList<RatingEntry>> FindSimilarTurbo(CarObject car, RulesWrapper rules) {
                if (car.AcdData?.GetIniFile("engine.ini").ContainsKey("TURBO_0") != true) return new RatingEntry[0];

                var similar = await FindSimilar(car, rules, "turbo", OptionSimilarThreshold);
                if (similar.Count <= 0) return new RatingEntry[0];

                return new[] {
                    new RatingEntry(
                            $"Turbo is similar to {similar[0].Car}",
                            $"{similar[0].Simular.Value * 100:F1}% match.", null,
                            () => {
                                return $@"It doesn’t really mean anything… Just a bit of information.

All found similarities:
{similar.Select(x => $" • {x.Car.DisplayName}: {x.Simular.Value * 100:F1}%").JoinToString(";\n")}.";
                            })
                };
            }

            [ItemNotNull]
            private static async Task<IReadOnlyList<RatingEntry>> FindSimilarGearbox(CarObject car, RulesWrapper rules) {
                var similar = await FindSimilar(car, rules, "gearbox", OptionSimilarThreshold);
                if (similar.Count <= 0) return new RatingEntry[0];

                return new[] {
                    new RatingEntry(
                            $"Gear ratios are similar to {similar[0].Car}",
                            $"{similar[0].Simular.Value * 100:F1}% match.", null,
                            () => {
                                return $@"Why not?

All found similarities:
{similar.Select(x => $" • {x.Car.DisplayName}: {x.Simular.Value * 100:F1}%").JoinToString(";\n")}.";
                            })
                };
            }

            [ItemNotNull]
            private static async Task<IReadOnlyList<RatingEntry>> FindSimilarAero(CarObject car, RulesWrapper rules) {
                var similar = await FindSimilar(car, rules, "aero", OptionSimilarThreshold);
                if (similar.Count <= 0) return new RatingEntry[0];

                return new[] {
                    new RatingEntry(
                            $"Aerodynamic properties are similar to {similar[0].Car}",
                            $"{similar[0].Simular.Value * 100:F1}% match.", null,
                            () => {
                                return $@"It might be difficult to calculate those values from scratch for every model.

All found similarities:
{similar.Select(x => $" • {x.Car.DisplayName}: {x.Simular.Value * 100:F1}%").JoinToString(";\n")}.";
                            })
                };
            }

            [ItemNotNull]
            private static async Task<IReadOnlyList<RatingEntry>> FindSimilarSuspension(CarObject car, RulesWrapper rules) {
                var result = new List<RatingEntry>();

                var similarFront = await FindSimilar(car, rules, "suspension_front", OptionSimilarThreshold);
                var similarRear = await FindSimilar(car, rules, "suspension_rear", OptionSimilarThreshold);

                var frontGeometry = similarFront.Count == 0;
                if (frontGeometry) {
                    similarFront = await FindSimilar(car, rules, "suspension_front_geometry", OptionSimilarThreshold);
                }

                var rearGeometry = similarRear.Count == 0;
                if (rearGeometry) {
                    similarRear = await FindSimilar(car, rules, "suspension_rear_geometry", OptionSimilarThreshold);
                }

                if (frontGeometry == rearGeometry) {
                    var zipList = similarFront.Select(x => new {
                        Front = x,
                        Rear = similarRear.FirstOrDefault(y => y.Car == x.Car)
                    }).Where(x => x.Rear != null).OrderBy(x => x.Front.Interest + x.Rear.Interest).ToList();
                    var zip = zipList.FirstOrDefault();

                    if (zip != null &&
                            zip.Front.Interest + zip.Rear.Interest > similarFront[0].Interest &&
                            zip.Front.Interest + zip.Rear.Interest > similarRear[0].Interest) {
                        return new[] {
                            new RatingEntry(
                                    frontGeometry ? $"Suspension geometry is similar to {zip.Front.Car}" : $"Suspension is similar to {zip.Front.Car}",
                                    $"{(zip.Front.Simular.Value + zip.Rear.Simular.Value) * 50d:F1}% match.", null, () => {
                                        return $@"Of course, it’s not always a bad thing. A lot of cars are built on the same chassis.

All found similarities:
{zipList.Select(x => $" • {x.Front.Car.DisplayName}: {(x.Front.Simular.Value + x.Rear.Simular.Value) * 50:F1}%").JoinToString(";\n")}.";
                                    })
                        };
                    }
                }

                if (similarFront.Count > 0) {
                    result.Add(new RatingEntry(
                            frontGeometry ? $"Front suspension geometry is similar to {similarFront[0].Car}" : $"Front suspension is similar to {similarFront[0].Car}",
                            $"{similarFront[0].Simular.Value * 100:F1}% match.", null,
                            () => {
                                return $@"Of course, it’s not always a bad thing. A lot of cars are built on the same chassis.

All found similarities:
{similarFront.Select(x => $" • {x.Car.DisplayName}: {x.Simular.Value * 100:F1}%").JoinToString(";\n")}.";
                            }));
                }

                if (similarRear.Count > 0) {
                    result.Add(new RatingEntry(
                            rearGeometry ? $"Rear suspension geometry is similar to {similarRear[0].Car}" : $"Rear suspension is similar to {similarRear[0].Car}",
                            $"{similarRear[0].Simular.Value * 100:F1}% match.", null,
                            () => {
                                return $@"Of course, it’s not always a bad thing. A lot of cars are built on the same chassis.

All found similarities:
{similarRear.Select(x => $" • {x.Car.DisplayName}: {x.Simular.Value * 100:F1}%").JoinToString(";\n")}.";
                            }));
                }

                return result;
            }

            [ItemNotNull]
            private static async Task<IReadOnlyList<RatingEntry>> AnalyzeLods(CarObject car, DataWrapper data, Kn5 kn5, int trisCount) {
                var result = new List<RatingEntry>();

                var carData = new CarData(data);
                var lods = carData.GetLods().ToList();

                if (lods.Count <= 1) {
                    result.Add(new RatingEntry("Only LOD A found", "Don’t put too many cars in the race if you want smooth 60 FPS and don’t have the best PC.",
                            2d - ((trisCount - 100e3) / 200e3).Clamp(0, 2).Round(0.5)));
                } else {
                    try {
                        var lodsFilenames = lods.Select(x => Path.Combine(car.Location, x.FileName)).ToList();
                        var lodsKn5 = await Task.Run(() => lodsFilenames.Select((x, i) => i == 0 ? kn5 : Kn5.FromFile(x)).ToList());

                        // 0–5: 1 from start; +1 if there are no textures in LODs, +1 if there are four of them
                        // (±1 → −0.3), +2 for triangles amount
                        var lodsRate = 1d;

                        // +0.5 if there are no textures in LODs
                        var noExtraTextures = lodsKn5.Skip(1).All(x => x.TexturesData.Count == 0);
                        if (noExtraTextures) {
                            lodsRate += 0.5;
                        }

                        // +0.5 if distances are correct
                        var correctDistances = (lods[0].Out - 15f).Abs() <= 5.1f &&
                                (lods.Count < 3 || 25f < lods[1].Out && lods[1].Out < 60f) &&
                                (lods.Count < 4 || 60f < lods[2].Out && lods[2].Out < 250f);
                        if (correctDistances) {
                            lodsRate += 0.5;
                        }

                        // +0.5 if there are four of them (±1 → −0.2)
                        lodsRate += (0.5d - (lodsKn5.Count - 4).Abs() * 0.2).Saturate();

                        // +1.5 (or +2 in case of really good LODs) for triangles amount
                        var lodsTris = lodsKn5.Select(x => x.RootNode.TotalTrianglesCount).ToList();
                        var correctLods = lodsTris[1] <= 45e3 &&
                                (lods.Count < 3 || lodsTris[2] <= 11e3) &&
                                (lods.Count < 4 || lodsTris[3] <= 5e3);
                        var superbLods = lodsTris[1] <= 22e3 &&
                                (lods.Count < 3 || lodsTris[2] <= 6e3) &&
                                (lods.Count < 4 || lodsTris[3] <= 3e3);
                        if (correctLods) {
                            lodsRate += superbLods ? 2d : 1.5;
                        }

                        // +1 for valid COCKPIT_LR, +0.5 for not quite valid
                        var cockpitHrNode = kn5.RootNode.GetByName("COCKPIT_HR");
                        var cockpitLrNode = kn5.RootNode.GetByName("COCKPIT_LR");
                        var cockpitSwitch = data.GetIniFile("lods.ini")["COCKPIT_HR"].GetFloat("DISTANCE_SWITCH", 0f);
                        var cockpitLrValid = 0;

                        if (cockpitHrNode == null || cockpitHrNode.TotalTrianglesCount < 20e3) {
                            cockpitLrValid = 2;
                            lodsRate += 1d;
                        } else {
                            if (cockpitLrNode != null && cockpitSwitch > 0f && cockpitSwitch < 50f) {
                                lodsRate += 0.5;
                                cockpitLrValid = 1;
                                if ((cockpitSwitch - 7f).Abs() < 3f && cockpitLrNode.TotalTrianglesCount < 10e3) {
                                    lodsRate += 0.5;
                                    cockpitLrValid = 2;
                                }
                            }
                        }

                        result.Add(new RatingEntry(
                                cockpitLrValid == 0 ? $"{lods.Count} LODs" : $"{lods.Count} LODs and low-resolution cockpit",
                                "Recommended: 4 LODs and low-resolution cockpit", lodsRate,
                                !noExtraTextures || !correctDistances || cockpitLrValid != 2 || !superbLods ? () => {
                                    return new[] {
                                        noExtraTextures ? null
                                                : "Please, don’t use additional textures in LODs to keep VRAM consumption low",
                                        correctDistances
                                                ? null
                                                : $"LODs switching distances: {lods.Take(lods.Count - 1).Select(x => $"{x.Out:F0} m").JoinToString(", ")} (recommended: 15 m, 45 m and 200 m)",
                                        cockpitLrValid == 0 ? "Don’t forget about COCKPIT_LR switching on 7 m" : cockpitLrValid == 1
                                                ? $"COCKPIT_LR tris: {cockpitLrNode?.TotalTrianglesCount / 1e3:F0}K, switching at {cockpitSwitch:F0} m (recommended: 5K, 7 m)"
                                                : null,
                                        superbLods
                                                ? null
                                                : $"Triangles per LODs: {lodsTris.Skip(1).Select(x => $"{x / 1e3:F0}K").JoinToString(", ")} (recommended: 20K, 5K, 2K)",
                                    }.NonNull().Select(x => $"• {x};").JoinToString("\n").ApartFromLast(";") +
                                            ".\n\nMore information about LODs and recommended complexity you can find in the car pipeline by Kunos.";
                                }
                                        : (Func<string>)null));
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }
                }

                return result;
            }

            private static async Task<long> FindAverageLargeSkinSize(CarObject car, Kn5 kn5) {
                await car.SkinsManager.EnsureLoadedAsync();
                var directories = car.EnabledOnlySkins.Select(x => x.Location).ToList();
                var sizes = await Task.Run(() => directories.Select(x =>
                        new DirectoryInfo(x).GetFiles().Where(y => y.Name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) ||
                                kn5.Textures.Keys.Any(z => string.Equals(z, y.Name, StringComparison.OrdinalIgnoreCase))).Sum(y => y.Length)).ToList());
                sizes.Sort();
                return sizes.Skip(sizes.Count / 2).Sum() / (sizes.Count - sizes.Count / 2);
            }

            private bool _collectingRatings;

            private async Task CollectRatings() {
                if (_collectingRatings) return;
                _collectingRatings = true;

                RatingLoading = true;

                try {
                    var rules = CarDataComparing.GetRules(OptionSimilarAdditionalSourceIds);
                    await rules.EnsureActualAsync();

                    var result = new List<RatingEntry>();
                    if (Car.Author != AcCommonObject.AuthorKunos) {
                        result.AddRange(await FindSimilarSuspension(Car, rules));
                        result.AddRange(await FindSimilarAero(Car, rules));
                        result.AddRange(await FindSimilarEngine(Car, rules));
                        result.AddRange(await FindSimilarTurbo(Car, rules));
                        result.AddRange(await FindSimilarGearbox(Car, rules));
                    }

                    var data = Car.AcdData;
                    if (data != null) {
                        var kn5Filename = AcPaths.GetMainCarFilename(Car.Location, data, false);
                        var kn5 = await Task.Run(() => Kn5.FromFile(kn5Filename));

                        // triangles count
                        var trisCount = kn5.RootNode.TotalTrianglesCount;

                        {
                            var trisRate = trisCount < 160e3 ? 5.5 :
                                    (5d - (trisCount - 350e3) / 100e3).Clamp(1d, 5d).Round(0.5);
                            result.Add(new RatingEntry($"{trisCount / 1000d:F0}K triangles in LOD A", "Recommended amount: 150K–350K.", trisRate));
                        }

                        // LODs
                        if (trisCount > 100e3) {
                            result.AddRange(await AnalyzeLods(Car, data, kn5, trisCount));
                        }

                        // KN5 textures
                        var wrongFormat = kn5.TexturesData.Any(x => x.Value.Length > 320e3 &&
                                Regex.IsMatch(x.Key, @"\.(?:jpe?g|png|bmp|gif)$", RegexOptions.IgnoreCase));
                        var texturesWeight = kn5.TexturesData.Values.Sum(x => x.Length);

                        {
                            var texturesRate = texturesWeight < 30e6 ? 5.5 :
                                    ((5d - (texturesWeight - 50e6) / 20e6) * (wrongFormat ? 0.5 : 1d)).Clamp(1d, 5d).Round(0.5);
                            result.Add(new RatingEntry(
                                    wrongFormat ?
                                            $"{texturesWeight / 1e6:F0} MB of textures, probably not properly compressed" :
                                            $"{texturesWeight / 1e6:F0} MB of textures, properly compressed",
                                    wrongFormat
                                            ? "Recommended amount: 20–50 MB. As for format, for anything big, please, use only DDS."
                                            : "Recommended amount: 20–50 MB.",
                                    texturesRate));
                        }

                        // skins textures
                        var skinSize = await FindAverageLargeSkinSize(Car, kn5);
                        {
                            var skinSizeRate = skinSize < 2e6 ? 5.5 :
                                    (5d - (skinSize - 20e6) / 20e6).Clamp(1d, 5d).Round(0.5);
                            result.Add(new RatingEntry(
                                    $"Avg. skin size: {skinSize / 1e6:F0} MB",
                                    "Recommended size: up to 20 MB.",
                                    skinSizeRate));
                        }

                        Ratings = result;
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }

                RatingLoading = false;
            }

            private List<RatingEntry> _ratings;

            public List<RatingEntry> Ratings {
                get {
                    if (_ratings == null) {
                        CollectRatings().Forget();
                    }

                    return _ratings;
                }
                set => Apply(value, ref _ratings);
            }
            #endregion
        }

        public class RatingEntry {
            public RatingEntry(string message, string details, double? rate, Func<string> informationMsg = null) {
                Message = message;
                Rate = rate;
                InformationMsg = informationMsg;
                Details = details;
            }

            public string Message { get; }

            public string Details { get; }

            public double? Rate { get; }

            [CanBeNull]
            public Func<string> InformationMsg { get; }

            private DelegateCommand _showInformationCommand;

            public DelegateCommand ShowInformationCommand => _showInformationCommand ?? (_showInformationCommand = new DelegateCommand(() => {
                ModernDialog.ShowMessage(InformationMsg?.Invoke() ?? "?", Message, MessageBoxButton.OK);
            }, () => InformationMsg != null));

            public bool HasRate => Rate.HasValue;
        }

        #region Loading
        private bool _models, _rating;
        private string _filter, _id;

        protected override void InitializeOverride(Uri uri) {
            _models = uri.GetQueryParamBool("Models");
            _rating = uri.GetQueryParamBool("Rating") && SettingsHolder.Content.RateCars;
            _filter = uri.GetQueryParam("Filter");
            _id = uri.GetQueryParam("Id");

            InitializeComponent();
        }

        [CanBeNull]
        private static BrokenDetails GetDetails(CarObject car, bool models, bool allowEmpty, bool ratingMode) {
            if (car.AcdData?.IsEmpty != false) return null;

            var list = CarRepair.GetRepairSuggestions(car, models).ToList();
            return allowEmpty || list.Count > 0 ? new BrokenDetails(car, list, ratingMode) : null;
        }

        protected override async Task<bool> LoadAsyncOverride(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (_id != null) {
                var car = CarsManager.Instance.GetById(_id);
                if (car != null) {
                    progress.Report(AsyncProgressEntry.Indetermitate);
                    BrokenCars = new List<BrokenDetails> {
                        await Task.Run(() => GetDetails(car, _models, true, _rating))
                    };
                } else {
                    BrokenCars = new List<BrokenDetails>();
                }
            } else {
                var entries = new List<BrokenDetails>();
                var filter = _filter == null ? null : Filter.Create(CarObjectTester.Instance, _filter);

                progress.Report(AsyncProgressEntry.FromStringIndetermitate("Loading cars…"));
                await CarsManager.Instance.EnsureLoadedAsync();

                IEnumerable<CarObject> carsEnumerable = CarsManager.Instance.Loaded.OrderBy(x => x.Name);
                if (filter != null) {
                    carsEnumerable = carsEnumerable.Where(filter.Test);
                }

                var cars = carsEnumerable.ToList();
                for (var i = 0; i < cars.Count; i++) {
                    var car = cars[i];
                    progress.Report(new AsyncProgressEntry(car.Name, i, cars.Count));

                    try {
                        var details = await Task.Run(() => GetDetails(car, _models, false, _rating));
                        if (details != null) {
                            entries.Add(details);
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify($"Can’t check {car.DisplayName}", e);
                    }
                }

                BrokenCars = entries;
            }

            return BrokenCars.Count > 0;
        }
        #endregion

        #region Entries
        private List<BrokenDetails> _brokenCars;

        public List<BrokenDetails> BrokenCars {
            get => _brokenCars;
            set {
                if (Equals(value, _brokenCars)) return;
                _brokenCars = value;
                OnPropertyChanged();

                BrokenCar = value?.FirstOrDefault();
            }
        }

        private BrokenDetails _brokenCar;

        public BrokenDetails BrokenCar {
            get => _brokenCar;
            set => this.Apply(value, ref _brokenCar);
        }
        #endregion

        private bool _warned;

        private void OnFixButtonClick(object sender, RoutedEventArgs e) {
            if (((FrameworkElement)sender).DataContext is ContentRepairSuggestionFix aspect) {
                if (aspect.AffectsData && !_warned) {
                    if (!DataUpdateWarning.Warn(BrokenCar.Car)) return;
                    _warned = true;
                }

                aspect.FixCommand.ExecuteAsync().Forget();
            }
        }

        [ValueConversion(typeof(double?), typeof(SolidColorBrush))]
        private class RatingToColorConverterInner : IValueConverter {
            private static Color GetColor(double? level) {
                if (!level.HasValue) return Colors.DarkCyan;
                if (level >= 4.8d) return Colors.LimeGreen;
                if (level >= 3.8d) return Colors.Yellow;
                if (level >= 2.8d) return Colors.Orange;
                if (level >= 1.8d) return Colors.OrangeRed;
                if (level >= 0.8d) return Colors.Red;
                return Colors.SaddleBrown;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return new SolidColorBrush(GetColor(value as double?));
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        [ValueConversion(typeof(double?), typeof(string))]
        private class RatingToTextConverterInner : IValueConverter {
            private static string GetRating(double? level) {
                if (!level.HasValue) return "i";

                if (level >= 5.5d) return "A+";
                if (level >= 5d) return "A";
                if (level >= 4.8d) return "A−";

                if (level >= 4.5d) return "B+";
                if (level >= 4d) return "B";
                if (level >= 3.8d) return "B−";

                if (level >= 3.5d) return "C+";
                if (level >= 3d) return "C";
                if (level >= 2.8d) return "C−";

                if (level >= 2.5d) return "D+";
                if (level >= 2d) return "D";
                if (level >= 1.8d) return "D−";

                if (level >= 1.5d) return "E+";
                if (level >= 1d) return "E";
                if (level >= 0.8d) return "E−";

                if (level >= 0.2d) return "F";
                return "F−";
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return GetRating(value as double?);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter RatingToColorConverter { get; } = new RatingToColorConverterInner();
        public static IValueConverter RatingToTextConverter { get; } = new RatingToTextConverterInner();

        #region As a separate tool
        private static WeakReference<ModernDialog> _analyzerDialog;

        public static void Run([NotNull] CarObject car) {
            if (car == null) throw new ArgumentNullException(nameof(car));

            if (_analyzerDialog != null && _analyzerDialog.TryGetTarget(out ModernDialog dialog)) {
                dialog.Close();
            }

            dialog = new ModernDialog {
                ShowTitle = false,
                Title = "Analyzer",
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = @"lsMigrationHelper",
                MinWidth = 800,
                MinHeight = 480,
                Width = 800,
                Height = 640,
                MaxWidth = 99999,
                MaxHeight = 99999,
                Content = new ModernFrame {
                    Source = UriExtension.Create("/Pages/ContentTools/CarAnalyzer.xaml?Id={0}&Models=True&Rating=True", car.Id)
                }
            };

            dialog.Show();
            _analyzerDialog = new WeakReference<ModernDialog>(dialog);
        }
        #endregion
    }
}
