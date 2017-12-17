using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.CustomShowroom;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using OxyPlot.Reporting;
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
        public static void OnPaste() {
            if (Keyboard.FocusedElement is TextBoxBase || Keyboard.FocusedElement is ComboBox) {
                return;
            }

            try {
                if (Clipboard.ContainsData(DataFormats.FileDrop)) {
                    var data = Clipboard.GetFileDropList().OfType<string>().ToList();
                    ActionExtension.InvokeInMainThreadAsync(() => ProcessArguments(data));
                } else if (Clipboard.ContainsData(DataFormats.UnicodeText)) {
                    var list = Clipboard.GetText().SplitLines();
                    ActionExtension.InvokeInMainThreadAsync(() => ProcessArguments(list));
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public static void OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            (sender as IInputElement)?.Focus();
            var data = e.GetInputFiles().ToList();
            ActionExtension.InvokeInMainThreadAsync(() => e.Handled ? Task.Delay(0) : ProcessArguments(data));
        }

        public static void OnDragEnter(object sender, DragEventArgs e) {
            if (e.AllowedEffects.HasFlag(DragDropEffects.All) &&
                    (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText))) {
                e.Effects = DragDropEffects.All;
            }
        }
        #endregion

        public enum ShowMainWindow {
            No, Yes, Immediately,
        }

        /// <summary>
        /// Returns true if MainWindow should be shown afterwards.
        /// </summary>
        public static async Task<ShowMainWindow> ProcessArguments([CanBeNull] IEnumerable<string> arguments, TimeSpan extraDelay = default(TimeSpan)) {
            if (arguments == null) return ShowMainWindow.No;

            var list = arguments.ToList();
            if (_previousArguments?.SequenceEqual(list) == true) return ShowMainWindow.No;
            if (list.Count == 0) return ShowMainWindow.Immediately;

            _previousArguments = list;

            var contentToInstall = (await list.Select(async x => Tuple.Create(x,
                    ContentInstallationManager.IsRemoteSource(x) || ContentInstallationManager.IsAdditionalContent(x) ? x :
                            await ContentInstallationManager.IsRemoteSourceFlexible(x))
                    ).WhenAll()).Where(x => x.Item2 != null).ToList();
            if (contentToInstall.Any()) {
                list = list.ApartFrom(contentToInstall.Select(x => x.Item1)).ToList();
                if ((await contentToInstall.Select(x => ContentInstallationManager.Instance.InstallAsync(x.Item2, new ContentInstallationParams {
                    AllowExecutables = true
                })).WhenAll()).All(x => !x)) {
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

            _previousArguments = null;
            return showMainWindow ? ShowMainWindow.Yes : ShowMainWindow.No;
        }

        public static bool IsCustomUriScheme(string argument) {
            return argument.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.InvariantCultureIgnoreCase);
        }

        private static async Task<ArgumentHandleResult> ProcessArgument(string argument) {
            if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;

            if (IsCustomUriScheme(argument)) {
                return await ProcessUriRequest(argument);
            }

            /*if (ContentInstallationManager.IsRemoteSource(argument)) {
                //argument = await LoadRemoveFile(argument);
                //if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;
                return await ProcessInstallableContent(argument);
            }*/

            /*try {
                if (!FileUtils.Exists(argument)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }*/

            return await ProcessInputFile(argument);
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
                }, information => {
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
                await FlexibleLoader.LoadAsyncTo(argument, (url, information) => new FlexibleLoaderDestination(destination, false), information => {
                    if (information.FileName != null) {
                        waiting.Title = $@"Loading {information.FileName}…";
                    }
                }, null, waiting, waiting.CancellationToken);
            }
        }

        private static async Task<ArgumentHandleResult> ProcessInputFile(string filename) {
            var isDirectory = FileUtils.IsDirectory(filename);
            if (!isDirectory && filename.EndsWith(@".acreplay", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetDirectoryName(filename)?.Equals(AcPaths.GetReplaysDirectory(), StringComparison.OrdinalIgnoreCase) == true) {
                await GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Filename = filename
                }));
                return ArgumentHandleResult.Successful;
            }

            if (!isDirectory && filename.EndsWith(@".kn5", StringComparison.OrdinalIgnoreCase)) {
                await CustomShowroomWrapper.StartAsync(filename);
                return ArgumentHandleResult.Successful;
            }

            return ArgumentHandleResult.FailedShow;
        }

        /*private static async Task<ArgumentHandleResult> ProcessInstallableContent(string source) {
            try {
                await ContentInstallationManager.Instance.InstallAsync(source);
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Arguments_CannotInstallAdditionalContent, e);
                return ArgumentHandleResult.Failed;
            }

            return ArgumentHandleResult.Successful;
        }*/
    }
}