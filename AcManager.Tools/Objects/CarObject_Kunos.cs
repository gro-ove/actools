using System;
using System.IO;
using System.Linq;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;

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
    }
}
