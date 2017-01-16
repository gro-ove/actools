using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private static string[] _kunosCarsIds;

        private static bool TestIfKunosUsingGuids(string id) {
            if (_kunosCarsIds == null) {
                try {
                    _kunosCarsIds = File.ReadAllLines(FileUtils.GetSfxGuidsFilename(AcRootDirectory.Instance.Value))
                                       .Select(x => x.Split('/'))
                                       .Where(x => x.Length > 3 && x[1] == "cars" && x[0].EndsWith("event:"))
                                       .Select(x => x[2].ToLowerInvariant())
                                       .Distinct()
                                       .ToArray();
                } catch (Exception e) {
                    Logging.Warning("Can’t get IDs from GUIDs.txt: " + e);
                    _kunosCarsIds = new string[] {};
                }
            }

            return _kunosCarsIds.Contains(id);
        }

        private KunosDlcInformation _dlc;
        private bool _dlcSet;

        [CanBeNull]
        public KunosDlcInformation Dlc {
            get {
                if (!_dlcSet) {
                    _dlcSet = true;

                    if (Author == AuthorKunos) {
                        var dlcs = DataProvider.Instance.DlcInformations;
                        for (var i = dlcs.Length - 1; i >= 0; i--) {
                            var dlc = dlcs[i];
                            if (dlc.Cars.Contains(Id)) {
                                _dlc = dlc;
                                break;
                            }
                        }
                    }
                }

                return _dlc;
            }
        }

        public override string VersionInfoDisplay {
            get {
                var dlc = Dlc;
                return dlc != null ? $@"{AuthorKunos} ([url=""http://store.steampowered.com/app/{dlc.Id}/""]{dlc.ShortName}[/url])" :
                        base.VersionInfoDisplay;
            }
        }
    }
}
