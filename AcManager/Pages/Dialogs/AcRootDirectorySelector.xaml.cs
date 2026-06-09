using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Starters;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class AcRootDirectorySelector {
        public static bool OptionUseCustomSteamIdApproach = false;

        private const string WizardVersion = "2";
        private const string KeyWizardVersion = "_wizardVersion";

        public static bool IsReviewNeeded() {
            return ValuesStorage.Get<string>(KeyWizardVersion) != WizardVersion;
        }

        public static void JustReviewed() {
            ValuesStorage.Set(KeyWizardVersion, WizardVersion);
        }

        private ViewModel Model => (ViewModel)DataContext;

        public AcRootDirectorySelector() : this(true, true) { }

        public AcRootDirectorySelector(bool changeAcRoot, bool changeSteamId) {
            InitializeComponent();
            DataContext = new ViewModel(changeAcRoot, changeSteamId && !SteamStarter.IsInitialized);

            if (AppArguments.Values.Any()) {
                ProcessArguments(AppArguments.Values);
            }

            Buttons = new[] {
                CreateExtraDialogButton(UiStrings.Ok,
                        new CombinedCommand(Model.ApplyCommand, new DelegateCommand(() => CloseWithResult(MessageBoxResult.OK)))),
                CancelButton
            };

            EntryPoint.HandleSecondInstanceMessages(this, ProcessArguments);
            AppUi.AppUiMessageInterception = ProcessArguments;
            PluginsManager.Instance.UpdateIfObsolete().Ignore();
        }

        protected override void OnClosingOverride(CancelEventArgs e) {
            AppUi.AppUiMessageInterception = null;
            base.OnClosingOverride(e);
        }

        private bool ProcessArguments(IEnumerable<string> arguments) {
            foreach (var message in arguments) {
                var request = CustomUriRequest.TryParse(message);
                Logging.Debug($"Request: {request?.Path ?? @"<no path>"}");
                if (request == null) continue;

                switch (request.Path) {
                    case "setsteamid":
                        Logging.Debug("Packed code: " + request.Params.Get(@"code"));
                        Model.SetPacked(request.Params.Get(@"code"));
                        return true;
                }
            }

            return false;
        }

        public class ViewModel : NotifyPropertyChanged, INotifyDataErrorInfo {
            private static readonly string AdditionalSalt = (DateTime.Now - default(DateTime)).Milliseconds.ToInvariantString();

            public bool FirstRun { get; }

            public bool ReviewMode { get; }

            public bool ChangeAcRoot { get; }

            public bool ChangeSteamId { get; }

            public bool SettingsRun => !FirstRun && !ReviewMode;

            private const string KeyFirstRun = "_second_run";

            public ViewModel(bool changeAcRoot, bool changeSteamId) {
                ChangeAcRoot = changeAcRoot;
                ChangeSteamId = changeSteamId;

                FirstRun = ValuesStorage.Get(KeyFirstRun, true);
                if (FirstRun) {
                    ValuesStorage.Set(KeyFirstRun, false);
                }

                ReviewMode = !FirstRun && IsReviewNeeded();
                Value = AcRootDirectory.Instance.IsReady ? AcRootDirectory.Instance.Value : AcRootDirectory.TryToFind();

#if DEBUG
                if (changeAcRoot) {
                    // Value = Value?.Replace("D:", "C:");
                }
#endif

                var steamId = SteamIdHelper.Instance.Value;
                SteamProfiles = new BetterObservableCollection<SteamProfile>(SteamIdHelper.TryToFind().Append(SteamProfile.None));
                SteamProfile = SteamProfiles.GetByIdOrDefault(steamId) ?? SteamProfiles.First();

                if (steamId != null && SteamProfile.SteamId != steamId) {
                    SetSteamId(steamId);
                }
            }

            private bool _isValueAcceptable;
            private string _previousInacceptanceReason;

            public bool IsValueAcceptable {
                get => _isValueAcceptable;
                set {
                    if (value == _isValueAcceptable) return;
                    _isValueAcceptable = value;
                    OnPropertyChanged();
                    OnErrorsChanged(nameof(Value));
                    CommandManager.InvalidateRequerySuggested();
                }
            }

            private string _value;

            public string Value {
                get => _value;
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();

                    IsValueAcceptable = AcRootDirectory.CheckDirectory(_value, true, out _previousInacceptanceReason);
                    OnErrorsChanged();
                    _getSteamIdCommand?.RaiseCanExecuteChanged();
                }
            }

            private SteamProfile _steamProfile = SteamProfile.None;

            [NotNull]
            public SteamProfile SteamProfile {
                get => _steamProfile;
                set => Apply(value, ref _steamProfile);
            }

            public BetterObservableCollection<SteamProfile> SteamProfiles { get; }

            public void SetPacked(string packed) {
                SetSteamId(InternalUtils.GetPackedSteamId(packed, AdditionalSalt));
            }

            private async void SetSteamId(string steamId) {
                if (steamId == null) return;

                _cancellationTokenSource?.Cancel();

                var existing = SteamProfiles.FirstOrDefault(x => x.SteamId == steamId);
                if (existing != null) {
                    SteamProfile = existing;
                    return;
                }

                var profile = new SteamProfile(steamId);
                SteamProfiles.Add(profile);
                SteamProfile = profile;

                profile.ProfileName = await SteamIdHelper.GetSteamNameAsync(steamId);
            }

            private CancellationTokenSource _cancellationTokenSource;

            private DelegateCommand _changeAcRootCommand;

            public ICommand ChangeAcRootCommand => _changeAcRootCommand ?? (_changeAcRootCommand = new DelegateCommand(() => {
                var dialog = new FolderBrowserDialog {
                    ShowNewFolderButton = false,
                    SelectedPath = Value
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    Value = dialog.SelectedPath;
                }
            }));

            private AsyncCommand<CancellationToken?> _getSteamIdCommand;

            public AsyncCommand<CancellationToken?> GetSteamIdCommand
                => _getSteamIdCommand ?? (_getSteamIdCommand = new AsyncCommand<CancellationToken?>(async t => {
                    var acRoot = IsValueAcceptable ? Value : AcRootDirectory.Instance.Value;
                    if (acRoot == null) return;

                    if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift)) {
                        var id = await Prompt.ShowAsync("Enter new Steam ID:", "Change Steam ID", SteamIdHelper.Instance.Value);
                        if (id != null) {
                            SetSteamId(id);
                        }
                        return;
                    }

                    if (OptionUseCustomSteamIdApproach && ShowMessage(
                            "Your Steam ID is not in the list? In that case, Content Manager would need to replace official launcher. There are other benefits to this as well: races will launch faster and more reliably, Content Manager will be able to check challenges progress more directly, it would get Steam overlay. Or, you could always get the original launcher back by simply renaming “AssettoCorsa_original.exe” back to “AssettoCorsa.exe”.[br][br]So, replace it now?",
                            "Fix Steam ID", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                        try {
                            var acLauncher = AcPaths.GetAcLauncherFilename(acRoot);
                            if (File.Exists(acLauncher)) {
                                var acLauncherNewPlace = Path.Combine(acRoot, "AssettoCorsa_original.exe");
                                if (!File.Exists(acLauncherNewPlace)) {
                                    File.Move(acLauncher, acLauncherNewPlace);
                                } else {
                                    FileUtils.Recycle(acLauncher);
                                }
                            }
                            File.Copy(MainExecutingFile.Location, acLauncher, true);
                            ProcessExtension.Start(acLauncher, new[] { @"--restart", @"--move-app=" + MainExecutingFile.Location });
                            Environment.Exit(0);
                        } catch (Exception e) {
                            NonfatalError.Notify("Failed to move Content Manager executable", "I’m afraid you’ll have to do it manually.", e);
                        }
                        return;
                    }

                    using (_cancellationTokenSource = new CancellationTokenSource())
                    using (var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(t ?? default(CancellationToken), _cancellationTokenSource.Token)) {
                        try {
                            Logging.Debug("SteamID: OAuth route");
                            var packed = await OAuth.GetCode("Steam", $"{InternalUtils.MainApiDomain}/u/steam?s={AdditionalSalt}", null,
                                    @"CM Steam ID Helper: (\w+)", description: "Enter the authentication code:", title: "Steam (via acstuff.club)",
                                    serverless: true, cancellation: combinedToken.Token);
                            Logging.Debug($"SteamID: OAuth route result={packed.Code ?? @"<nil>"}");
                            if (!combinedToken.IsCancellationRequested && packed.Code != null) {
                                SetPacked(packed.Code);
                            }
                        } catch (Exception e) when (e.IsCancelled()) { } catch (Exception e) {
                            NonfatalError.Notify("Can’t get Steam ID", e);
                        }
                    }
                    _cancellationTokenSource = null;
                }, mainCancellationToken => IsValueAcceptable ? Value != null : AcRootDirectory.Instance.Value != null));

            private DelegateCommand _applyCommand;

            public ICommand ApplyCommand => _applyCommand ?? (_applyCommand = new DelegateCommand(() => {
                Logging.Write($"[Initial setup] AC root=“{Value}”, Steam ID=“{SteamProfile.SteamId}”");
                AcRootDirectory.Instance.RawValue = Value?.Trim();
                SteamIdHelper.Instance.Value = SteamProfile.SteamId;
                JustReviewed();
                _getSteamIdCommand?.RaiseCanExecuteChanged();
            }, () => IsValueAcceptable));

            public IEnumerable GetErrors(string propertyName) {
                switch (propertyName) {
                    case nameof(Value):
                        return string.IsNullOrWhiteSpace(Value) ? new[] { AppStrings.Common_RequiredValue } :
                                IsValueAcceptable ? null : new[] { _previousInacceptanceReason ?? AppStrings.AcRoot_FolderIsUnacceptable };
                    default:
                        return null;
                }
            }

            public bool HasErrors => string.IsNullOrWhiteSpace(Value) || !IsValueAcceptable;
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }
    }
}