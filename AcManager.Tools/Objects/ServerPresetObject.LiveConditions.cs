using AcManager.Tools.ServerPlugins;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        private bool _CmPluginLiveConditions;

        public bool CmPluginLiveConditions {
            get => _CmPluginLiveConditions;
            set => Apply(value, ref _CmPluginLiveConditions, () => {
                if (Loaded) {
                    Changed = true;
                }
            });
        }

        public LiveConditionsServerPlugin.LiveConditionParams CmPluginLiveConditionsParams { get; } = new LiveConditionsServerPlugin.LiveConditionParams();
    }
}