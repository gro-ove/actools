using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcLog;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public partial class CmPreviewsTools {
        public static int OptionBatchSize = 20;

        private class AcUpdater : UpdaterBase {
            public AcUpdater([NotNull] IReadOnlyList<ToUpdatePreview> entries, [NotNull] DarkPreviewsOptions options, [CanBeNull] string presetName,
                    Func<CarSkinObject, string> destinationOverrideCallback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation)
                    : base(entries, options, presetName, destinationOverrideCallback, progress, cancellation) { }

            private static IEnumerable<List<T>> SplitIntoBatches<T>(IReadOnlyCollection<T> items, int groupSize, int groupSizeMax) {
                List<T> ret;
                for (var pos = 0; pos < items.Count; pos += ret.Count) {
                    yield return ret = items.Skip(pos).Take(pos + groupSizeMax < items.Count ? groupSize : int.MaxValue).ToList();
                }
            }

            private static void CleanUpStates(string[] carIds) {
                Task.Run(() => {
                    foreach (var item in carIds) {
                        DarkPreviewsAcUpdater.CleanPreviewState(AcRootDirectory.Instance.RequireValue, item);
                    }
                }).Ignore();
            }

            protected override async Task RunAsyncOverride() {
                foreach (var car in Items.Select(x => x.Car).Distinct()) {
                    progressReport?.Report(new AsyncProgressEntry($"Installing config and occlusion for {car.DisplayName}…", double.Epsilon));
                    await PatchCarsDataUpdater.Instance.TriggerAutoLoadAsync(car?.Id, null, CancellationToken);
                    await PatchCarsVaoDataUpdater.Instance.TriggerAutoLoadAsync(car?.Id, null, CancellationToken);
                }
                
                progressReport?.Report(new AsyncProgressEntry("Launching Assetto Corsa in background…", double.Epsilon));

                var carIds = Items.Select(x => x.Car.Id).ToArray();
                var totalSkins = Items.Sum(x => x.Skins.Count);
                var readySkins = 0;
                var multiCarRun = Items.Count > 1;

                CleanUpStates(carIds);
                using (new ActionAsDisposable(() => CleanUpStates(carIds))) {
                    var extension = Path.GetExtension(Options.PreviewName);
                    var checksum = Options.GetChecksum(true);
                    var comment = GetInformation(null, null, PresetName, checksum).Comment;
                    var shutdownStopwatch = Stopwatch.StartNew();
                    var runIndex = new[] { 0 };
                    var timeouted = false;
                    string acError = null;

                    Task ShotWrapperAsync(int currentRun, IEnumerable<Tuple<string, string, string>> items) {
                        return DarkPreviewsAcUpdater.ShotSeriesAsync(AcRootDirectory.Instance.RequireValue, Options, items, comment,
                                () => {
                                    if (shutdownStopwatch.Elapsed.TotalMinutes > 5d) {
                                        timeouted = true;
                                        return true;
                                    }
                                    return false;
                                }, CancellationToken)
                                .ContinueWith(r => {
                                    return Task.Delay(TimeSpan.FromSeconds(4d)).ContinueWith(_ => {
                                        if (currentRun == runIndex[0]) {
                                            acError = AcLogHelper.TryToDetermineWhatsGoingOn()?.GetDescription() ?? string.Empty;
                                        }
                                    });
                                });
                    }

                    foreach (var car in Items) {
                        foreach (var skins in SplitIntoBatches(car.Skins, OptionBatchSize, (int)(OptionBatchSize * 1.5))) {
                            var currentRun = ++runIndex[0];
                            var expectedIndex = 0;
                            var items = skins.Select(y => Tuple.Create(car.Car.Id, y.Id, y.PreviewImage)).ToList();
                            var temporaryDestinations = items.Select(x =>
                                    FilesStorage.Instance.GetTemporaryFilename("Previews", $"{x.Item1}__{x.Item2}{extension}")).ToList();
                            await Task.Run(() => temporaryDestinations.ForEach(x => FileUtils.TryToDelete(x)));
                            if (Cancel()) return;
                            if (acError != null && !multiCarRun) {
                                throw new Exception(acError.Or(timeouted ? "Assetto Corsa took too long" : "Assetto Corsa closed down unexpectedly"));
                            }

                            shutdownStopwatch.Restart();
                            await new[] {
                                ShotWrapperAsync(currentRun, items.Select((x, i) => Tuple.Create(x.Item1, x.Item2, temporaryDestinations[i]))),
                                ((Func<Task>)(async () => {
                                    while (!Cancel() && acError == null) {
                                        if (File.Exists(temporaryDestinations[expectedIndex])) {
                                            shutdownStopwatch.Restart();
                                            var realDestination = await GetDestinationAsync(skins[expectedIndex], 0d);
                                            var temporaryDestination = temporaryDestinations[expectedIndex];
                                            await Task.Run(() => {
                                                FileUtils.TryToDelete(realDestination);
                                                for (var i = 0; i < 3; ++i) {
                                                    if (i > 0) Thread.Sleep(200);
                                                    try {
                                                        File.Move(temporaryDestination, realDestination);
                                                        break;
                                                    } catch {
                                                        if (i == 2) throw;
                                                    }
                                                }
                                            });
                                            shutdownStopwatch.Restart();
                                            PreviewReadyCallback(skins[expectedIndex]);
                                            ReportShotSkin();

                                            ++readySkins;
                                            progressReport?.Report(new AsyncProgressEntry("Waiting for Assetto Corsa to make shots…", readySkins, totalSkins));

                                            if (++expectedIndex == temporaryDestinations.Count) {
                                                ++currentRun;
                                                break;
                                            }
                                        }
                                        await Task.Delay(200);
                                    }
                                }))()
                            }.WhenAll();
                            if (Cancel()) return;

                            if (acError != null) {
                                var errorMessage = acError.Or("Assetto Corsa closed down unexpectedly");
                                if (multiCarRun) {
                                    for (var i = expectedIndex; i < items.Count; ++i) {
                                        ReportFailedSkin(errorMessage, null);
                                    }
                                } else {
                                    throw new Exception(errorMessage);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}