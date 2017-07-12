using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.ContentRepair;
using AcManager.ContentRepair.Repairs;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.ContentRepairUi {
    public class CarObsoleteTyresRepair : CarSimpleRepairBase {
        public static readonly CarDashCameraRepair Instance = new CarDashCameraRepair();

        protected override Task<bool> FixAsync(CarObject car, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing car…"));
            return CarReplaceTyresDialog.Run(car);
        }

        protected override void Fix(CarObject car, DataWrapper data) {}

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {
            var ini = data.GetIniFile("tyres.ini");
            var section = ini["HEADER"];

            int version;
            if (int.TryParse(section.GetNonEmpty("VERSION"), out version) && version > 4) return null;

            return new ContentObsoleteSuggestion("Tyres use quite an old Tyre Model",
                    $"Used version is only v{version}, but v7 and v10 are already available. If you want, you can try to find a replacement for them manually.",
                    (p, c) => FixAsync(car, p, c)) {
                        /* we have our own warning in CarReplaceTyresDialog */
                        AffectsData = false,
                        ShowProgressDialog = false,
                        FixCaption = "Fix It…"
                    };
        }

        public override bool AffectsData => true;
    }

    public class CarObsoleteSoundUiRepair : CarSimpleRepairBase {
        public static readonly CarDashCameraRepair Instance = new CarDashCameraRepair();

        protected override Task<bool> FixAsync(CarObject car, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Fixing car…"));
            return CarReplaceTyresDialog.Run(car);
        }

        protected override void Fix(CarObject car, DataWrapper data) {}

        protected override ContentRepairSuggestion GetObsoletableAspect(CarObject car, DataWrapper data) {


            var ini = data.GetIniFile("tyres.ini");
            var section = ini["HEADER"];

            int version;
            if (int.TryParse(section.GetNonEmpty("VERSION"), out version) && version > 4) return null;

            return new ContentObsoleteSuggestion("Tyres use quite an old Tyre Model",
                    $"Used version is only v{version}, but v7 and v10 are already available. If you want, you can try to find a replacement for them manually.",
                    (p, c) => FixAsync(car, p, c)) {
                        /* we have our own warning in CarReplaceTyresDialog */
                        AffectsData = false,
                        ShowProgressDialog = false,
                        FixCaption = "Fix It…"
                    };
        }

        public override bool AffectsData => true;
    }
}