﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;
using AcManager.PaintShop;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.CustomShowroom {
    public partial class LiteShowroomTools {
        public partial class ViewModel : INotifyDataErrorInfo {
            #region Skin
            private void DisposeSkinItems() {
                Renderer?.SetCurrentSkinActive(true);
                Renderer?.DisposePaintShop();
                FilesStorage.Instance.Watcher(ContentCategory.PaintShop).Update -= OnPaintShopDataChanged;
                FilesStorage.Instance.Watcher(ContentCategory.LicensePlates).Update -= OnLicensePlatesChanged;
                SkinItems = null;
            }

            [ItemCanBeNull]
            private async Task<List<PaintableItem>> GetSkinItems(CancellationToken cancellation) {
                // PaintShopRulesLoader = new PaintShopRulesLoader();

                await Task.Delay(50);
                if (cancellation.IsCancellationRequested || Skin == null) return null;

                var skinItems = (await PaintShop.PaintShop.GetPaintableItemsAsync(Car.Id, Renderer?.MainSlot.Kn5, cancellation))?.ToList();
                if (skinItems == null || cancellation.IsCancellationRequested) return null;

                try {
                    var skin = Path.Combine(Skin.Location, "cm_skin.json");
                    await Task.Run(() => {
                        if (File.Exists(skin)) {
                            var jObj = JObject.Parse(File.ReadAllText(skin));
                            foreach (var pair in jObj) {
                                if (pair.Value.Type != JTokenType.Object) continue;
                                skinItems.FirstOrDefault(x => PaintShop.PaintShop.NameToId(x.DisplayName, false) == pair.Key)?
                                        .Deserialize((JObject)pair.Value);
                            }
                        }
                    });
                } catch (Exception e) {
                    Logging.Error(e);
                }

                return skinItems;
            }

            #region Errors
            public IEnumerable GetErrors(string propertyName) {
                switch (propertyName) {
                    case nameof(SaveAsSkinId):
                        if (!SaveAsNewSkin) return null;

                        var skinId = SaveAsSkinId;
                        if (string.IsNullOrWhiteSpace(skinId)) return null;
                        if (Path.GetInvalidFileNameChars().Any(x => skinId.IndexOf(x) != -1) ||
                                Regex.IsMatch(skinId, @"[^\.\w-]")) return new[] { "Please, use only letters, digits, underscores, hyphens or dots" };
                        if (FileUtils.Exists(Path.Combine(Car.SkinsDirectory, skinId))) return new[] { "Place is taken" };
                        return null;
                    default:
                        return null;
                }
            }

            public bool HasErrors => GetErrors(nameof(SaveAsSkinId)) != null;
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
            #endregion

            private static PluginsRequirement _requirement;
            private static PluginsRequirement _requirementMontage;

            public PluginsRequirement PaintShopRequirement => _requirement ?? (_requirement = new PluginsRequirement(KnownPlugins.Magick));
            public PluginsRequirement MontageRequirement => _requirementMontage ?? (_requirementMontage = new PluginsRequirement(KnownPlugins.ImageMontage));

            public Lazier<bool> PaintShopSupported { get; }

            private AsyncCommand<CancellationToken?> _paintShopCommand;

            public AsyncCommand<CancellationToken?> PaintShopCommand => _paintShopCommand ?? (_paintShopCommand =
                    new AsyncCommand<CancellationToken?>(async c => {
                        if (SkinItems == null) {
                            var skinItems = await GetSkinItems(c ?? default(CancellationToken));
                            if (c?.IsCancellationRequested == true || skinItems == null || SkinItems != null) return;

                            SkinItems = skinItems;
                            UpdateLicensePlatesStyles();
                            FilesStorage.Instance.Watcher(ContentCategory.PaintShop).Update += OnPaintShopDataChanged;
                            FilesStorage.Instance.Watcher(ContentCategory.LicensePlates).Update += OnLicensePlatesChanged;
                            SkinFlagCountry = SettingsHolder.Drive.PlayerNationality;
                        }

                        Mode = Mode.Skin;
                        SkinNumber = Skin.SkinNumber.As(1);
                        SaveAsSkinIdSuggested = Path.GetFileName(FileUtils.EnsureUnique(Path.Combine(Car.SkinsDirectory, "generated")));
                    }));

            private IList<PaintableItem> _skinItems;

            [CanBeNull]
            public IList<PaintableItem> SkinItems {
                get => _skinItems;
                set {
                    if (Equals(value, _skinItems)) return;

                    if (_skinItems != null) {
                        foreach (var item in _skinItems) {
                            item.PropertyChanged -= OnSkinItemPropertyChanged;
                            item.Dispose();
                            item.SetRenderer(null);
                        }
                    }

                    _skinItems = value;
                    OnPropertyChanged();
                    _skinSaveCommand?.RaiseCanExecuteChanged();

                    if (_skinItems != null) {
                        foreach (var item in _skinItems) {
                            item.SetRenderer(Renderer);
                            item.PropertyChanged += OnSkinItemPropertyChanged;
                        }
                    }

                    SetSkinNumber();
                    SetSkinFlag();
                    SetSkinLabels();
                }
            }

            private void OnSkinItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(IPaintablePersonalItem.IsNumberActive):
                        SetSkinNumber();
                        break;
                    case nameof(IPaintablePersonalItem.IsFlagActive):
                        SetSkinFlag();
                        break;
                    case nameof(IPaintablePersonalItem.ActiveLabels):
                        SetSkinLabels();
                        break;
                    case nameof(PaintableItem.Enabled):
                        if (sender is IPaintablePersonalItem) {
                            SetSkinNumber();
                            SetSkinFlag();
                            SetSkinLabels();
                        }

                        var item = (PaintableItem)sender;
                        if (!item.Enabled) break;

                        foreach (var next in _skinItems) {
                            if (!ReferenceEquals(next, item) && next.Enabled && item.GetAffectedTextures().Any(next.GetAffectedTextures().Contains)) {
                                next.Enabled = false;
                            }
                        }
                        break;
                }
            }

            private DelegateCommand _toggleSkinModeCommand;

            public DelegateCommand ToggleSkinModeCommand
                => _toggleSkinModeCommand ?? (_toggleSkinModeCommand = new DelegateCommand(() => { Mode = Mode == Mode.Skin ? Mode.Main : Mode.Skin; }));

            private bool _saveAsNewSkin;

            public bool SaveAsNewSkin {
                get => _saveAsNewSkin;
                set {
                    if (Equals(value, _saveAsNewSkin)) return;
                    _saveAsNewSkin = value;
                    OnPropertyChanged();
                    OnErrorsChanged(nameof(SaveAsSkinId));
                    Renderer?.SetCurrentSkinActive(!value);
                }
            }

            private string _saveAsSkinId;

            public string SaveAsSkinId {
                get => _saveAsSkinId;
                set {
                    if (Equals(value, _saveAsSkinId)) return;
                    _saveAsSkinId = value;
                    OnPropertyChanged();
                    OnErrorsChanged();
                }
            }

            private string _saveAsSkinIdSuggested = "generated";

            public string SaveAsSkinIdSuggested {
                get => _saveAsSkinIdSuggested;
                set => Apply(value, ref _saveAsSkinIdSuggested);
            }

            private async Task SkinSaveAsync() {
                try {
                    var skinsItems = SkinItems;
                    if (Renderer == null || skinsItems == null) return;

                    var saveAsNew = SaveAsNewSkin;
                    var skin = saveAsNew ? (CarSkinObject)Car.SkinsManager.AddNew(SaveAsSkinId ?? SaveAsSkinIdSuggested) : Skin;
                    if (skin == null) {
                        throw new Exception("Can’t find skin");
                    }

                    using (var progress = WaitingDialog.Create("Saving…")) {
                        var cancellation = progress.CancellationToken;

                        var jObj = new JObject();
                        var list = skinsItems.ToList();

                        var listProgress = progress.Subrange(0.001, 0.8);
                        for (var i = 0; i < list.Count; i++) {
                            var item = list[i];
                            try {
                                jObj[PaintShop.PaintShop.NameToId(item.DisplayName, false)] = item.Serialize();
                                listProgress.Report(item.DisplayName, i, list.Count);
                                await item.SaveAsync(skin.Location, cancellation);
                                if (cancellation.IsCancellationRequested) break;
                            } catch (NotImplementedException) { }
                        }

                        if (HasNumbers) {
                            skin.SkinNumber = SkinNumber.ToInvariantString();
                            await skin.SaveAsync();
                        }

                        if (!cancellation.IsCancellationRequested && saveAsNew) {
                            var carPaint = skinsItems.OfType<CarPaint>().FirstOrDefault(x => x.Enabled);
                            if (carPaint != null && LiveryGenerator != null) {
                                var liveryStyle = (carPaint.PatternEnabled ? carPaint.CurrentPattern?.LiveryStyle : null) ?? carPaint.LiveryStyle;
                                if (liveryStyle != null) {
                                    progress.Report("Generating livery…", 0.9);

                                    var colors = new Dictionary<int, Color>(3);
                                    foreach (var item in skinsItems.Where(x => x.Enabled).OrderBy(x => x.LiveryPriority)) {
                                        foreach (var pair in item.LiveryColors) {
                                            colors[pair.Key] = pair.Value;
                                        }
                                    }

                                    if (carPaint.LiveryColorId.HasValue) {
                                        colors[carPaint.LiveryColorId.Value] = carPaint.Color;
                                    }

                                    var patternColors = carPaint.CurrentPattern?.LiveryColors;
                                    if (patternColors != null) {
                                        foreach (var pair in patternColors) {
                                            colors[pair.Key] = pair.Value;
                                        }
                                    }

                                    Logging.Debug("Livery colors: " + colors.Select(x => $"[{x.Key}={x.Value.ToHexString()}]").JoinToReadableString());

                                    var colorsArray = Enumerable.Range(0, 3).Select(x => colors.GetValueOr(x, Colors.White)).ToArray();
                                    await LiveryGenerator.CreateLiveryAsync(skin, colorsArray, liveryStyle);
                                }
                            }
                        }

                        File.WriteAllText(Path.Combine(skin.Location, "cm_skin.json"), jObj.ToString(Formatting.Indented));

                        if (!cancellation.IsCancellationRequested) {
                            progress.Report("Generating preview…", 0.99999);
                            await new ToUpdatePreview(Car, skin).Run();
                        }
                    }

                    if (saveAsNew) {
                        Skin = skin;
                        SaveAsNewSkin = false;
                    }
                } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                    NonfatalError.Notify("Can’t save skin", e);
                }
            }

            #region Skin numbers
            private bool _hasNumbers;

            public bool HasNumbers {
                get => _hasNumbers;
                set => Apply(value, ref _hasNumbers);
            }

            private int _skinNumber;

            public int SkinNumber {
                get => _skinNumber;
                set {
                    if (Equals(value, _skinNumber)) return;
                    _skinNumber = value;
                    OnPropertyChanged();
                    SetSkinNumber();
                }
            }

            private void SetSkinNumber() {
                var number = SkinNumber;
                var any = false;

                try {
                    if (SkinItems == null) return;
                    foreach (var item in SkinItems.Where(x => x.Enabled).OfType<IPaintablePersonalItem>()) {
                        item.Number = number;
                        any |= item.IsNumberActive;
                    }
                } finally {
                    HasNumbers = any;
                }
            }
            #endregion

            #region Skin flags
            public ListCollectionView Countries { get; } = new ListCollectionView(new List<string>(DataProvider.Instance.KunosIdsCountries)) {
                SortDescriptions = {
                    new SortDescription()
                }
            };

            private string _skinFlagCountry;

            public string SkinFlagCountry {
                get => _skinFlagCountry;
                set {
                    if (Equals(value, _skinFlagCountry)) return;
                    _skinFlagCountry = value;
                    OnPropertyChanged();
                    SkinFlag = AcStringValues.GetCountryId(value);
                }
            }

            private bool _hasFlags;

            public bool HasFlags {
                get => _hasFlags;
                set => Apply(value, ref _hasFlags);
            }

            private string _skinFlag;

            public string SkinFlag {
                get => _skinFlag;
                set {
                    if (Equals(value, _skinFlag)) return;
                    _skinFlag = value;
                    OnPropertyChanged();
                    SetSkinFlag();
                }
            }

            private void SetSkinFlag() {
                var flagId = SkinFlag;
                var any = false;

                try {
                    if (SkinItems == null) return;

                    var file = flagId == null ? null : FilesStorage.Instance.GetContentFile("Country Flags (Large)", $"{flagId}.png");
                    var filename = file?.Exists == true ? file.Filename : null;
                    foreach (var item in SkinItems.Where(x => x.Enabled).OfType<IPaintablePersonalItem>()) {
                        item.FlagTexture = filename;
                        any |= item.IsFlagActive;
                    }
                } finally {
                    HasFlags = any;
                }
            }
            #endregion

            #region Skin labels
            public sealed class SkinLabelWrapper : Displayable {
                private static string DriverNameToLabel(string name) {
                    var p = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    return p.Length == 2 ? $"{p[0][0]}. {p[1]}".ToUpper() : name.ToUpper();
                }

                public SkinLabelWrapper(string key) {
                    Key = key;
                    DisplayName = AcStringValues.NameFromId(key);

                    if (key == "pilot") {
                        Value = DriverNameToLabel(SettingsHolder.Drive.PlayerName);
                    }
                }

                public string Key { get; }

                private string _value;

                public string Value {
                    get => _value;
                    set => Apply(value, ref _value);
                }
            }

            private List<SkinLabelWrapper> _skinLabels;

            [CanBeNull]
            private Dictionary<string, string> _skinLabelsValues;

            public List<SkinLabelWrapper> SkinLabels {
                get => _skinLabels;
                set {
                    if (Equals(value, _skinLabels)) return;
                    _skinLabels = value;
                    OnPropertyChanged();

                    if (value != null) {
                        _skinLabelsValues = value.ToDictionary(x => x.Key, x => x.Value);
                        foreach (var skinLabelWrapper in value) {
                            skinLabelWrapper.PropertyChanged += OnSkinLabelWrapperPropertyChanged;
                        }
                    } else {
                        _skinLabelsValues = null;
                    }
                }
            }

            private void OnSkinLabelWrapperPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
                if (_skinLabelsValues == null) return;
                var v = (SkinLabelWrapper)sender;
                _skinLabelsValues[v.Key] = v.Value;

                if (SkinItems == null) return;
                foreach (var item in SkinItems.OfType<IPaintablePersonalItem>()) {
                    item.Labels = _skinLabelsValues;
                }
            }

            private void SetSkinLabels() {
                var list = new List<SkinLabelWrapper>();

                try {
                    if (SkinItems == null) return;
                    foreach (var item in SkinItems.Where(x => x.Enabled).OfType<IPaintablePersonalItem>()) {
                        var v = item.ActiveLabels;
                        if (v != null && v.Count > 0) {
                            foreach (var u in v) {
                                if (list.Any(x => x.Key == u)) continue;
                                list.Add(new SkinLabelWrapper(u));
                            }
                        }
                    }
                } finally {
                    SkinLabels = list;
                    if (SkinItems != null) {
                        foreach (var item in SkinItems.OfType<IPaintablePersonalItem>()) {
                            item.Labels = _skinLabelsValues;
                        }
                    }
                }
            }
            #endregion

            private AsyncCommand _skinSaveCommand;

            public AsyncCommand SkinSaveCommand => _skinSaveCommand ?? (_skinSaveCommand = new AsyncCommand(SkinSaveAsync,
                    () => SkinItems?.Any() == true));

            private void OnLicensePlatesChanged(object sender, EventArgs e) {
                UpdateLicensePlatesStyles();
            }

            private List<FilesStorage.ContentEntry> _styles;

            private void UpdateLicensePlatesStyles() {
                var skinItems = SkinItems;
                if (Renderer == null || skinItems == null) return;

                _styles = FilesStorage.Instance.GetContentDirectories(ContentCategory.LicensePlates).ToList();
                foreach (var item in skinItems.OfType<LicensePlate>()) {
                    item.SetStyles(_styles);
                }
            }

            private void OnPaintShopDataChanged(object sender, EventArgs e) {
                if (SkinItems == null) return;
                UpdatePaintShopData();
            }

            private readonly Busy _updatePaintShopDataBusy = new Busy();

            private void UpdatePaintShopData() {
                _updatePaintShopDataBusy.Task(async () => {
                    var skinItems = await GetSkinItems(default(CancellationToken));
                    if (skinItems == null || SkinItems == null) return;

                    if (_styles != null) {
                        foreach (var item in skinItems.OfType<LicensePlate>()) {
                            item.SetStyles(_styles);
                        }
                    }

                    for (var i = 0; i < SkinItems.Count; i++) {
                        var item = SkinItems[i];
                        skinItems.GetByIdOrDefault(item.Id)?.Deserialize(item.Serialize());
                    }

                    SkinItems = skinItems;
                });
            }
            #endregion
        }
    }
}