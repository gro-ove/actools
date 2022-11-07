using System;
using System.IO;
using System.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Tools.Managers {
    public class CarsManager : AcManagerNew<CarObject> {
        public static CarsManager Instance { get; private set; }

        public static CarsManager Initialize() {
            if (Instance != null) throw new Exception(@"Already initialized");
            return Instance = new CarsManager();
        }

        private CarsManager() {
            CupClient.Instance?.Register(this, CupContentType.Car);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.CarsDirectories;

        public override CarObject GetDefault() {
            return GetById(@"abarth500") ?? base.GetDefault();
        }

        protected override void OnListUpdate() {
            SuggestionLists.RebuildCarClassesList();
            SuggestionLists.RebuildCarTagsList();
        }

        protected override bool Filter(string id, string filename) {
            if (id.StartsWith(@"__cm_tmp_")) {
                return false;
            }

            if (id.StartsWith(@"ks_")) {
                var uiCarJson = Path.Combine(filename, @"ui", @"ui_car.json");
                if (!File.Exists(uiCarJson)) return false;
            }

            return base.Filter(id, filename);
        }

        protected override TaskbarHolder GetTaskbarProgress() {
            return TaskbarService.Create("Loading cars", 5);
        }

        private static readonly string[] WatchedFiles = {
            @"data.acd",
            @"logo.png",
            @"ui",
            @"ui\badge.png",
            @"ui\upgrade.png",
            @"ui\ui_car.json",
            @"ui\cm_paintshop.json",
            @"ui\camera_trajectory.json",
        };

        private static readonly string[] WatchedSkinFileNames = {
            @"livery.png",
            @"preview.jpg",
            @"preview.png",
            @"ui_skin.json"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;

            var inner = filename.SubstringExt(objectLocation.Length + 1).ToLowerInvariant();
            if (WatchedFiles.ArrayContains(inner)) {
                return false;
            }

            if (inner.StartsWith(@"data\")
                    || inner.StartsWith(@"ui\cm_paintshop\")
                    || inner.StartsWith(@"sfx\") && inner.EndsWith(@".bank")) return false;

            if (!inner.StartsWith(@"skins\") // texture\…
                    || inner.Count(x => x == '\\') > 2 // skins\abc\def\file.png
                    || inner.EndsWith(@".dds", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            var name = Path.GetFileName(inner);
            return !WatchedSkinFileNames.ArrayContains(name.ToLowerInvariant());
        }

        protected override CarObject CreateAcObject(string id, bool enabled) {
            return new CarObject(this, id, enabled);
        }
    }
}
