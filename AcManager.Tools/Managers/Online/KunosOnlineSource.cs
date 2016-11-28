using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class KunosOnlineSource : IOnlineSource {
        public const string Key = @"kunos";
        public static readonly KunosOnlineSource Instance = new KunosOnlineSource();

        string IOnlineSource.Key => Key;

        public bool IsBackgroundLoadable => false;

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

        public async Task LoadAsync(Action<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            Logging.Here();

            var data = await Task.Run(() => KunosApiProvider.TryToGetList(progress == null ? null : new ProgressConverter(progress)), cancellation);
            if (cancellation.IsCancellationRequested) return;

            foreach (var information in data) {
                callback(information);
            }
        }
    }

    public class MinoratingOnlineSource : IOnlineSource {
        public const string Key = @"minorating";
        public static readonly MinoratingOnlineSource Instance = new MinoratingOnlineSource();

        string IOnlineSource.Key => Key;

        public bool IsBackgroundLoadable => false;

        public async Task LoadAsync(Action<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            Logging.Here();

            var data = await Task.Run(() => KunosApiProvider.TryToGetMinoratingList(), cancellation);
            if (cancellation.IsCancellationRequested) return;

            foreach (var information in data) {
                callback(information);
            }
        }
    }

    public class LanOnlineSource : IOnlineSource {
        public const string Key = @"lan";
        public static readonly LanOnlineSource Instance = new LanOnlineSource();

        string IOnlineSource.Key => Key;

        public bool IsBackgroundLoadable => true;

        public Task LoadAsync(Action<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            Logging.Here();
            return KunosApiProvider.TryToGetLanListAsync(callback, progress, cancellation);
        }
    }
}