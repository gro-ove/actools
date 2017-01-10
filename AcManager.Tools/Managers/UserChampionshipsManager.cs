using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;

namespace AcManager.Tools.Managers {
    public class UserChampionshipsManager : AcManagerFileSpecific<UserChampionshipObject> {
        private static UserChampionshipsManager _instance;

        public static UserChampionshipsManager Instance => _instance ?? (_instance = new UserChampionshipsManager());

        public override string SearchPattern => @"*.champ";

        public override IAcDirectories Directories => AcRootDirectory.Instance.UserChampionshipsDirectories;

        protected override UserChampionshipObject CreateAcObject(string id, bool enabled) {
            return new UserChampionshipObject(this, id, enabled);
        }
    }
}