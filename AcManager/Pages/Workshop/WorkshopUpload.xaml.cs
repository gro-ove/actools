using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopUpload {
        public static bool OptionAvailable = false;

        private ViewModel Model => (ViewModel)DataContext;

        public WorkshopUpload(IEnumerable<AcObjectNew> objects) {
            DataContext = new ViewModel(objects);
            InitializeComponent();
        }

        public WorkshopUpload(params AcObjectNew[] objects) : this((IEnumerable<AcObjectNew>)objects) { }

        public class PlannedAction : NotifyPropertyChanged {
            public string IconFilename { get; }
            public string DisplayType { get; }
            public AcObjectNew ObjectToUpload { get; }
            public List<PlannedAction> Children { get; }

            private bool _active = true;

            public bool Active {
                get => _active;
                set => Apply(value, ref _active);
            }

            public PlannedAction(AcObjectNew objectToUpload) {
                ObjectToUpload = objectToUpload;
                switch (objectToUpload) {
                    case CarObject car:
                        DisplayType = "car";
                        IconFilename = car.BrandBadge;
                        Children = car.EnabledOnlySkins.Select(x => new PlannedAction(x)).ToList();
                        break;
                    case CarSkinObject carSkin:
                        DisplayType = "car skin";
                        IconFilename = carSkin.LiveryImage;
                        break;
                    default:
                        DisplayType = "unsupported";
                        break;
                }
            }
        }

        public enum UploadPhase {
            [Description("Authorization")]
            Authorization = 1,

            [Description("Select items to upload")]
            Preparation = 2,

            [Description("Uploading…")]
            Upload = 4,

            [Description("Upload complete")]
            Finalization = 5
        }

        public class ViewModel : NotifyPropertyChanged {
            public List<PlannedAction> PlannedActions { get; }

            private UploadPhase _phase;

            public UploadPhase Phase {
                get => _phase;
                set => Apply(value, ref _phase);
            }

            private const string KeyUserName = "w/nu";
            private const string KeyUserPassword = "w/pu";

            public ViewModel(IEnumerable<AcObjectNew> objects) {
                Phase = UploadPhase.Authorization;
                PlannedActions = objects.Select(x => new PlannedAction(x)).ToList();
                UserName = ValuesStorage.GetEncrypted<string>(KeyUserName);
                UserPassword = ValuesStorage.GetEncrypted<string>(KeyUserPassword);
                if (UserName != null && UserPassword != null) {
                    SilentLogIn = true;
                    LogInCommand.ExecuteAsync(null);
                }
            }

            // Common values
            private WorkshopClient _client;

            // Phase 1: authorization
            private bool _silentLogIn;

            public bool SilentLogIn {
                get => _silentLogIn;
                set => Apply(value, ref _silentLogIn);
            }

            private string _userName;

            public string UserName {
                get => _userName;
                set => Apply(value, ref _userName);
            }

            private string _userPassword;

            public string UserPassword {
                get => _userPassword;
                set => Apply(value, ref _userPassword);
            }

            private string _currentError;

            public string CurrentError {
                get => _currentError;
                set => Apply(value, ref _currentError);
            }

            private UserInfo _loggedInAs;

            public UserInfo LoggedInAs {
                get => _loggedInAs;
                set => Apply(value, ref _loggedInAs);
            }

            private AsyncCommand<CancellationToken?> _logInCommand;

            public AsyncCommand<CancellationToken?> LogInCommand
                => _logInCommand ?? (_logInCommand = new AsyncCommand<CancellationToken?>(async c => {
                    _client = new WorkshopClient("http://192.168.1.10:3000", UserName, UserPassword);
                    try {
                        LoggedInAs = await _client.GetAsync<UserInfo>("/manage/user-info", c.Straighten());
                        Phase = UploadPhase.Preparation;
                        CurrentError = null;
                        ValuesStorage.SetEncrypted(KeyUserName, UserName);
                        ValuesStorage.SetEncrypted(KeyUserPassword, UserPassword);
                    } catch (Exception e) when (e.IsCancelled()) {
                        Phase = UploadPhase.Authorization;
                        CurrentError = null;
                        LoggedInAs = null;
                    } catch (Exception e) {
                        var wrongCredentials = e.Message == @"Forbidden";
                        Phase = UploadPhase.Authorization;
                        CurrentError = wrongCredentials ? "Username or password is wrong" : $"Failed to log in:\n• {e.FlattenMessage(";\n• ")}.";
                        LoggedInAs = null;
                        if (wrongCredentials && SilentLogIn) {
                            ValuesStorage.Remove(KeyUserName);
                            ValuesStorage.Remove(KeyUserPassword);
                        }
                    }
                    SilentLogIn = false;
                }, c => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(UserPassword)))
                        .ListenOn(this, nameof(UserName))
                        .ListenOn(this, nameof(UserPassword));

            private DelegateCommand _logOutCommand;

            public DelegateCommand LogOutCommand => _logOutCommand ?? (_logOutCommand = new DelegateCommand(() => {
                Phase = UploadPhase.Authorization;
                CurrentError = null;
                LoggedInAs = null;
                ValuesStorage.SetEncrypted(KeyUserName, null);
                ValuesStorage.SetEncrypted(KeyUserPassword, null);
            }, () => LoggedInAs != null)).ListenOn(this, nameof(LoggedInAs));

            private AsyncCommand _editProfleCommand;

            public AsyncCommand EditProfleCommand => _editProfleCommand ?? (_editProfleCommand = new AsyncCommand(async () => {
                if (_client == null || LoggedInAs == null) return;
                if (new WorkshopEditProfile(_client, LoggedInAs).ShowDialog() == true) {
                    LoggedInAs = await _client.GetAsync<UserInfo>("/manage/user-info");
                }
            }, () => LoggedInAs != null)).ListenOn(this, nameof(LoggedInAs));

            // Phase 2: preparation
            private bool _isOriginalWork;

            public bool IsOriginalWork {
                get => _isOriginalWork;
                set => Apply(value, ref _isOriginalWork);
            }


            /*public List<string> OriginOptions1 { get; } = new List<string> {
                "an original work",
                "a port to Assetto Corsa",
                "a port of another mod",
                "based on someone else’s work",
                "an alteration of Kunos content",
                "something else, namely"
            };

            private string _originOption1;

            public string OriginOption1 {
                get => _originOption1;
                set => Apply(value, ref _originOption1, () => {
                    OriginShowPortSources = value == "a port to Assetto Corsa";
                    OriginShowPortPermissions = value == "a port of another mod" || value == "based on someone else’s work";
                    OriginShowNamelyInput = value == "something else, namely";
                });
            }

            private string _originNamelyInput;

            public string OriginNamelyInput {
                get => _originNamelyInput;
                set => Apply(value, ref _originNamelyInput);
            }

            private bool _originShowNamelyInput;

            public bool OriginShowNamelyInput {
                get => _originShowNamelyInput;
                set => Apply(value, ref _originShowNamelyInput);
            }

            private bool _originShowPortSources;

            public bool OriginShowPortSources {
                get => _originShowPortSources;
                set => Apply(value, ref _originShowPortSources);
            }

            public List<string> OriginOptionsPortSources { get; } = new List<string> {
                "Forza",
                "Gran Turismo",
                "Need For Speed",
                "Assetto Corsa Competizione",
                "Project Cars",
                "another simracing title",
                "another non-simracing game"
            };

            private string _originOptionsPortSource;

            public string OriginOptionsPortSource {
                get => _originOptionsPortSource;
                set => Apply(value, ref _originOptionsPortSource);
            }

            private bool _originShowPortPermissions;

            public bool OriginShowPortPermissions {
                get => _originShowPortPermissions;
                set => Apply(value, ref _originShowPortPermissions);
            }

            public List<string> OriginOptionsPermissions { get; } = new List<string> {
                "with granted permission",
                "without granted permission"
            };

            private string _originOptionsPermissionState;

            public string OriginOptionsPermissionState {
                get => _originOptionsPermissionState;
                set => Apply(value, ref _originOptionsPermissionState);
            }*/
        }
    }
}