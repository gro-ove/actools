using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class KunosOnlineSource : IOnlineListSource {
        public const string Key = @"kunos";
        public static readonly KunosOnlineSource Instance = new KunosOnlineSource();

        string IWithId<string>.Id => Key;

        public string DisplayName => "Kunos";

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        private class ProgressConverter : IProgress<int> {
            private readonly IProgress<AsyncProgressEntry> _target;

            public ProgressConverter([NotNull] IProgress<AsyncProgressEntry> target) {
                _target = target;
            }

            public void Report(int value) {
                _target.Report(value == 0 ? AsyncProgressEntry.Indetermitate :
                        AsyncProgressEntry.FromStringIndetermitate(string.Format(ToolsStrings.OnlineSource_Loading_Fallback, value + 1)));
            }
        }

        public async Task<bool> LoadAsync(ListAddAsyncCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (SteamIdHelper.Instance.Value == null) {
                throw new Exception(ToolsStrings.Common_SteamIdIsMissing);
            }

            var data = await Task.Run(
                    () => KunosApiProvider.TryToGetList(progress == null ? null : new ProgressConverter(progress)), cancellation);
            // if (cancellation.IsCancellationRequested) return false;

            if (data == null) {
                throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
            }

            progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.OnlineSource_Loading_FinalStep));
            await callback(data).ConfigureAwait(false);
            return true;
        }
    }
}