using JetBrains.Annotations;

namespace AcManager.Workshop {
    public static class WorkshopHolder {
        private static WorkshopClient _client;
        private static WorkshopModel _model;

        [NotNull]
        public static WorkshopClient Client => GetClient();

        [NotNull]
        public static WorkshopModel Model => GetModel();

        private static void Initialize() {
            if (_client == null) {
                _client = new WorkshopClient("http://192.168.1.10:3000");
                _model = new WorkshopModel(_client);
            }
        }

        [NotNull]
        public static WorkshopClient GetClient() {
            Initialize();
            return _client;
        }

        [NotNull]
        public static WorkshopModel GetModel() {
            Initialize();
            return _model;
        }
    }
}