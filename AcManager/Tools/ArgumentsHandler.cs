using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Controls;
using AcManager.CustomShowroom;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Tools {
    internal static class NameValueCollectionExtension {
        public static bool GetFlag(this NameValueCollection collection, [Localizable(false)] string key) {
            return collection.Keys.OfType<string>().Contains(key) &&
                    Regex.IsMatch(collection.Get(key) ?? @"on", @"^(1|yes|ok|on|true)$", RegexOptions.IgnoreCase);
        }
    }

    public partial class ArgumentsHandler {
        public async Task<ArgumentHandleResult> ProcessArgument(string argument) {
            if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;

            if (argument.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.InvariantCultureIgnoreCase)) {
                return await ProcessUriRequest(argument);
            }

            if (argument.StartsWith(@"http:", StringComparison.OrdinalIgnoreCase) || argument.StartsWith(@"https:", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith(@"ftp:", StringComparison.OrdinalIgnoreCase)) {
                argument = await LoadRemoveFile(argument);
                if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;
            }

            try {
                if (!FileUtils.Exists(argument)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }

            return await ProcessInputFile(argument);
        }

        private async Task<string> LoadRemoveFile(string argument, string name = null, string extension = null) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                return await FlexibleLoader.LoadAsync(argument, name, extension, waiting, waiting.CancellationToken);
            }
        }

        private async Task<string> LoadRemoveFileTo(string argument, string destination) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                return await FlexibleLoader.LoadAsyncTo(argument, destination, waiting, waiting.CancellationToken);
            }
        }

        private async Task<ArgumentHandleResult> ProcessInputFile(string filename) {
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

            try {
                new InstallAdditionalContentDialog(filename).ShowDialog();
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Arguments_CannotInstallAdditionalContent, e);
                return ArgumentHandleResult.Failed;
            }

            return ArgumentHandleResult.Successful;
        }
    }
}