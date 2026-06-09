using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Managers.Online {
    public sealed class ThirdPartyOnlineSource : Displayable, IOnlineListSource, IDisposable {
        public const string Prefix = @"c:";

        public static string To36(int value) {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            var result = "";
            while (value > 0) {
                result = chars[value % 36] + result;
                value /= 36;
            }
            return result;
        }

        public ThirdPartyOnlineSource(bool isBuiltIn, string url, string displayName) {
            IsBuiltIn = isBuiltIn;
            Url = url;
            Id = Prefix + To36(Math.Abs(url.GetHashCode()));
            DisplayName = displayName;
            _isEnabled = ValuesStorage.Get($".os.{Id}", true);
            if (_isEnabled) {
                OnEnabledChange(true);
            }
        }

        public void Dispose() {
            OnlineManager.Unregister(this);
        }

        public string Url { get; }
        
        private bool _isBuiltIn;

        public bool IsBuiltIn {
            get => _isBuiltIn;
            set => Apply(value, ref _isBuiltIn);
        }

        private string _description;

        public string Description {
            get => _description;
            set => Apply(value, ref _description);
        }

        private string _flags;

        public string Flags {
            get => _flags;
            set => Apply(value, ref _flags);
        }

        public bool HasFlag(string flag) {
            return _flags?.Split(',').Any(x => x.Trim() == flag) == true;
        }

        private void OnEnabledChange(bool value) {
            if (value && Url.IsWebUrl()) {
                OnlineManager.Register(this);
            } else {
                OnlineManager.Unregister(this);
            }
        }

        private bool _isEnabled;

        public bool IsEnabled {
            get => _isEnabled;
            set => Apply(value, ref _isEnabled, () => {
                OnEnabledChange(value);
                ValuesStorage.Set($".os.{Id}", value);
            });
        }

        public string Id { get; }

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        public async Task<bool> LoadAsync(ListAddAsyncCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress,
                CancellationToken cancellation) {
            var data = await Task.Run(() => KunosApiProvider.TryToGetThirdPartyList(Url), cancellation);
            // if (cancellation.IsCancellationRequested) return false;

            if (data == null) {
                throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
            }

            Logging.Debug($"Third-party source {Url}: {data.Length}");
            await callback(data).ConfigureAwait(false);
            return true;
        }
    }
}