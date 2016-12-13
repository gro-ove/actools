using System;
using System.Collections.Generic;
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

        string IWithId.Id => Key;

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
                        AsyncProgressEntry.FromStringIndetermitate($"Fallback to server #{value + 1}"));
            }
        }

        public async Task<bool> LoadAsync(Action<IEnumerable<ServerInformation>> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (SteamIdHelper.Instance.Value == null) {
                throw new Exception(ToolsStrings.Common_SteamIdIsMissing);
            }

            var data = await Task.Run(() => KunosApiProvider.TryToGetList(progress == null ? null : new ProgressConverter(progress)), cancellation);
            // if (cancellation.IsCancellationRequested) return false;

            if (data == null) {
                throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
            }

            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Applying list…"));
            callback(data);
            return true;
        }
    }

    public class MinoratingOnlineSource : IOnlineListSource {
        public const string Key = @"minorating";
        public static readonly MinoratingOnlineSource Instance = new MinoratingOnlineSource();

        string IWithId.Id => Key;

        public string DisplayName => "Minorating";

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        public async Task<bool> LoadAsync(Action<IEnumerable<ServerInformation>> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var data = await Task.Run(() => KunosApiProvider.TryToGetMinoratingList(), cancellation);
            // if (cancellation.IsCancellationRequested) return false;

            if (data == null) {
                throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
            }

            callback(data);
            return true;
        }
    }
}