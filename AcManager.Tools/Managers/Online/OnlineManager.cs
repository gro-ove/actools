using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;

namespace AcManager.Tools.Managers.Online {
    public enum OnlineManagerStatus {
        Error, Loading, Ready
    }

    public class OnlineManager : BaseOnlineManager, IProgress<int> {
        private static OnlineManager _instance;

        public static OnlineManager Instance => _instance ?? (_instance = new OnlineManager());

        protected override async Task InnerLoadAsync() {
            Status = OnlineManagerStatus.Loading;

            try {
                var data = await Task.Run(() => KunosApiProvider.TryToGetList(this)?.Select(x => new ServerEntry(x)).ToList());
                if (data != null) {
                    List.ReplaceEverythingBy(data);
                }

                Status = OnlineManagerStatus.Ready;
            } catch (Exception) {
                Status = OnlineManagerStatus.Error;
            }
        }

        public void Report(int value) {
            if (value == 0) {
                LoadingState = null;
            } else {
                LoadingState = $"Fallback to server #{value + 1}";
            }
        }
    }
}