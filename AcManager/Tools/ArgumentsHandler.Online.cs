using System;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Pages.Drive;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Tools {
    public static partial class ArgumentsHandler {
        private static async Task<ArgumentHandleResult> ProgressRaceOnline(NameValueCollection p) {
            /* required arguments */
            var ip = p.Get(@"ip");
            var port = FlexibleParser.TryParseInt(p.Get(@"port"));
            var httpPort = FlexibleParser.TryParseInt(p.Get(@"httpPort"));
            var carId = p.Get(@"car");

            /* optional arguments */
            var allowWithoutSteamId = p.GetFlag("allowWithoutSteamId");
            var carSkinId = p.Get(@"skin");
            var trackId = p.Get(@"track");
            var name = p.Get(@"name");
            var nationality = p.Get(@"nationality");
            var password = p.Get(@"plainPassword");
            var encryptedPassword = p.Get(@"password");

            if (string.IsNullOrWhiteSpace(ip)) {
                throw new InformativeException("IP is missing");
            }

            if (!port.HasValue) {
                throw new InformativeException("Port is missing or is in invalid format");
            }

            if (!httpPort.HasValue) {
                throw new InformativeException("HTTP port is missing or is in invalid format");
            }

            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(encryptedPassword)) {
                password = OnlineServer.DecryptSharedPassword(ip, httpPort.Value, encryptedPassword);
            }

            if (string.IsNullOrWhiteSpace(carId)) {
                throw new InformativeException("Car ID is missing");
            }

            var car = CarsManager.Instance.GetById(carId);
            if (car == null) {
                throw new InformativeException("Car is missing");
            }

            if (!string.IsNullOrWhiteSpace(carSkinId) && car.GetSkinById(carSkinId) == null) {
                throw new InformativeException("Car skin is missing");
            }

            var track = string.IsNullOrWhiteSpace(trackId) ? null : TracksManager.Instance.GetLayoutByKunosId(trackId);
            if (!string.IsNullOrWhiteSpace(trackId) && track == null) {
                throw new InformativeException("Track is missing");
            }


            if (!SteamIdHelper.Instance.IsReady && !allowWithoutSteamId) {
                throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);
            }

            await GameWrapper.StartAsync(new Game.StartProperties {
                BasicProperties = new Game.BasicProperties {
                    CarId = carId,
                    TrackId = track?.MainTrackObject.Id ?? @"imola",
                    TrackConfigurationId = track?.LayoutId,
                    CarSkinId = carSkinId,
                    DriverName = name,
                    DriverNationality = nationality
                },
                ModeProperties = new Game.OnlineProperties {
                    Guid = SteamIdHelper.Instance.Value,
                    ServerIp = ip,
                    ServerPort = port.Value,
                    ServerHttpPort = httpPort.Value,
                    Password = password,
                    RequestedCar = carId
                }
            });

            return ArgumentHandleResult.Successful;
        }

        private class FakeSource : IOnlineListSource {
            private ServerInformation _information;

            public FakeSource(string ip, int httpPort) {
                _information = new ServerInformation { Ip = ip, PortHttp = httpPort };
                Id = _information.Id;
            }

            public string Id { get; }

            public string DisplayName => "Temporary Source";

            public event EventHandler Obsolete {
                add { }
                remove { }
            }

            public Task<bool> LoadAsync(ListAddCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                // This source will load provided server, but only once — call .ReloadAsync() and server will be nicely removed.
                callback(new[] { _information }.NonNull());
                _information = null;
                return Task.FromResult(true);
            }
        }

        private static async Task<ArgumentHandleResult> ProgressRaceOnlineJoin(NameValueCollection p) {
            /* required arguments */
            var ip = p.Get(@"ip");
            var httpPort = FlexibleParser.TryParseInt(p.Get(@"httpPort"));
            var password = p.Get(@"plainPassword");
            var encryptedPassword = p.Get(@"password");

            if (string.IsNullOrWhiteSpace(ip)) {
                throw new InformativeException("IP is missing");
            }

            if (!httpPort.HasValue) {
                throw new InformativeException("HTTP port is missing or is in invalid format");
            }

            OnlineManager.EnsureInitialized();

            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(encryptedPassword)) {
                password = OnlineServer.DecryptSharedPassword(ip, httpPort.Value, encryptedPassword);
            }

            var list = OnlineManager.Instance.List;
            var source = new FakeSource(ip, httpPort.Value);
            var wrapper = new OnlineSourceWrapper(list, source);

            ServerEntry server;

            using (var waiting = new WaitingDialog()) {
                waiting.Report(ControlsStrings.Common_Loading);

                await wrapper.EnsureLoadedAsync();
                server = list.GetByIdOrDefault(source.Id);
                if (server == null) {
                    throw new Exception(@"Unexpected");
                }
            }

            if (password != null) {
                server.Password = password;
            }

            var content = new OnlineServer(server) {
                Margin = new Thickness(0, 0, 0, -38),
                ToolBar = { FitWidth = true },

                // Values taken from ModernDialog.xaml
                // TODO: Extract them to some style?
                Title = { FontSize = 24, FontWeight = FontWeights.Light, Margin = new Thickness(6, 0, 0, 8) }
            };

            content.Title.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);

            var dlg = new ModernDialog {
                ShowTitle = false,
                Content = content,
                MinHeight = 400,
                MinWidth = 450,
                MaxHeight = 99999,
                MaxWidth = 700,
                Padding = new Thickness(0),
                ButtonsMargin = new Thickness(8),
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = @".OnlineServerDialog"
            };

            dlg.SetBinding(Window.TitleProperty, new Binding {
                Path = new PropertyPath(nameof(server.DisplayName)),
                Source = server
            });

            dlg.ShowDialog();
            await wrapper.ReloadAsync(true);
            return ArgumentHandleResult.Successful;
        }
    }
}