using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AcManager.Controls;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public partial class CmPreviewsTools {
        private static UpdaterBase UpdaterFactory([NotNull] IReadOnlyList<ToUpdatePreview> entries, [NotNull] DarkPreviewsOptions options,
                [CanBeNull] string presetName, [CanBeNull] IDarkPreviewsUpdater updater,
                Func<CarSkinObject, string> destinationOverrideCallback = null, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            return SettingsHolder.CustomShowroom.CspPreviewsReady
                    ? (UpdaterBase)new AcUpdater(entries, options, presetName, destinationOverrideCallback, progress, cancellation)
                    : new Updater(entries, options, presetName, updater, destinationOverrideCallback, progress, cancellation);
        }
        
        private abstract class UpdaterBase {
            [NotNull]
            private readonly IReadOnlyList<ToUpdatePreview> _inEntries;

            [NotNull]
            protected readonly DarkPreviewsOptions Options;

            [CanBeNull]
            protected readonly string PresetName;

            protected CancellationToken CancellationToken => _cancellationToken;

            private readonly Func<CarSkinObject, string> _destinationOverrideCallback;
            private CancellationToken _cancellationToken;
            private readonly IProgress<AsyncProgressEntry> _progress;

            private readonly List<UpdatePreviewError> _errors = new List<UpdatePreviewError>();
            private DispatcherTimer _dispatcherTimer;

            [CanBeNull]
            private WaitingDialog _waiting;

            private Stopwatch _started;
            private bool _finished;

            private bool _recyclingWarning;

            protected bool VerySingleMode;

            protected class ToUpdateSolved {
                [NotNull]
                public CarObject Car;
                
                [NotNull]
                public List<CarSkinObject> Skins;
                
                [NotNull]
                public ToUpdatePreview Source;

                public ToUpdateSolved(CarObject car, List<CarSkinObject> skins, ToUpdatePreview source) {
                    Car = car;
                    Skins = skins;
                    Source = source;
                }
            }

            protected List<ToUpdateSolved> Items;
            
            private int _entryIndex;
            private int _skinIndex;
            private int _shotSkins;

            private void InitializeProgressValues() {
                _shotSkins = 0;
                _entryIndex = 0;
                _skinIndex = 0;
            }

            protected void ReportShotSkin() {
                if (_entryIndex >= Items.Count) return;
                _shotSkins++;
                if (++_skinIndex == Items[_entryIndex].Skins.Count) {
                    _skinIndex = 0;
                    if (++_entryIndex == Items.Count) _finished = true;
                }
            }

            protected void ReportFailedSkin(string errorMessage, Exception exception) {
                if (_entryIndex >= Items.Count) return;
                RegisterError(errorMessage, exception, Items[_entryIndex]);
                _shotSkins++;
                if (++_skinIndex == Items[_entryIndex].Skins.Count) {
                    _skinIndex = 0;
                    if (++_entryIndex == Items.Count) _finished = true;
                }
            }

            protected async Task<string> GetDestinationAsync(CarSkinObject skin, double subprogress) {
                var ret = _destinationOverrideCallback?.Invoke(skin) ?? Path.Combine(skin.Location, Options.PreviewName);
                if (_destinationOverrideCallback == null && SettingsHolder.CustomShowroom.PreviewsRecycleOld && File.Exists(ret)) {
                    if (++_recycled > 5) {
                        _recyclingWarning = true;
                    }
                    progressReport?.Report(new AsyncProgressEntry($"Recycling current preview for {skin.DisplayName}…",
                            VerySingleMode ? 0d : subprogress));
                    await Task.Run(() => FileUtils.Recycle(ret));
                }
                return ret;
            }

            protected UpdaterBase([NotNull] IReadOnlyList<ToUpdatePreview> entries, [NotNull] DarkPreviewsOptions options, [CanBeNull] string presetName,
                    Func<CarSkinObject, string> destinationOverrideCallback, IProgress<AsyncProgressEntry> progress,
                    CancellationToken cancellation) {
                _inEntries = entries;
                Options = options;
                PresetName = presetName;
                _destinationOverrideCallback = destinationOverrideCallback;
                _progress = progress;
                _cancellationToken = cancellation;
            }

            protected void RegisterError([CanBeNull] string errorMessage, [CanBeNull] Exception exception, ToUpdateSolved entry) {
                if (_errors.All(x => x.ToUpdate != entry.Source)) {
                    var errorData = errorMessage ?? exception?.ToString() ?? "Unknown error";
                    Logging.Warning(errorData);
                    _errors.Add(new UpdatePreviewError(entry.Source, errorData, null));
                }
            }

            private async Task<IReadOnlyList<UpdatePreviewError>> RunInnerAsync() {
                Initialize();

                Items = new List<ToUpdateSolved>();
                foreach (var entry in _inEntries) {
                    if (entry.Skins != null) {
                        Items.Add(new ToUpdateSolved(entry.Car, entry.Skins.ToList(), entry));
                    } else {
                        progressReport?.Report(new AsyncProgressEntry("Loading skins…", 0d));
                        await entry.Car.SkinsManager.EnsureLoadedAsync();
                        if (Cancel()) return _errors;
                        Items.Add(new ToUpdateSolved(entry.Car, entry.Car.EnabledOnlySkins.ToList(), entry));
                    }
                }
                Items.RemoveAll(x => x.Skins.Count == 0);
                if (Items.Count != 0) {
                    InitializeProgressValues();
                    await RunAsyncOverride();
                    if (!Cancel()) {
                        await FinalizeAsync();
                    }
                }
                return _errors;
            }

            public async Task<IReadOnlyList<UpdatePreviewError>> RunAsync() {
                try {
                    if (Options.Showroom != null && ShowroomsManager.Instance.GetById(Options.Showroom) == null) {
                        if (Options.Showroom == @"at_previews" && MissingShowroomHelper != null) {
                            await MissingShowroomHelper.OfferToInstall("Kunos Previews Showroom (AT Previews Special)", "at_previews",
                                    "http://www.assettocorsa.net/assetto-corsa-v1-5-dev-diary-part-33/");
                            if (ShowroomsManager.Instance.GetById(Options.Showroom) != null) {
                                return await RunInnerAsync();
                            }
                        }

                        throw new InformativeException("Can’t update preview", $"Showroom “{Options.Showroom}” is missing");
                    }
                    return await RunInnerAsync();
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t update preview", e);
                    return null;
                } finally {
                    _waiting?.Dispose();
                    _dispatcherTimer?.Stop();
                }
            }

            private int _recycled;
            protected IProgress<AsyncProgressEntry> progressReport;

            private void Initialize() {
                _finished = false;

                _waiting = _progress != null ? null : new WaitingDialog { CancellationText = "Stop" };
                progressReport = _progress ?? _waiting;
                if (_progress == null) {
                    _cancellationToken = _waiting?.CancellationToken ?? default(CancellationToken);
                }

                var singleMode = _inEntries.Count == 1;
                VerySingleMode = singleMode && _inEntries[0].Skins?.Count == 1;
                _recycled = 0;

                if (!VerySingleMode) {
                    _waiting?.SetImage(null);

                    if (SettingsHolder.CustomShowroom.PreviewsRecycleOld) {
                        _waiting?.SetMultiline(true);
                    }
                }

                _started = Stopwatch.StartNew();

                _dispatcherTimer = new DispatcherTimer(TimeSpan.FromSeconds(0.5), DispatcherPriority.Background, TimerCallback,
                        Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
                _dispatcherTimer.Start();
            }

            private async Task FinalizeAsync() {
                _dispatcherTimer?.Stop();
                progressReport?.Report(new AsyncProgressEntry("Saving…", VerySingleMode ? 0d : 0.999999d));
                await FinalizeAsyncOverride();

                if (_errors.Count > 0) {
                    NonfatalError.Notify("Can’t update previews:\n"
                            + _errors.Select(x => @"• " + x.Message.ToSentence()).JoinToString(";" + Environment.NewLine));
                }
            }

            protected virtual Task FinalizeAsyncOverride() {
                return Task.Delay(0);
            } 

            protected void PreviewReadyCallback(CarSkinObject skin) {
                if (VerySingleMode) return;
                try {
                    var filename = Path.Combine(skin.Location, Options.PreviewName);
                    ActionExtension.InvokeInMainThreadAsync(() => UpdatePreviewImage(filename));
                } catch {
                    // ignored
                }
            }

            private void UpdatePreviewImage(string filename) {
                if (_finished) return;
                BetterImage.CleanUpCache();
                _waiting?.SetImage(filename);
            }

            protected bool Cancel() {
                if (!_cancellationToken.IsCancellationRequested) return false;
                for (var i = _entryIndex; i < Items.Count; i++) {
                    _errors.Add(new UpdatePreviewError(Items[i].Source, ControlsStrings.Common_Cancelled, null));
                }
                return true;
            }

            private void TimerCallback(object sender, EventArgs args) {
                if (Items == null) return;
                UpdateWaitingDetails();
            }

            protected void UpdateWaitingDetails() {
                if (_finished || _entryIndex >= Items.Count) return;
                var current = Items[_entryIndex];
                _waiting?.SetDetails(GetDetails(_entryIndex, current.Car, current.Skins[_skinIndex], current.Skins.Count - _skinIndex));
            }

            private int LeftSkins(int currentEntry) {
                var result = 0d;
                for (var k = currentEntry + 1; k < Items.Count; k++) {
                    var entry = Items[k];
                    result += entry.Skins.Count;
                }
                return result.RoundToInt();
            }

            private IEnumerable<string> GetDetails(int currentIndex, CarObject car, CarSkinObject skin, int currentEntrySkinsLeft) {
                var left = LeftSkins(currentIndex) + currentEntrySkinsLeft;

                var speed = _shotSkins / _started.Elapsed.TotalMinutes;
                var remainingTime = speed < 0.0001 ? "Unknown" : $"About {TimeSpan.FromMinutes(left / speed).ToReadableTime()}";
                var remainingItems = $"About {left} {PluralizingConverter.Pluralize(left, ControlsStrings.CustomShowroom_SkinHeader).ToSentenceMember()}";

                return new[] {
                    $"Car: {car?.DisplayName}", $"Skin: {skin?.DisplayName ?? "?"}",
                    $"Speed: {speed:F2} {PluralizingConverter.Pluralize(10, ControlsStrings.CustomShowroom_SkinHeader).ToSentenceMember()}/{"min"}",
                    $"Time remaining: {remainingTime}",
                    $"Items remaining: {remainingItems}",
                    _recyclingWarning ? "[i]Recycling seems to take too long? If so, it can always be disabled in Settings.[/i]" : null
                }.NonNull();
            }

            protected abstract Task RunAsyncOverride();
        }
    }
}