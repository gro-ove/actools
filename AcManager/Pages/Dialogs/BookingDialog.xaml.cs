using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class BookingDialog : IBookingUi, IInvokingNotifyPropertyChanged {
        public ServerEntry ServerEntry { get; private set; }

        private CarObject _car;

        [CanBeNull]
        public CarObject Car {
            get => _car;
            set => this.Apply(value, ref _car);
        }

        private bool _ignoreSkinChange;
        private CarSkinObject _skin;

        [CanBeNull]
        public CarSkinObject Skin {
            get => _skin;
            set {
                if (Equals(value, _skin)) return;
                _skin = value;
                OnPropertyChanged();

                if (!_ignoreSkinChange) {
                    var carEntry = ServerEntry.SelectedCarEntry;
                    if (carEntry == null) return;

                    carEntry.SetAvailableSkinId(value?.Id, null);
                    ServerEntry.JoinCommand.ExecuteAsync(null).Ignore();
                }
            }
        }

        private TrackObjectBase _track;

        [CanBeNull]
        public TrackObjectBase Track {
            get => _track;
            set => this.Apply(value, ref _track);
        }

        private readonly CancellationTokenSource _cancellationSource;

        public BookingDialog() {
            _cancellationSource = new CancellationTokenSource();
            CancellationToken = _cancellationSource.Token;
        }

        protected override void OnClosingOverride(CancelEventArgs e) {
            if (IsResultCancel) {
                try {
                    _cancellationSource.Cancel();
                } catch (ObjectDisposedException) { }
                ServerEntry?.CancelBookingCommand.Execute(null);
            }

            base.OnClosingOverride(e);
        }

        public void Dispose() {
            if (IsVisible) {
                Close();
            }

            try {
                _cancellationSource.Cancel();
            } catch (ObjectDisposedException) { }
        }

        private DispatcherTimer _timer;

        public void Show(ServerEntry server) {
            ServerEntry = server;

            if (!ReferenceEquals(DataContext, this)) {
                DataContext = this;
                InitializeComponent();
                Show();

                Owner = Application.Current?.MainWindow;

                _timer = new DispatcherTimer {
                    Interval = TimeSpan.FromSeconds(1),
                    IsEnabled = true
                };
                _timer.Tick += OnTick;
            }

            Car = server.SelectedCarEntry?.CarObject;
            Track = server.Track;

            try {
                _ignoreSkinChange = true;
                Skin = server.GetSelectedCarSkin();
            } finally {
                _ignoreSkinChange = false;
            }

            Buttons = new[] {
                CreateExtraStyledDialogButton("Go.Button", AppStrings.Common_Go,
                        () => ServerEntry?.JoinCommand.ExecuteAsync(ServerEntry.ActualJoin), () => Ready),
                CancelButton
            };
        }

        private bool _ready;

        public bool Ready {
            get => _ready;
            set {
                if (Equals(value, _ready)) return;
                _ready = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();

                if (value) {
                    Toast.Show(AppStrings.Dialogs_BookingDialog_BookingIsFinished, AppStrings.Srs_ReadyNotification,
                            () => ServerEntry?.JoinCommand.ExecuteAsync(ServerEntry.ActualJoin));
                }
            }
        }

        private void OnTick(object sender, EventArgs e) {
            ServerEntry?.OnTick();
            Ready = ServerEntry?.BookingTimeLeft == TimeSpan.Zero && ServerEntry.BookingErrorMessage == null;
        }

        public void OnUpdate(BookingResult response) {
            if (response?.IsSuccessful != true) return;

            Car = ServerEntry.SelectedCarEntry?.CarObject;
            Track = ServerEntry.Track;

            try {
                _ignoreSkinChange = true;
                Skin = ServerEntry.GetSelectedCarSkin();
            } finally {
                _ignoreSkinChange = false;
            }
        }

        public CancellationToken CancellationToken { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        void IInvokingNotifyPropertyChanged.OnPropertyChanged(string propertyName) {
            OnPropertyChanged(propertyName);
        }
    }
}