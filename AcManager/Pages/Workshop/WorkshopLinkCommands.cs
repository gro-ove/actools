using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using AcManager.CustomShowroom;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Kn5File;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.Workshop {
    public static class WorkshopLinkCommands {
        private static bool _showingCarInShowroom;

        private static async Task ShowCarInShowroomAsync(string carId) {
            if (_showingCarInShowroom) return;
            _showingCarInShowroom = true;

            try {
                var temporaryDirectory = FilesStorage.Instance.GetTemporaryDirectory("Workshop", "Showroom", carId);
                var temporaryFilename = Path.Combine(temporaryDirectory, "data.zip");

                byte[] modelData = null;
                string mainSkidId = null;
                var carData = new VirtualDataWrapper();

                using (var waiting = WaitingDialog.Create("Loading showroom…")) {
                    if (!File.Exists(temporaryFilename)) {
                        var temporaryFilenameProgress = $"{temporaryFilename}.tmp";
                        using (var client = new CookieAwareWebClient()) {
                            var totalSize = -1L;
                            var progressTimer = new AsyncProgressBytesStopwatch();
                            await new DirectLoader("https://files.acstuff.ru/shared/XAPv/peugeot_504.zip").DownloadAsync(client, (url, information) => {
                                totalSize = information.TotalSize ?? -1L;
                                return new FlexibleLoaderDestination(temporaryFilenameProgress, true);
                            }, progress: new Progress<long>(x => { waiting.Report(AsyncProgressEntry.CreateDownloading(x, totalSize, progressTimer)); }),
                                    cancellation: waiting.CancellationToken);
                            waiting.CancellationToken.ThrowIfCancellationRequested();
                        }
                        File.Move(temporaryFilenameProgress, temporaryFilename);
                    }
                    waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Loading…"));

                    await Task.Run(() => {
                        var data = File.ReadAllBytes(@"H:\temp\peugeot_504\peugeot_504.zip");
                        using (var stream = new MemoryStream(data))
                        using (var archive = new ZipArchive(stream)) {
                            foreach (var entry in archive.Entries) {
                                if (entry.Length == 0) continue;
                                if (entry.FullName == @"model.kn5") {
                                    modelData = entry.Open().ReadAsBytesAndDispose();
                                } else if (entry.FullName.StartsWith(@"data")) {
                                    carData.Data[Path.GetFileName(entry.FullName)] = entry.Open().ReadAsStringAndDispose();
                                } else {
                                    if (mainSkidId == null && entry.FullName.StartsWith(@"skins")) {
                                        mainSkidId = Path.GetFileName(Path.GetDirectoryName(entry.FullName));
                                    }
                                    var newFilename = Path.Combine(temporaryDirectory, entry.FullName);
                                    FileUtils.EnsureFileDirectoryExists(newFilename);
                                    entry.ExtractToFile(newFilename, true);
                                }
                                waiting.CancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                    });
                    waiting.CancellationToken.ThrowIfCancellationRequested();
                }

                if (modelData == null) {
                    throw new Exception("Model is missing");
                }

                if (mainSkidId == null) {
                    throw new Exception("Skins are missing");
                }

                var description = CarDescription.FromKn5(Kn5.FromBytes(modelData), temporaryDirectory, carData);
                var renderer = new DarkKn5ObjectRenderer(description) {
                    FlatMirror = true,
                    FlatMirrorReflectiveness = 0.3f,
                    FlatMirrorReflectedLight = true,
                    BackgroundColor = Color.White,
                    BackgroundBrightness = 0.05f,
                    LightBrightness = 2f,
                    AmbientBrightness = 2f,
                    AmbientUp = Color.FromArgb(0xEEEEEE),
                    AmbientDown = Color.FromArgb(0x333333),
                    UseDof = true,
                    UseAccumulationDof = true,
                    AccumulationDofIterations = 40,
                    AccumulationDofApertureSize = 0f,
                    UseSslr = true,
                    VisibleUi = false,
                    UseSprite = false,
                    AnyGround = false,
                    ToneMapping = ToneMappingFn.Uncharted2,
                    ToneExposure = 1.2f,
                    ToneGamma = 1f,
                    ToneWhitePoint = 2.2f,
                };
                renderer.SelectSkin(mainSkidId);
                await FormWrapperBase.PrepareAsync();
                var wrapper = new LiteShowroomFormWrapper(renderer);
                wrapper.Form.Icon = AppIconService.GetAppIcon();
                CustomShowroomWrapper.SetProperties(wrapper, renderer);
                wrapper.Run();
            } catch (Exception e) when (!e.IsCancelled()) {
                Logging.Warning(e);
                NonfatalError.Notify("Failed to load showroom", e);
            } finally {
                _showingCarInShowroom = false;
            }
        }

        public static void Initialize() {
            BbCodeBlock.AddLinkCommand(new Uri("cmd://workshop/showCarInShowroom"), new AsyncCommand<string>(ShowCarInShowroomAsync));
            BbCodeBlock.AddLinkCommand(new Uri("cmd://workshop/editUserProfile"), new AsyncCommand<string>(async userId => {
                try {
                    var client = WorkshopHolder.Client;
                    var model = WorkshopHolder.Model;
                    var userUrl = $@"/users/{userId ?? @"~me"}";
                    var currentProfile = await client.GetAsync<UserInfo>(userUrl);
                    if (await new WorkshopEditProfile(client, currentProfile).ShowDialogAsync() == true) {
                        model.LoggedInAs = await client.GetAsync<UserInfo>(userUrl);
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Failed to edit profile", e);
                }
            }));
        }
    }
}