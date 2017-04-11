using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.ContentRepair {
    public class ContentObsoleteSuggestion : ContentRepairSuggestion {
        public ContentObsoleteSuggestion(string name, string description, Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> fix) :
                base("Obsolete", name, description, fix) {}
    }

    public class CommonErrorSuggestion : ContentRepairSuggestion {
        public CommonErrorSuggestion(string name, string description, Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> fix) :
                base("Common Error", name, description, fix) {}
    }

    public class ContentRepairSuggestionFix {
        public ContentRepairSuggestion Parent { get; }

        public string FixCaption { get; set; }

        public bool AffectsData { get; set; }

        public bool ShowProgressDialog { get; set; } = true;

        private readonly Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> _fix;

        public ContentRepairSuggestionFix(ContentRepairSuggestion parent, string fixCaption,
                Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> fix) {
            Parent = parent;
            FixCaption = fixCaption;
            _fix = fix;
        }

        public Task<bool> Fix(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            return _fix(progress, cancellation);
        }

        private AsyncCommand _fixCommand;

        public AsyncCommand FixCommand => _fixCommand ?? (_fixCommand = new AsyncCommand(async () => {
            try {
                if (ShowProgressDialog) {
                    using (var waiting = new WaitingDialog()) {
                        waiting.Report(AsyncProgressEntry.Indetermitate);
                        Parent.IsSolved = await Fix(waiting, waiting.CancellationToken);
                    }
                } else {
                    Parent.IsSolved = await Fix();
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t fix the issue", e);
            }
        }));
    }

    public class ContentRepairSuggestion : Displayable {
        public string Category { get; }

        public sealed override string DisplayName { get; set; }

        public string Description { get; }

        public List<ContentRepairSuggestionFix> Fixes { get; }

        public string FixCaption {
            get { return Fixes[0].FixCaption; }
            set { Fixes[0].FixCaption = value; }
        }

        public bool AffectsData {
            get { return Fixes[0].AffectsData; }
            set { Fixes[0].AffectsData = value; }
        }

        public bool ShowProgressDialog {
            get { return Fixes[0].ShowProgressDialog; }
            set { Fixes[0].ShowProgressDialog = value; }
        }

        public ContentRepairSuggestion(string category, string name, string description, Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> fix) {
            Fixes = new List<ContentRepairSuggestionFix> {
                new ContentRepairSuggestionFix(this, null, fix)
            };
            
            Category = category;
            DisplayName = name;
            Description = description;
        }

        public ContentRepairSuggestion AlternateFix(string name, Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> fix, bool affectsData = true,
                bool showProgressDialog = true) {
            Fixes.Add(new ContentRepairSuggestionFix(this, name, fix) {
                AffectsData = affectsData,
                ShowProgressDialog = showProgressDialog
            });
            return this;
        }

        private bool _isHidden;

        public bool IsHidden {
            get { return _isHidden; }
            set {
                if (Equals(value, _isHidden)) return;
                _isHidden = value;
                OnPropertyChanged();
            }
        }

        private bool _isSolved;

        public bool IsSolved {
            get { return _isSolved; }
            set {
                if (Equals(value, _isSolved)) return;
                _isSolved = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _hideCommand;

        public DelegateCommand HideCommand => _hideCommand ?? (_hideCommand = new DelegateCommand(() => {
            IsHidden = true;
        }));
    }
}