using System;
using System.Diagnostics;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Helpers;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject : AcObjectNew {
        public const string AuthorKunos = "Kunos";

        public readonly IFileAcManager FileAcManager;

        public virtual bool NeedsMargin => false;

        protected AcCommonObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            FileAcManager = manager;
        }

        public override void Reload() {
            ClearErrors();
            LoadOrThrow();
            Changed = false;
        }

        protected bool LoadingInProcess { get; private set; }

        protected abstract void LoadOrThrow();

        public sealed override void Load() {
            ClearErrors();
            Changed = false;
            LoadingInProcess = true;

            try {
                LoadOrThrow();
            } catch (AcErrorException e) {
                AddError(e.AcError);
#if !DEBUG
            } catch (Exception e) {
                AddError(AcErrorType.Load_Base, e);
#endif
            } finally {
                LoadingInProcess = false;
            }
        }

        public void SortAffectingValueChanged() {
            if (!LoadingInProcess) {
                Manager.UpdateList();
            }
        }

        public abstract bool HasData { get; }

        public override string Name {
            get { return base.Name; }
            protected set {
                if (Equals(value, base.Name)) return;
                base.Name = value;

                if (value == null && HasData) {
                    AddError(AcErrorType.Data_ObjectNameIsMissing);
                } else {
                    RemoveError(AcErrorType.Data_ObjectNameIsMissing);
                }

                SortAffectingValueChanged();

                OnPropertyChanged(nameof(NameEditable));
                Changed = true;
            }
        }
        
        public virtual string NameEditable {
            get { return Name ?? Id; }
            set { Name = value; }
        }

        private bool _changed;

        public bool Changed {
            get { return _changed; }
            protected set {
                if (value == _changed || LoadingInProcess) return;
                _changed = value;
                OnPropertyChanged(nameof(Changed));
            }
        }

        private int? _year;

        public int? Year {
            get { return _year; }
            set {
                if (value == 0) {
                    value = null;
                }

                if (value == _year) return;
                _year = value;
                OnPropertyChanged(nameof(Year));

                if (_year.HasValue && Name != null) {
                    var inName = AcStringValues.GetYearFromName(Name);
                    if (inName.HasValue && inName.Value != _year.Value) {
                        Name = AcStringValues.NameReplaceYear(Name, _year.Value);
                    }
                }

                Changed = true;
            }
        }

        public abstract void Save();

        public virtual string Location => FileAcManager.Directories.GetLocation(Id, Enabled);

        public virtual void ViewInExplorer() {
            if (File.GetAttributes(Location).HasFlag(FileAttributes.Directory)) {
                Process.Start("explorer", Location);
            } else {
                Process.Start("explorer", "/select," + Location);
            }
        }

        protected virtual void Toggle() {
            FileAcManager.Toggle(Id);
        }

        public virtual void Delete() {
            FileAcManager.Delete(Id);
        }

        public virtual bool HandleChangedFile(string filename) => false;

        protected string ImageRefreshing { get; private set; }

        protected void OnImageChanged(string propertyName) {
            ImageRefreshing = string.Empty;
            OnPropertyChanged(propertyName);
            ImageRefreshing = null;
            OnPropertyChanged(propertyName);
        }
    }
}
