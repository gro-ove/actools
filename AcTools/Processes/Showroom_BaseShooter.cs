using System;
using System.Collections.Generic;
using AcTools.Utils;
using System.IO;
using System.Linq;
using AcTools.Utils.Helpers;

namespace AcTools.Processes {
    public partial class Showroom {
        public abstract class BaseShotter : IDisposable {
            public string OutputDirectory { get; private set; }

            public string AcRoot, CarId, ShowroomId;
            public string[] SkinIds;
            public string Filter, TemporaryDirectory;
            public bool UseBmp, DisableWatermark, DisableSweetFx, SpecialResolution, MaximizeVideoSettings;
            public bool? Fxaa;

            private bool _prepared;

            private readonly List<IDisposable> _changes = new List<IDisposable>();

            protected virtual void Prepare() {
                if (_prepared) return;
                _prepared = true;

                if (AcRoot == null) throw new Exception("AcRoot is null");
                if (CarId == null) throw new Exception("CarId is null");
                if (ShowroomId == null) throw new Exception("ShowroomId is null");

                if (!Directory.Exists(FileUtils.GetShowroomDirectory(AcRoot, ShowroomId))) {
                    throw new Exception("Showroom not found");
                }

                if (UseBmp) {
                    _changes.Add(new ScreenshotFormatChange(AcRoot, "BMP"));
                }

                if (DisableWatermark) {
                    _changes.Add(new DisableShowroomWatermarkChange(AcRoot));
                }

                if (DisableSweetFx) {
                    _changes.Add(new DisableSweetFxChange(AcRoot));
                }

                _changes.Add(new VideoIniChange(Filter, Fxaa, SpecialResolution, MaximizeVideoSettings));

                if (TemporaryDirectory == null) {
                    TemporaryDirectory = Path.GetTempPath();
                }

                OutputDirectory = Path.Combine(TemporaryDirectory, "AcToolsShot_" + CarId + "_" + DateTime.Now.Ticks);
                Directory.CreateDirectory(OutputDirectory);
            }

            public IEnumerable<string> CarSkins {
                get {
                    var unfiltered = Directory.GetDirectories(FileUtils.GetCarSkinsDirectory(AcRoot, CarId)).Select(Path.GetFileName);
                    if (SkinIds == null) return unfiltered;

                    var skinIdsLower = SkinIds.Select(x => x.ToLowerInvariant()).ToList();
                    unfiltered = unfiltered.Where(x => skinIdsLower.Contains(Path.GetFileName(x)?.ToLowerInvariant()));
                    return unfiltered;
                }
            } 

            public abstract void ShotAll();

            public virtual void Dispose() {
                _changes.DisposeEverything();
            }
        }

        public abstract class BaseIterableShooter : BaseShotter {
            public abstract void Shot(string skinId);

            public override void ShotAll() {
                Prepare();
                foreach (var skinId in CarSkins) {
                    Shot(skinId);
                }
            }
        } 
    }
}
