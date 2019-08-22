using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.UserControls;
using AcManager.CustomShowroom;
using AcManager.Internal;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.SemiGui;
using AcTools.Kn5File;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Tools {
    internal static class NameValueCollectionExtension {
        public static bool GetFlag(this NameValueCollection collection, [Localizable(false)] string key) {
            return collection.Keys.OfType<string>().Contains(key) &&
                    Regex.IsMatch(collection.Get(key) ?? @"on", @"^(1|yes|ok|on|true)$", RegexOptions.IgnoreCase);
        }
    }

    public static partial class ArgumentsHandler {
        private enum ArgumentHandleResult {
            Ignore,
            Successful,
            SuccessfulShow,
            Failed,
            FailedShow
        }

        private static List<string> _previousArguments;

        #region Events handling
        public static void HandlePasteEvent(Window window) {
            window.PreviewKeyDown += (sender, args) => {
                if (args.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && OnPaste()) {
                    args.Handled = true;
                }
            };
        }

        private static bool OnPaste() {
            if (VisualExtension.IsInputFocused()) {
                return false;
            }

            try {
                if (Clipboard.ContainsData(DataFormats.FileDrop)) {
                    var data = Clipboard.GetFileDropList().OfType<string>().ToList();
                    ActionExtension.InvokeInMainThreadAsync(() => ProcessArguments(data, true));
                    return true;
                }

                if (Clipboard.ContainsData(DataFormats.UnicodeText)) {
                    var list = Clipboard.GetText().ToLines();
                    if (list.Length > 0 && list.All(x => !string.IsNullOrWhiteSpace(x))) {
                        ActionExtension.InvokeInMainThreadAsync(() => ProcessArguments(list, true));
                        return true;
                    }
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }

            return false;
        }

        public static void OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            if (Application.Current?.Windows.OfType<Window>().SelectMany(VisualTreeHelperEx.FindVisualChildren<WebBlock>)
                           .Any(x => x.IsMouseOver) == true) {
                return;
            }

            (sender as IInputElement)?.Focus();
            var data = e.GetInputFiles().ToList();
            ActionExtension.InvokeInMainThreadAsync(() => e.Handled ? Task.Delay(0) : ProcessArguments(data, true));
        }

        public static void OnDragEnter(DragEventArgs e) {
            if (e.AllowedEffects.HasFlag(DragDropEffects.All) &&
                    (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText))) {
                e.Effects = DragDropEffects.All;
            }
        }
        #endregion

        public enum ShowMainWindow {
            No,
            Yes,
            Immediately,
        }

        /// <summary>
        /// Returns true if MainWindow should be shown afterwards.
        /// </summary>
        public static async Task<ShowMainWindow> ProcessArguments([CanBeNull] IEnumerable<string> arguments, bool fullPathsOnly,
                TimeSpan extraDelay = default(TimeSpan)) {
            if (arguments == null) return ShowMainWindow.No;

            var list = arguments.Select(FixProxiedRequest).ToList();
            if (_previousArguments?.SequenceEqual(list) == true) return ShowMainWindow.No;
            if (list.Count == 0) return ShowMainWindow.Immediately;

            try {
                // Why it’s here?
                _previousArguments = list;

                using (BringNewWindowsInFront()) {
                    var contentToInstall = (await list.Where(x => !IsCustomUriScheme(x)).Select(async x => Tuple.Create(x,
                            ContentInstallationManager.IsRemoteSource(x) || ContentInstallationManager.IsAdditionalContent(x, fullPathsOnly) ? x :
                                    await ContentInstallationManager.IsRemoteSourceFlexible(x))
                            ).WhenAll()).Where(x => x.Item2 != null).ToList();
                    if (contentToInstall.Any()) {
                        list = list.ApartFrom(contentToInstall.Select(x => x.Item1)).ToList();
                        if ((await contentToInstall.Select(x => ContentInstallationManager.Instance.InstallAsync(x.Item2,
                                new ContentInstallationParams(true))).WhenAll()).All(x => !x)) {
                            // TODO
                            await Task.Delay(2000);
                        }
                    }

                    var showMainWindow = false;
                    foreach (var arg in list) {
                        var result = await ProcessArgument(arg);

                        if (extraDelay != TimeSpan.Zero) {
                            await Task.Delay(extraDelay);
                        }

                        if (result == ArgumentHandleResult.FailedShow) {
                            NonfatalError.Notify(string.Format(AppStrings.Main_CannotProcessArgument, arg), AppStrings.Main_CannotProcessArgument_Commentary);
                        }

                        if (result == ArgumentHandleResult.SuccessfulShow || result == ArgumentHandleResult.FailedShow) {
                            showMainWindow = true;
                        }
                    }

                    return showMainWindow ? ShowMainWindow.Yes : ShowMainWindow.No;
                }
            } finally {
                _previousArguments = null;
            }
        }

        private static IDisposable BringNewWindowsInFront() {
            DpiAwareWindow.NewWindowCreated += OnNewWindow;
            return new ActionAsDisposable(() => { DpiAwareWindow.NewWindowCreated -= OnNewWindow; });

            void OnNewWindow(object sender, EventArgs args) {
                if (sender is DpiAwareWindow window) {
                    window.Loaded += OnWindowLoaded;
                }
            }

            void OnWindowLoaded(object sender, RoutedEventArgs args) {
                var window = (DpiAwareWindow)sender;
                window.Loaded -= OnWindowLoaded;
                window.BringToFront();
            }
        }

        private static string FixProxiedRequest(string argument) {
            var prefix = $@"{InternalUtils.MainApiDomain}/s/q:";
            if (argument?.StartsWith(prefix) == true) {
                return CustomUriSchemeHelper.UriScheme + @"//" + argument.SubstringExt(prefix.Length);
            }
            return argument;
        }

        public static bool IsCustomUriScheme(string argument) {
            return argument.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.InvariantCultureIgnoreCase);
        }

        private static async Task<ArgumentHandleResult> ProcessArgument(string argument) {
            Logging.Debug($"Processing argument: {argument}");
            try {
                argument = FixProxiedRequest(argument);
                if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;

                if (IsCustomUriScheme(argument)) {
                    return await ProcessUriRequest(argument);
                }

                return await ProcessInputFile(argument);
            } finally {
                Logging.Debug($"Argument processed: {argument}");
            }
        }

        /// <summary>
        /// Loads remote file using FlexibleLoader.
        /// </summary>
        /// <param name="argument">Remote source.</param>
        /// <param name="name">Preferable name.</param>
        /// <param name="extension">Extension for loaded file.</param>
        /// <returns>Path to loaded file.</returns>
        /// <exception cref="Exception">Thrown if failed or cancelled.</exception>
        [ItemNotNull]
        private static async Task<string> LoadRemoveFile(string argument, string name = null, string extension = null) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                return await FlexibleLoader.LoadAsyncTo(argument, (url, information) => {
                    var filename = Path.Combine(SettingsHolder.Content.TemporaryFilesLocationValue, name + extension);
                    return new FlexibleLoaderDestination(filename, true);
                }, null, information => {
                    if (information.FileName != null) {
                        waiting.Title = $@"Loading {information.FileName}…";
                    }
                }, null, waiting, waiting.CancellationToken);
            }
        }

        /// <summary>
        /// Loads remote file using FlexibleLoader.
        /// </summary>
        /// <param name="argument">Remote source.</param>
        /// <param name="destination">Destination.</param>
        /// <exception cref="Exception">Thrown if failed or cancelled.</exception>
        private static async Task LoadRemoveFileToNew(string argument, string destination) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                await FlexibleLoader.LoadAsyncTo(argument, (url, information) => new FlexibleLoaderDestination(destination, false), null, information => {
                    if (information.FileName != null) {
                        waiting.Title = $@"Loading {information.FileName}…";
                    }
                }, null, waiting, waiting.CancellationToken);
            }
        }

        private static async Task<ArgumentHandleResult> ProcessInputFile(string filename) {
            bool isDirectory;
            try {
                if (!FileUtils.Exists(filename)) return ArgumentHandleResult.Failed;
                isDirectory = FileUtils.IsDirectory(filename);
            } catch (Exception) {
                return ArgumentHandleResult.Failed;
            }

            if (!isDirectory && filename.EndsWith(@".acreplay", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetDirectoryName(filename)?.Equals(AcPaths.GetReplaysDirectory(), StringComparison.OrdinalIgnoreCase) == true) {
                await GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Filename = filename
                }));
                return ArgumentHandleResult.Successful;
            }

            if (!isDirectory && filename.EndsWith(@".kn5", StringComparison.OrdinalIgnoreCase)) {
                if (Keyboard.Modifiers == ModifierKeys.Alt) {
                    try {
                        Kn5.FbxConverterLocation = PluginsManager.Instance.GetPluginFilename("FbxConverter", "FbxConverter.exe");
                        var kn5 = Kn5.FromFile(filename);
                        var destination = FileUtils.EnsureUnique(Path.Combine(Path.GetDirectoryName(filename) ?? @".", "unpacked"));
                        var name = kn5.RootNode.Name.StartsWith(@"FBX: ") ? kn5.RootNode.Name.Substring(5) :
                                @"model.fbx";
                        Directory.CreateDirectory(destination);
                        await kn5.ExportFbxWithIniAsync(Path.Combine(destination, name));
                        var textures = Path.Combine(destination, "texture");
                        Directory.CreateDirectory(textures);
                        await kn5.ExportTexturesAsync(textures);
                        Process.Start(destination);
                    } catch (Exception e) {
                        Logging.Error(e);
                        return ArgumentHandleResult.FailedShow;
                    }
                } else {
                    await CustomShowroomWrapper.StartAsync(filename);
                }

                return ArgumentHandleResult.Successful;
            }

            return ArgumentHandleResult.FailedShow;
        }
    }
}