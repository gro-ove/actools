using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Controls;
using AcManager.CustomShowroom;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
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

        /// <summary>
        /// Returns true if MainWindow should be shown afterwards.
        /// </summary>
        public static async Task<bool> ProcessArguments(IEnumerable<string> arguments, TimeSpan extraDelay = default(TimeSpan)) {
            var showMainWindow = false;

            var list = arguments.ToList();
            if (_previousArguments?.SequenceEqual(list) == true) {
                return false;
            }

            _previousArguments = list;

            var remote = (await list.Select(async x => Tuple.Create(x,
                    ContentInstallationManager.IsRemoteSource(x) || ContentInstallationManager.IsAdditionalContent(x) ? x :
                            await ContentInstallationManager.IsRemoteSourceFlexible(x))
                    ).WhenAll()).Where(x => x.Item2 != null).ToList();

            if (remote.Any()) {
                list = list.ApartFrom(remote.Select(x => x.Item1)).ToList();
                if ((await remote.Select(x => ContentInstallationManager.Instance.InstallAsync(x.Item2, new ContentInstallationParams {
                    AllowExecutables = true
                })).WhenAll()).All(x => !x)) {
                    await Task.Delay(ContentInstallationManager.OptionFailedDelay);
                }
            }

            foreach (var arg in list) {
                var result = await ProcessArgument(arg);

                if (extraDelay != TimeSpan.Zero) {
                    await Task.Delay(extraDelay);
                }

                if (result == ArgumentHandleResult.FailedShow) {
                    NonfatalError.Notify(AppStrings.Main_CannotProcessArgument, AppStrings.Main_CannotProcessArgument_Commentary);
                }

                if (result == ArgumentHandleResult.SuccessfulShow || result == ArgumentHandleResult.FailedShow) {
                    showMainWindow = true;
                }
            }

            _previousArguments = null;
            return showMainWindow;
        }

        private static async Task<ArgumentHandleResult> ProcessArgument(string argument) {
            if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;

            if (argument.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.InvariantCultureIgnoreCase)) {
                return await ProcessUriRequest(argument);
            }

            /*if (ContentInstallationManager.IsRemoteSource(argument)) {
                //argument = await LoadRemoveFile(argument);
                //if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;
                return await ProcessInstallableContent(argument);
            }*/

            try {
                if (!FileUtils.Exists(argument)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }

            return await ProcessInputFile(argument);
        }

        private static async Task<string> LoadRemoveFile(string argument, string name = null, string extension = null) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                return await FlexibleLoader.TryToLoadAsync(argument, name, extension, true, null, waiting, information => {
                    if (information.FileName != null) {
                        waiting.Title = $@"Loading {information.FileName}…";
                    }
                }, waiting.CancellationToken);
            }
        }

        private static async Task<string> LoadRemoveFileTo(string argument, string destination) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                return await FlexibleLoader.TryToLoadAsyncTo(argument, destination, waiting, information => {
                    if (information.FileName != null) {
                        waiting.Title = $@"Loading {information.FileName}…";
                    }
                }, waiting.CancellationToken);
            }
        }

        private static async Task<ArgumentHandleResult> ProcessInputFile(string filename) {
            var isDirectory = FileUtils.IsDirectory(filename);
            if (!isDirectory && filename.EndsWith(@".acreplay", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetDirectoryName(filename)?.Equals(FileUtils.GetReplaysDirectory(), StringComparison.OrdinalIgnoreCase) == true) {
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