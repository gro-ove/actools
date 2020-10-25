using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.CustomShowroom;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcManager.Workshop;
using AcManager.Workshop.Data;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SharpCompress.Writers;

namespace AcManager.Pages.Workshop {
    public partial class WorkshopUpload {
        private ViewModel Model => (ViewModel)DataContext;

        public WorkshopUpload(IEnumerable<AcJsonObjectNew> objects) {
            DataContext = new ViewModel(objects);
            InitializeComponent();
        }

        public WorkshopUpload(params AcJsonObjectNew[] objects) : this((IEnumerable<AcJsonObjectNew>)objects) { }

        public class PlannedAction : NotifyPropertyChanged {
            public string IconFilename { get; }

            public string DisplayType { get; }

            public AcJsonObjectNew ObjectToUpload { get; }

            private bool _isActive = true;

            public bool IsActive {
                get => _isActive;
                set => Apply(value, ref _isActive, CommandManager.InvalidateRequerySuggested);
            }

            private AsyncProgressEntry _uploadProgress;

            public AsyncProgressEntry UploadProgress {
                get => _uploadProgress;
                set => Apply(value, ref _uploadProgress);
            }

            public PlannedAction(AcJsonObjectNew objectToUpload) {
                ObjectToUpload = objectToUpload;
                switch (objectToUpload) {
                    case CarObject car:
                        DisplayType = "Car";
                        IconFilename = car.BrandBadge;
                        break;
                    case CarSkinObject carSkin:
                        DisplayType = "Car skin";
                        IconFilename = carSkin.LiveryImage;
                        break;
                    default:
                        DisplayType = "Unsupported";
                        break;
                }
            }
        }

        public class PlannedActionRoot : PlannedAction {
            private bool _isFailed;

            public bool IsFailed {
                get => _isFailed;
                set => Apply(value, ref _isFailed, UpdateDisplayStatus);
            }

            private string _remoteVersion;

            public string RemoteVersion {
                get => _remoteVersion;
                set => Apply(value, ref _remoteVersion, UpdateDisplayStatus);
            }

            private bool _isAvailable;

            public bool IsAvailable {
                get => _isAvailable;
                set => Apply(value, ref _isAvailable, CommandManager.InvalidateRequerySuggested);
            }

            private bool _isBusy;

            public bool IsBusy {
                get => _isBusy;
                set => Apply(value, ref _isBusy);
            }

            private string _displayStatus;

            public string DisplayStatus {
                get => _displayStatus;
                set => Apply(value, ref _displayStatus);
            }

            private void UpdateDisplayStatus() {
                if (IsFailed) {
                    IsAvailable = false;
                    DisplayStatus = "Communication with CM Workshop failed";
                } else if (string.IsNullOrWhiteSpace(ObjectToUpload.Version)) {
                    IsAvailable = false;
                    DisplayStatus = "Version is not set";
                } else {
                    var remoteStatus = RemoteVersion == null ? "checking version online…"
                            : RemoteVersion == string.Empty ? "it’s going to be a new entry"
                                    : ObjectToUpload.Version.IsVersionNewerThan(RemoteVersion) ? $"online version is {RemoteVersion}, this will be an update"
                                            : ObjectToUpload.Version == RemoteVersion ? "the same version is already available online"
                                                    : $"online version is {RemoteVersion}, can’t downgrade";
                    IsAvailable = RemoteVersion != null && (RemoteVersion == string.Empty || ObjectToUpload.Version.IsVersionNewerThan(RemoteVersion));
                    DisplayStatus = $"Local version is {ObjectToUpload.Version}, {remoteStatus}";
                }
            }

            public List<PlannedAction> Children { get; }

            public PlannedActionRoot(AcJsonObjectNew objectToUpload) : base(objectToUpload) {
                switch (objectToUpload) {
                    case CarObject car:
                        Children = car.EnabledOnlySkins.Select(x => new PlannedAction(x)).ToList();
                        break;
                }
            }

            public async Task LoadVersionInformationAsync(WorkshopClient client) {
                switch (ObjectToUpload) {
                    case CarObject car:
                        try {
                            RemoteVersion = (await client.GetAsync<JObject>("/manage/cars/" + car.Id))[@"lastVersion"].ToString();
                        } catch (WorkshopException e) when (e.Code == HttpStatusCode.NotFound) {
                            RemoteVersion = string.Empty;
                        } catch {
                            IsFailed = false;
                            throw;
                        }
                        break;
                }
            }

            public async Task UploadAsync(WorkshopClient client, [CanBeNull] IUploadLogger log, Originality originality) {
                switch (ObjectToUpload) {
                    case CarObject car:
                        var temporaryData = FilesStorage.Instance.GetTemporaryDirectory("CM Workshop Upload");
                        var skins = Children.Where(x => x.IsActive).Select(x => x.ObjectToUpload).OfType<CarSkinObject>().ToList();

                        if (car.Id.Any(char.IsUpper)) {
                            throw new Exception("Upper case in car folder name is not allowed");
                        }

                        using (log?.Begin("Generating previews with default look")) {
                            var presetData = PresetsManager.Instance.GetBuiltInPresetData(@"Custom Previews", @"Kunos");
                            await CmPreviewsTools.UpdatePreviewAsync(new[] {
                                new ToUpdatePreview(car, skins.Where(x => !File.Exists(Path.Combine(temporaryData, $"preview_{car.Id}_{x.Id}.jpg"))).ToList())
                            }, CmPreviewsSettings.GetSerializedSavedOptions(presetData), @"Kunos",
                                    destinationOverrideCallback: skin => Path.Combine(temporaryData, $"preview_{car.Id}_{skin.Id}.jpg"),
                                    progress: log?.Progress(@"Updating skin"));
                        }

                        var filenameCarCompact = Path.Combine(temporaryData, $"car_compact_{car.Id}.zip");
                        var filenameCarFull = Path.Combine(temporaryData, $"car_full_{car.Id}.zip");
                        var urls = new Dictionary<string, string>();
                        var urlCarCompact = string.Empty;
                        var urlCarFull = string.Empty;

                        await TaskExtension.MakeList(async () => {
                            using (var op = log?.BeginParallel("Packing car without skins", @"Packing:")) {
                                await car.TryToPack(new CarObject.CarPackerParams {
                                    Destination = filenameCarCompact,
                                    ShowInExplorer = false,
                                    Override = key => {
                                        if (Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(key))) == "skins") {
                                            return (w, k) => { };
                                        }
                                        return null;
                                    },
                                    Progress = op
                                });
                            }

                            using (var op = log?.BeginParallel("Uploading car without skins")) {
                                urlCarCompact = await client.UploadAsync(File.ReadAllBytes(filenameCarCompact), $"{car.Id}_compact.zip", op);
                                log?.Write($"Car without skins: {urlCarCompact}");
                            }
                        }, async () => {
                            using (var op = log?.BeginParallel("Packing car with skins", @"Packing:")) {
                                await car.TryToPack(new CarObject.CarPackerParams {
                                    Destination = filenameCarFull,
                                    ShowInExplorer = false,
                                    Override = key => {
                                        if (Path.GetFileName(key) == "preview.jpg") {
                                            var skinId = Path.GetFileName(Path.GetDirectoryName(key));
                                            var filename = Path.Combine(temporaryData, $"preview_{car.Id}_{skinId}.jpg");
                                            if (File.Exists(filename)) {
                                                return (w, k) => w.Write(k, filename);
                                            }
                                        }
                                        return null;
                                    },
                                    Progress = op
                                });
                            }

                            using (var op = log?.BeginParallel("Uploading car with skins")) {
                                urlCarFull = await client.UploadAsync(File.ReadAllBytes(filenameCarFull), $"{car.Id}_full.zip", op);
                                log?.Write($"Car with skins: {urlCarFull}");
                            }
                        }).WhenAll();

                        using (log?.Begin("Uploading skins")) {
                            await skins.Select(async skin => {
                                var filenamePreview = Path.Combine(temporaryData, $"preview_{car.Id}_{skin.Id}.jpg");
                                var filenameSkin = Path.Combine(temporaryData, $"skin_{skin.Id}.zip");

                                using (var op = log?.BeginParallel($"Packing skin {skin.Id}", @"Packing:")) {
                                    await skin.TryToPack(new CarSkinObject.CarSkinPackerParams {
                                        Destination = filenameSkin,
                                        ShowInExplorer = false,
                                        Override = key => {
                                            if (Path.GetFileName(key) == "preview.jpg") {
                                                return (w, k) => w.Write(k, filenamePreview);
                                            }
                                            return null;
                                        },
                                        Progress = op
                                    });
                                }

                                using (var op = log?.BeginParallel($"Upload skin {skin.Id}")) {
                                    urls[$@"{skin.Id}_data"] = await client.UploadAsync(File.ReadAllBytes(filenameSkin), $"{skin.Id}.zip", op);
                                    op?.SetResult(urls[$@"{skin.Id}_data"]);
                                }

                                using (var op = log?.BeginParallel($"Uploading skin icon for {skin.Id}")) {
                                    urls[$@"{skin.Id}_icon"] = await client.UploadAsync(File.ReadAllBytes(skin.LiveryImage), $"{skin.Id}.png", op);
                                    op?.SetResult(urls[$@"{skin.Id}_icon"]);
                                }

                                using (var op = log?.BeginParallel($"Uploading skin preview for {skin.Id}")) {
                                    urls[$@"{skin.Id}_preview"] = await client.UploadAsync(File.ReadAllBytes(filenamePreview), $"{skin.Id}.jpg", op);
                                    op?.SetResult(urls[$@"{skin.Id}_preview"]);
                                }
                            }).WhenAll(10);
                        }

                        using (log?.Begin("Submitting car to CM Workshop")) {
                            await client.PutAsync($"/cars/{car.Id}", new {
                                name = car.Name,
                                description = car.Description?.Trim(),
                                tags = TagsCollection.CleanUp(car.Tags).OrderBy(x => x, TagsComparer.Instance).Distinct().ToList(),
                                version = car.Version,
                                downloadURL = urlCarCompact,
                                downloadFullURL = urlCarFull,
                                year = car.Year,
                                originality,
                                skins = skins.ToDictionary(skin => skin.Id, skin => new {
                                    name = skin.Name.Or(skin.NameFromId),
                                    skinIcon = urls.GetValueOrDefault($@"{skin.Id}_icon") ?? throw new Exception("CM Workshop skin icon is missing"),
                                    previewImage = urls.GetValueOrDefault($@"{skin.Id}_preview") ?? throw new Exception("CM Workshop skin preview is missing"),
                                    downloadURL = urls.GetValueOrDefault($@"{skin.Id}_data") ?? throw new Exception("CM Workshop skin data is missing"),
                                    driver = skin.DriverName,
                                    team = skin.Team,
                                    tags = TagsCollection.CleanUp(car.Tags).OrderBy(x => x, TagsComparer.Instance).Distinct().ToList(),
                                    number = FlexibleParser.TryParseInt(skin.SkinNumber),
                                    country = skin.Country,
                                    originality,
                                }),
                                carBrand = car.Brand,
                                carClass = car.CarClass,
                                country = car.Country,
                                specs = @"{}"
                                /*weight = FlexibleParser.TryParseDouble(car.SpecsWeight),
                                power = FlexibleParser.TryParseDouble(car.SpecsBhp),
                                torque = FlexibleParser.TryParseDouble(car.SpecsTorque),
                                speed = FlexibleParser.TryParseDouble(car.SpecsTopSpeed),
                                acceleration = FlexibleParser.TryParseDouble(car.SpecsAcceleration)*/
                            });
                        }
                        break;
                }
            }
        }

        public enum UploadPhase {
            /*[Description("Authorization")]
            Authorization = 1,*/

            [Description("Select items to upload")]
            Preparation = 2,

            [Description("Uploading…")]
            Upload = 3,

            [Description("Upload complete")]
            Finalization = 5
        }

        public class ViewModel : NotifyPropertyChanged {
            public List<PlannedActionRoot> PlannedActions { get; }

            private UploadPhase _phase;

            public UploadPhase Phase {
                get => _phase;
                set => Apply(value, ref _phase, () => { PhaseName = value.GetDescription(); });
            }

            private string _phaseName = "Authorization";

            public string PhaseName {
                get => _phaseName;
                set => Apply(value, ref _phaseName);
            }

            public ViewModel(IEnumerable<AcJsonObjectNew> objects) {
                PlannedActions = objects.Select(x => new PlannedActionRoot(x)).ToList();
            }

            // Common values
            private WorkshopClient _client;

            private string _currentError;

            public string CurrentError {
                get => _currentError;
                set => Apply(value, ref _currentError);
            }

            // Phase 1: authorization
            /*private bool _silentLogIn;

            public bool SilentLogIn {
                get => _silentLogIn;
                set => Apply(value, ref _silentLogIn);
            }

            private UserInfo _loggedInAs;

            public UserInfo LoggedInAs {
                get => _loggedInAs;
                set => Apply(value, ref _loggedInAs);
            }*/

            /*private AsyncCommand<CancellationToken?> _logInCommand;

            public AsyncCommand<CancellationToken?> LogInCommand
                => _logInCommand ?? (_logInCommand = new AsyncCommand<CancellationToken?>(async c => {
                    _client = new WorkshopClient("http://192.168.1.10:3000", UserName, UserPassword);
                    try {
                        LoggedInAs = await _client.GetAsync<UserInfo>("/users/~me", c.Straighten());
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
                    try {
                        await LoadVersionInformationAsync();
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t load information about remote objects", e);
                    }
                }, c => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(UserPassword)))
                        .ListenOn(this, nameof(UserName))
                        .ListenOn(this, nameof(UserPassword));*/

            /*private DelegateCommand _logOutCommand;

            public DelegateCommand LogOutCommand => _logOutCommand ?? (_logOutCommand = new DelegateCommand(() => {
                Phase = UploadPhase.Authorization;
                CurrentError = null;
                LoggedInAs = null;
            }, () => LoggedInAs != null)).ListenOn(this, nameof(LoggedInAs));

            private AsyncCommand _editProfleCommand;

            public AsyncCommand EditProfleCommand => _editProfleCommand ?? (_editProfleCommand = new AsyncCommand(async () => {
                if (_client == null || LoggedInAs == null) return;
                if (new WorkshopEditProfile(_client, LoggedInAs).ShowDialog() == true) {
                    LoggedInAs = await _client.GetAsync<UserInfo>("/users/~me");
                }
            }, () => LoggedInAs != null)).ListenOn(this, nameof(LoggedInAs));*/

            // Phase 2: preparation
            private async Task LoadVersionInformationAsync() {
                foreach (var action in PlannedActions) {
                    await action.LoadVersionInformationAsync(_client);
                }
            }

            private bool _isOriginalWork;

            public bool IsOriginalWork {
                get => _isOriginalWork;
                set => Apply(value, ref _isOriginalWork);
            }

            private AsyncCommand _uploadCommand;

            public AsyncCommand UploadCommand => _uploadCommand ?? (_uploadCommand = new AsyncCommand(async () => {
                var logger = new UploadLogger(UploadLog);

                Phase = UploadPhase.Upload;
                foreach (var action in PlannedActions) {
                    action.IsBusy = true;
                }

                foreach (var action in PlannedActions.Where(action => action.IsAvailable && action.IsActive)) {
                    using (var op = logger.Begin($@"Starting to upload {action.DisplayType.ToSentenceMember()} {action.ObjectToUpload.Name}")) {
                        try {
                            logger.Write($@"Version: {action.ObjectToUpload.Version}");
                            logger.Write($@"Remote version: {action.RemoteVersion.Or("none")}");
                            _client.MarkNewUploadGroup();
                            await action.UploadAsync(_client, logger, IsOriginalWork ? Originality.Original : Originality.Ported);
                        } catch (Exception e) {
                            Logging.Error(e);
                            logger.Error(e.FlattenMessage().JoinToString("\n\t"));
                            op.SetFailed();
                        }
                    }
                }
            }, () => _client != null && PlannedActions.Any(x => x.IsAvailable && x.IsActive)));

            // Phase 3: upload
            public BetterObservableCollection<string> UploadLog { get; } = new BetterObservableCollection<string>();
        }
    }
}