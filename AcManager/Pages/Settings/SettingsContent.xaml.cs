using System;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Internal;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using CG.Web.MegaApiClient;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Pages.Settings {
    public partial class SettingsContent {
        public SettingsContent() {
            InitializeComponent();
            DataContext = new ViewModel();
            this.AddWidthCondition(1080).Add(v => Grid.Columns = v ? 2 : 1);
        }

        public class ViewModel : NotifyPropertyChanged {
            public SettingsHolder.ContentSettings Holder => SettingsHolder.Content;

            internal ViewModel() {
                /*AuthenticationStorage.CopyExistingTo(new[] {
                    KeyUserEmail, KeySession, KeyToken,
                }, new[] {
                    "MegaUploader:"
                }, _megaStorage);*/

                MegaUserEmail = MegaStorage.GetEncrypted<string>(KeyUserEmail);
                MegaReady = !string.IsNullOrWhiteSpace(MegaUserEmail)
                        && MegaStorage.GetEncrypted<string>(KeySession) != null && MegaStorage.GetEncrypted<string>(KeyToken) != null;
            }

            private DelegateCommand _resetCupRegistriesCommand;

            public DelegateCommand ResetCupRegistriesCommand => _resetCupRegistriesCommand ?? (_resetCupRegistriesCommand = new DelegateCommand(() => {
                Holder.CupRegistries = "https://acstuff.ru/cup/";
            }));

            public string DefaultTemporaryFilesLocation { get; } = Path.GetTempPath();

            private ICommand _changeTemporaryFilesLocationCommand;

            public ICommand ChangeTemporaryFilesLocationCommand
                => _changeTemporaryFilesLocationCommand ?? (_changeTemporaryFilesLocationCommand = new DelegateCommand(() => {
                    var dialog = new FolderBrowserDialog {
                        ShowNewFolderButton = true,
                        SelectedPath = SettingsHolder.Content.TemporaryFilesLocation
                    };

                    if (dialog.ShowDialog() == DialogResult.OK) {
                        SettingsHolder.Content.TemporaryFilesLocation = dialog.SelectedPath;
                    }
                }));

            #region Mega.nz credentials
            private IStorage MegaStorage => Holder.MegaAuthenticationStorage;

            private const string KeyUserEmail = "email";
            private const string KeySession = "session";
            private const string KeyToken = "token";

            private string _megaUserEmail;

            public string MegaUserEmail {
                get => _megaUserEmail;
                set {
                    if (Equals(value, _megaUserEmail)) return;
                    _megaUserEmail = value;
                    OnPropertyChanged();
                    MegaStorage.SetEncrypted(KeyUserEmail, value);
                    _megaLogInCommand?.RaiseCanExecuteChanged();
                }
            }

            private bool _megaReady;

            public bool MegaReady {
                get => _megaReady;
                set => Apply(value, ref _megaReady, () => {
                    _megaLogOutCommand?.RaiseCanExecuteChanged();
                });
            }

            private string _megaUserPassword;

            public string MegaUserPassword {
                get => _megaUserPassword;
                set => Apply(value, ref _megaUserPassword, () => {
                    _megaLogInCommand?.RaiseCanExecuteChanged();
                });
            }

            private AsyncCommand _megaLogInCommand;

            public AsyncCommand MegaLogInCommand => _megaLogInCommand ?? (_megaLogInCommand = new AsyncCommand(async () => {
                try {
                    var client = new MegaApiClient(new Options(InternalUtils.GetMegaAppKey().Item1));
                    var token = await client.LoginAsync(MegaApiClient.GenerateAuthInfos(MegaUserEmail, MegaUserPassword));
                    MegaStorage.SetEncrypted(KeySession, token.SessionId);
                    MegaStorage.SetEncrypted(KeyToken, token.MasterKey);
                    MegaReady = true;
                } catch (Exception e) when (e.IsCancelled()) {
                } catch (WebException e) {
                    NonfatalError.Notify("Can’t sign in", ToolsStrings.Common_MakeSureInternetWorks, e);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t sign in", e);
                }
            }, () => !string.IsNullOrWhiteSpace(MegaUserEmail) && !string.IsNullOrWhiteSpace(MegaUserPassword)));

            private DelegateCommand _megaLogOutCommand;

            public DelegateCommand MegaLogOutCommand => _megaLogOutCommand ?? (_megaLogOutCommand = new DelegateCommand(() => {
                MegaUserEmail = null;
                MegaUserPassword = null;
                MegaStorage.Remove(KeySession);
                MegaStorage.Remove(KeyToken);
                MegaReady = false;
            }, () => MegaReady));
            #endregion
        }
    }
}