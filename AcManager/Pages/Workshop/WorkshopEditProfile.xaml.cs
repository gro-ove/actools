using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Helpers;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopEditProfile {
        private ViewModel Model => (ViewModel)DataContext;

        public WorkshopEditProfile([NotNull] WorkshopClient workshopClient, [NotNull] UserInfo user) {
            DataContext = new ViewModel(workshopClient, user);
            InitializeComponent();
            Buttons = new[] {
                new AsyncButton {
                    Content = UiStrings.Ok,
                    Command = Model.ApplyCommand,
                    Width = 200d
                },
                CancelButton
            };
            Model.PropertyChanged += OnModelPropertyChanged;
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Model.Finished)) {
                DialogResult = true;
                Close();
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            [NotNull]
            private readonly WorkshopClient _workshopClient;

            [NotNull]
            private readonly UserInfo _user;

            public ViewModel([NotNull] WorkshopClient workshopClient, [NotNull] UserInfo user) {
                _workshopClient = workshopClient;
                _user = user;
                Username = _user.Username;
                Name = _user.Name;
                Location = _user.Location;
                Bio = _user.Bio;
                AvatarImageSource = _user.AvatarLarge;
            }

            private string _username;

            public string Username {
                get => _username;
                set => Apply(value, ref _username);
            }

            private string _name;

            public string Name {
                get => _name;
                set => Apply(value, ref _name);
            }

            private string _location;

            public string Location {
                get => _location;
                set => Apply(value, ref _location);
            }

            private string _bio;

            public string Bio {
                get => _bio;
                set => Apply(value, ref _bio);
            }

            private object _avatarImageSource;

            public object AvatarImageSource {
                get => _avatarImageSource;
                set => Apply(value, ref _avatarImageSource);
            }

            private byte[] _newAvatarLarge;
            private byte[] _newAvatarSmall;

            public void ApplyNewAvatar(string filename) {
                try {
                    var source = ImageEditor.Proceed(filename, new Size(184, 184));
                    _newAvatarLarge = source?.ToBytes(ImageFormat.Jpeg);
                    _newAvatarSmall = source?.Resize(48, 48).ToBytes(ImageFormat.Jpeg);
                    AvatarImageSource = _newAvatarLarge;
                    _revertAvatarCommand?.RaiseCanExecuteChanged();
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t crop avatar", e);
                }
            }

            private DelegateCommand _changeAvatarCommand;

            public DelegateCommand ChangeAvatarCommand => _changeAvatarCommand ?? (_changeAvatarCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = FileDialogFilters.ImagesFilter,
                    Title = "Select image for avatar",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() == true) {
                    ApplyNewAvatar(dialog.FileName);
                }
            }));

            private DelegateCommand _revertAvatarCommand;

            public DelegateCommand RevertAvatarCommand => _revertAvatarCommand ?? (_revertAvatarCommand = new DelegateCommand(() => {
                _newAvatarLarge = null;
                _newAvatarSmall = null;
                AvatarImageSource = _user.AvatarLarge;
                _revertAvatarCommand?.RaiseCanExecuteChanged();
            }, () => _newAvatarLarge != null));

            private AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> _applyCommand;

            public AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> ApplyCommand
                => _applyCommand ?? (_applyCommand = new AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>>(async c => {
                    try {
                        var newInfo = new JObject {
                            [@"username"] = Username,
                            [@"name"] = Name,
                            [@"bio"] = Bio,
                            [@"location"] = Location
                        };

                        if (_newAvatarLarge != null && _newAvatarSmall != null) {
                            var group = _workshopClient.StartNewUploadGroup();
                            c?.Item1?.Report(new AsyncProgressEntry("Small avatar…", 0.2));
                            newInfo[@"avatarImageSmall"] = await group.UploadAsync(_newAvatarSmall, $@"{_user.Username}_small.jpg");
                            c?.Item1?.Report(new AsyncProgressEntry("Large avatar…", 0.4));
                            newInfo[@"avatarImageLarge"] = await group.UploadAsync(_newAvatarLarge, $@"{_user.Username}_large.jpg");
                        }

                        c?.Item1?.Report(new AsyncProgressEntry("New information…", 0.6));
                        await _workshopClient.PatchAsync($"/users/{_user.UserId}", newInfo, (c?.Item2).Straighten());
                        Finished = true;
                    } catch (Exception e) when (e.IsCancelled()) { }
                }));

            private bool _finished;

            public bool Finished {
                get => _finished;
                set => Apply(value, ref _finished);
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            Focus();
            var inputFile = e.GetInputFiles().FirstOrDefault(x => Regex.IsMatch(x, @"\.(jpe?g|png|bmp|gif)$", RegexOptions.IgnoreCase));
            if (inputFile != null) {
                e.Handled = true;
                Model.ApplyNewAvatar(inputFile);
            }
        }
    }
}