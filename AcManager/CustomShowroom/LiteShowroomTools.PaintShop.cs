using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.CustomShowroom {
    public partial class LiteShowroomTools {
        public partial class ViewModel : INotifyDataErrorInfo {
            #region Skin
            private void DisposeSkinItems() {
                Renderer?.SetCurrentSkinActive(true);
                FilesStorage.Instance.Watcher(ContentCategory.PaintShop).Update -= OnPaintShopDataChanged;
                FilesStorage.Instance.Watcher(ContentCategory.LicensePlates).Update -= OnLicensePlatesChanged;
                SkinItems = null;
            }

            [ItemCanBeNull]
            private async Task<List<PaintShop.PaintableItem>> GetSkinItems(CancellationToken cancellation) {
                // PaintShopRulesLoader = new PaintShopRulesLoader();

                await Task.Delay(50);
                if (cancellation.IsCancellationRequested) return null;

                var skinItems = (await PaintShop.GetPaintableItemsAsync(Car.Id, Renderer?.MainSlot.Kn5, cancellation))?.ToList();
                if (skinItems == null || cancellation.IsCancellationRequested) return null;

                try {
                    var skin = Path.Combine(Skin.Location, "cm_skin.json");
                    await Task.Run(() => {
                        if (File.Exists(skin)) {
                            var jObj = JObject.Parse(File.ReadAllText(skin));
                            foreach (var pair in jObj) {
                                if (pair.Value.Type != JTokenType.Object) continue;
                                skinItems.FirstOrDefault(x => PaintShop.NameToId(x.DisplayName, false) == pair.Key)?
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

            /*private PaintShopRulesLoader _paintShopRulesLoader;

            public PaintShopRulesLoader PaintShopRulesLoader {
                get => _paintShopRulesLoader;
                set {
                    if (Equals(value, _paintShopRulesLoader)) return;
                    _paintShopRulesLoader = value;
                    OnPropertyChanged();
                }
            }*/

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
                        }

                        Mode = Mode.Skin;
                        SkinNumber = Skin.SkinNumber.AsInt(1);
                        SaveAsSkinIdSuggested = Path.GetFileName(FileUtils.EnsureUnique(Path.Combine(Car.SkinsDirectory, "generated")));
                    }));

            private IList<PaintShop.PaintableItem> _skinItems;

            [CanBeNull]
            public IList<PaintShop.PaintableItem> SkinItems {
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
                }
            }

            private void OnSkinItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(PaintShop.IPaintableNumberItem.IsNumberActive):
                        SetSkinNumber();
                        break;
                    case nameof(PaintShop.PaintableItem.Enabled):
                        var item = (PaintShop.PaintableItem)sender;
                        if (!item.Enabled) break;

                        foreach (var next in _skinItems) {
                            if (!ReferenceEquals(next, item) && next.Enabled && item.AffectedTextures.Any(next.AffectedTextures.Contains)) {
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
                set {
                    if (Equals(value, _saveAsSkinIdSuggested)) return;
                    _saveAsSkinIdSuggested = value;
                    OnPropertyChanged();
                }
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

                        for (var i = 0; i < list.Count; i++) {
                            var item = list[i];
                            try {
                                jObj[PaintShop.NameToId(item.DisplayName, false)] = item.Serialize();
                                progress.Report(item.DisplayName, i, list.Count);
                                await item.SaveAsync(skin.Location, cancellation);
                                if (cancellation.IsCancellationRequested) break;
                            } catch (NotImplementedException) { }
                        }

                        if (HasNumbers) {
                            skin.SkinNumber = SkinNumber.ToInvariantString();
                            skin.Save();
                        }

                        if (!cancellation.IsCancellationRequested) {
                            var carPaint = skinsItems.OfType<PaintShop.CarPaint>().FirstOrDefault(x => x.Enabled);
                            if (carPaint != null && LiveryGenerator != null) {
                                var liveryStyle = (carPaint.PatternEnabled ? carPaint.CurrentPattern?.LiveryStyle : null) ?? carPaint.LiveryStyle;
                                if (liveryStyle != null) {
                                    progress.Report("Generating livery…", 0.99999);

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
                    }

                    if (saveAsNew) {
                        Skin = skin;
                    }
                } catch (Exception e) when (e.IsCanceled()) {
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t save skin", e);
                }
            }

            private bool _hasNumbers;

            public bool HasNumbers {
                get => _hasNumbers;
                set {
                    if (Equals(value, _hasNumbers)) return;
                    _hasNumbers = value;
                    OnPropertyChanged();
                }
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
                    foreach (var item in SkinItems.OfType<PaintShop.IPaintableNumberItem>()) {
                        item.Number = number;
                        any = item.IsNumberActive;
                    }
                } finally {
                    HasNumbers = any;
                }
            }

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
                foreach (var item in skinItems.OfType<PaintShop.LicensePlate>()) {
                    item.SetStyles(_styles);
                }
            }

            private void OnPaintShopDataChanged(object sender, EventArgs e) {
                UpdatePaintShopData();
            }

            private readonly Busy _updatePaintShopDataBusy = new Busy();

            private void UpdatePaintShopData() {
                _updatePaintShopDataBusy.Task(async () => {
                    var skinItems = await GetSkinItems(default(CancellationToken));
                    if (skinItems == null || SkinItems == null) return;

                    if (_styles != null) {
                        foreach (var item in skinItems.OfType<PaintShop.LicensePlate>()) {
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