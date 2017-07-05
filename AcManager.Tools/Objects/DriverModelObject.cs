using System;
using System.Collections;
using System.IO;
using System.Linq;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class DriverModelObject : AcCommonSingleFileObject, IAcObjectAuthorInformation {
        public const string FileExtension = ".kn5";

        public override string Extension => FileExtension;

        public DriverModelObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            AcId = id.ApartFromLast(FileExtension);

            _usingsCarsIds = ValuesStorage.GetStringList(KeyUsingsCarsIds).ToArray();
            IsUsed = _usingsCarsIds.Any();
        }

        public override bool HasData => true;

        private string KeyUsingsCarsIds => @"__tmp_DriverModelObject.UsingsCarsIds_" + Id;

        [NotNull]
        private string[] _usingsCarsIds;

        [NotNull]
        public string[] UsingsCarsIds {
            get => _usingsCarsIds;
            set {
                if (Equals(value, _usingsCarsIds)) return;
                _usingsCarsIds = value;
                OnPropertyChanged();

                IsUsed = value.Any();
                ValuesStorage.Set(KeyUsingsCarsIds, value);
            }
        }

        private bool _isUsed;

        public bool IsUsed {
            get => _isUsed;
            set {
                if (Equals(value, _isUsed)) return;
                _isUsed = value;
                OnPropertyChanged();

                ErrorIf(IsUsed && !Enabled, AcErrorType.Font_UsedButDisabled);
            }
        }

        public string AcId { get; }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();

            try {
                Author = (DataProvider.Instance.KunosContent[@"drivers"]?.Contains(Id) ?? false) ? AuthorKunos : null;
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        public string Author { get; private set; }

        #region Packing
        private class DriverModelPacker : AcCommonObjectPacker<DriverModelObject> {
            protected override string GetBasePath(DriverModelObject t) {
                return "content/driver";
            }

            protected override IEnumerable PackOverride(DriverModelObject t) {
                yield return AddFilename(Path.GetFileName(t.Location), t.Location);
            }

            protected override PackedDescription GetDescriptionOverride(DriverModelObject t) {
                return new PackedDescription(t.Id, t.Name, null, DriverModelsManager.Instance.Directories.GetMainDirectory(), true);
            }
        }

        protected override AcCommonObjectPacker CreatePacker() {
            return new DriverModelPacker();
        }
        #endregion
    }
}