using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.ContentRepair {
    public class ObsoletableAspect : Displayable {
        private readonly Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> _fix;

        public sealed override string DisplayName { get; set; }

        public string Description { get; }
        
        public bool AffectsData { get; set; }

        public string FixCaption { get; set; }

        public ObsoletableAspect(string name, string description, Func<IProgress<AsyncProgressEntry>, CancellationToken, Task<bool>> fix) {
            _fix = fix;
            DisplayName = name;
            Description = description;
        }

        public Task<bool> Fix(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            return _fix(progress, cancellation);
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

        private AsyncCommand _fixCommand;

        public AsyncCommand FixCommand => _fixCommand ?? (_fixCommand = new AsyncCommand(async () => {
            try {
                using (var waiting = new WaitingDialog()) {
                    waiting.Report(AsyncProgressEntry.Indetermitate);
                    IsSolved = await Fix(waiting, waiting.CancellationToken);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t fix the issue", e);
            }
        }));

        private DelegateCommand _hideCommand;

        public DelegateCommand HideCommand => _hideCommand ?? (_hideCommand = new DelegateCommand(() => {
            IsHidden = true;
        }));
    }
}