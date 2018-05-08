using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcTools.Render.Base;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Special;
using AcTools.Render.Wrapper;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Size = System.Drawing.Size;

namespace AcManager.CustomShowroom {
    public class LiteShowroomFormWrapperWithUiShots : LiteShowroomFormWrapper {
        public LiteShowroomFormWrapperWithUiShots(ForwardKn5ObjectRenderer renderer, string title = "Lite Showroom", int width = 1600, int height = 900)
                : base(renderer, title, width, height) { }

        private bool _busy;

        protected sealed override void OnMouseWheel(object sender, MouseEventArgs e) {
            if (_busy) return;
            base.OnMouseWheel(sender, e);
        }

        protected sealed override void OnTick(object sender, TickEventArgs args) {
            if (_busy) return;
            base.OnTick(sender, args);
        }

        protected sealed override void UpdateSize() {
            if (_busy) return;
            base.UpdateSize();
        }

        protected sealed override void OnClick() {
            if (_busy) return;
            base.OnClick();
            OnClickOverride();
        }

        protected virtual void OnClickOverride() { }

        protected sealed override void OnKeyUp(object sender, KeyEventArgs args) {
            if (_busy) return;
            OnKeyUpOverride(args);
            if (!args.Handled) {
                base.OnKeyUp(sender, args);
            }
        }

        protected virtual void OnKeyUpOverride(KeyEventArgs args) { }

        private async Task ShotAsync(Action<IProgress<Tuple<string, double?>>, CancellationToken> action, CancellationToken cancellationToken) {
            if (_busy) return;
            _busy = true;

            try {
                using (var waiting = new WaitingDialog { WindowStartupLocation = WindowStartupLocation.CenterScreen, Owner = null })
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, waiting.CancellationToken)) {
                    waiting.Report(AsyncProgressEntry.Indetermitate);
                    Renderer.IsPaused = true;

                    try {
                        await Task.Run(() => {
                            // ReSharper disable once AccessToDisposedClosure
                            action(waiting, linked.Token);
                        });
                    } finally {
                        Renderer.IsPaused = false;
                    }
                }
            } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                NonfatalError.Notify("Can’t build image", e);
            } finally {
                _busy = false;
                UpdateSize();
            }
        }

        public Task SplitShotAsync(Size size, bool downscale, string filename, RendererShotFormat format, CancellationToken cancellationToken) {
            return ShotAsync((progress, token) => { SplitShotInner(size, downscale, filename, format, progress, token); }, cancellationToken);
        }

        public Task ShotAsync(Size size, bool downscale, string filename, RendererShotFormat format, CancellationToken cancellationToken) {
            return ShotAsync((progress, token) => { ShotInner(size, downscale, filename, format, progress, token); }, cancellationToken);
        }

        private static bool _warningShown;

        protected override void SplitShotPieces(Size size, bool downscale, string filename, RendererShotFormat format,
                IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            /*if (format.IsHdr()) {
                throw new NotSupportedException("Can’t make an HDR-screenshot in super-resolution");
            }*/

            PiecesBlender.OptionMaxCacheSize = SettingsHolder.Plugins.MontageVramCache;

            var plugin = PluginsManager.Instance.GetById(KnownPlugins.ImageMontage);
            if (plugin == null || !plugin.IsReady) {
                if (!_warningShown) {
                    _warningShown = true;
                    FirstFloor.ModernUI.Windows.Toast.Show("Montage plugin not installed", "You’ll have to join pieces manually");
                }

                OptionMontageMemoryLimit = SettingsHolder.Plugins.MontageMemoryLimit;
                base.SplitShotPieces(size, downscale, filename, format, progress, cancellation);
            } else {
                var dark = (DarkKn5ObjectRenderer)Renderer;
                var destination = Path.Combine(SettingsHolder.Plugins.MontageTemporaryDirectory, Path.GetFileNameWithoutExtension(filename) ?? "image");

                // For pre-smoothed files, in case somebody would want to use super-resolution with SSLR/SSAO
                DarkKn5ObjectRenderer.OptionTemporaryDirectory = destination;

                var information = dark.SplitShot(size.Width, size.Height, downscale ? 0.5d : 1d, destination, format,
                        progress.SubrangeTuple(0.001, 0.95, "Rendering ({0})…"), cancellation);

                progress?.Report(new Tuple<string, double?>("Combining pieces…", 0.97));

                var magick = plugin.GetFilename("magick.exe");
                if (!File.Exists(magick)) {
                    magick = plugin.GetFilename("montage.exe");
                    FirstFloor.ModernUI.Windows.Toast.Show("Montage plugin is obsolete", "Please, update it, and it’ll consume twice less power");
                }

                Environment.SetEnvironmentVariable(@"MAGICK_TMPDIR", destination);
                using (var process = new Process {
                    StartInfo = {
                        FileName = magick,
                        WorkingDirectory = destination,
                        Arguments = information.GetMagickCommand(SettingsHolder.Plugins.MontageMemoryLimit),
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                }) {
                    process.Start();
                    process.WaitForExit(600000);
                    if (!process.HasExited) {
                        process.Kill();
                    }
                }

                progress?.Report(new Tuple<string, double?>("Cleaning up…", 0.99));

                var result = Path.Combine(destination, $"out{format.GetExtension()}");
                if (!File.Exists(result)) {
                    throw new Exception("Combining failed, file not found");
                }

                File.Move(result, filename);
                Directory.Delete(destination, true);
            }
        }

        protected override void SplitShot(Size size, bool downscale, string filename, RendererShotFormat format) {
            SplitShotAsync(size, downscale, filename, format, default(CancellationToken));
        }

        protected override void Shot(Size size, bool downscale, string filename, RendererShotFormat format) {
            ShotAsync(size, downscale, filename, format, default(CancellationToken));
        }
    }
}