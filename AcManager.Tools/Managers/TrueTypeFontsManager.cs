using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers {
    public class TrueTypeFontsManager : AcManagerFileSpecific<TrueTypeFontObject> {
        private static TrueTypeFontsManager _instance;

        public static TrueTypeFontsManager Instance => _instance ?? (_instance = new TrueTypeFontsManager());

        [CanBeNull]
        public TrueTypeFontObject GetByAcId(string v) {
            return GetById(v + TrueTypeFontObject.FileExtension);
        }

        public string DefaultFilename => Directories.GetLocation("consola.ttf", true);

        public override string SearchPattern => @"*.ttf";

        protected override string CheckIfIdValid(string id) {
            if (!id.EndsWith(TrueTypeFontObject.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return $"ID should end with “{TrueTypeFontObject.FileExtension}”.";
            }

            return base.CheckIfIdValid(id);
        }

        public override IAcDirectories Directories => AcRootDirectory.Instance.FontsDirectories;

        protected override TrueTypeFontObject CreateAcObject(string id, bool enabled) {
            return new TrueTypeFontObject(this, id, enabled);
        }
    }
}