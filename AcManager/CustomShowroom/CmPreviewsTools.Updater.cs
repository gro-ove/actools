using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;
using OxyPlot.Reporting;

namespace AcManager.CustomShowroom {
    public partial class CmPreviewsTools {
        private class Updater : UpdaterBase {
            [NotNull]
            private readonly IDarkPreviewsUpdater _updater;
            
            private readonly bool _localUpdater;

            public Updater([NotNull] IReadOnlyList<ToUpdatePreview> entries, [NotNull] DarkPreviewsOptions options, [CanBeNull] string presetName,
                    [CanBeNull] IDarkPreviewsUpdater updater, Func<CarSkinObject, string> destinationOverrideCallback, IProgress<AsyncProgressEntry> progress,
                    CancellationToken cancellation) : base(entries, options, presetName, destinationOverrideCallback, progress, cancellation) {
                if (updater == null) {
                    _localUpdater = true;
                    _updater = DarkPreviewsUpdaterFactory.Create(false, AcRootDirectory.Instance.RequireValue, options);
                } else {
                    _updater = updater;
                    _updater.SetOptions(options);
                }
            }

            protected override async Task RunAsyncOverride() {
                try {
                    var checksum = Options.GetChecksum(false);
                    var step = 1d / Items.Count;
                    for (var i = 0; i < Items.Count; i++) {
                        if (Cancel()) return;

                        var entry = Items[i];
                        var progress = step * i;

                        var currentCar = entry.Car;
                        var currentSkins = entry.Skins;

                        var halfstep = step * 0.5 / currentSkins.Count;
                        for (var j = 0; j < currentSkins.Count; j++) {
                            if (Cancel()) return;

                            var currentSkin = currentSkins[j];
                            UpdateWaitingDetails();

                            var subprogress = progress + step * (0.1 + 0.8 * j / currentSkins.Count);
                            var filename = await GetDestinationAsync(currentSkin, subprogress);
                            progressReport?.Report(new AsyncProgressEntry($"Updating skin {currentSkin.DisplayName}…",
                                    VerySingleMode ? 0d : subprogress + halfstep));

                            try {
                                await _updater.ShotAsync(currentCar.Id, currentSkin.Id, filename, currentCar.AcdData,
                                        GetInformation(currentCar, currentSkin, PresetName, checksum), () => PreviewReadyCallback(currentSkin),
                                        cancellation: CancellationToken);
                            } catch (Exception e) {
                                ReportFailedSkin(null, e);
                                continue;
                            }
                            ReportShotSkin();
                        }
                    }
                } finally {
                    if (_localUpdater) {
                        _updater.Dispose();
                    }
                }
            }

            protected override async Task FinalizeAsyncOverride() {
                await _updater.WaitForProcessing();
            }
        }
    }
}