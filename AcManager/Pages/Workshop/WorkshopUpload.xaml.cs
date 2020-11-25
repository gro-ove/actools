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
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Objects;
using AcManager.Tools.WorkshopPublishTools.Submitters;
using AcManager.Tools.WorkshopPublishTools.Validators;
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

            public List<WorkshopValidatedItem> ValidationPassed { get; }
            public List<WorkshopValidatedItem> ValidationFixable { get; }
            public List<WorkshopValidatedItem> ValidationWarning { get; }
            public List<WorkshopValidatedItem> ValidationFailed { get; }

            public PlannedAction(AcJsonObjectNew objectToUpload, bool isChildObject) {
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

                var validationResults = WorkshopValidatorFactory.Create(objectToUpload, isChildObject).Validate().ToList();
                ValidationPassed = validationResults.Where(x => x.State == WorkshopValidatedState.Passed).ToList();
                ValidationFixable = validationResults.Where(x => x.State == WorkshopValidatedState.Fixable).ToList();
                ValidationWarning = validationResults.Where(x => x.State == WorkshopValidatedState.Warning).ToList();
                ValidationFailed = validationResults.Where(x => x.State == WorkshopValidatedState.Failed).ToList();
            }
        }

        public sealed class PlannedActionsGroup : Displayable, IWithId {
            public string Id { get; }

            public List<PlannedAction> Children { get; }

            public string DisplayTitle => $"{DisplayName} ({Children.Count})";

            public bool AtLeastOneIsRequired { get; set; }

            public int ChildrenValidationPassed { get; }
            public int ChildrenValidationFixable { get; }
            public int ChildrenValidationWarning { get; }
            public int ChildrenValidationFailed { get; }

            public PlannedActionsGroup([Localizable(false)] string id, string name, IEnumerable<PlannedAction> children) {
                Id = id;
                DisplayName = name;
                Children = children.ToList();
                ChildrenValidationPassed = Children.Sum(x => x.ValidationPassed.Count);
                ChildrenValidationFixable = Children.Sum(x => x.ValidationFixable.Count);
                ChildrenValidationWarning = Children.Sum(x => x.ValidationWarning.Count);
                ChildrenValidationFailed = Children.Sum(x => x.ValidationFailed.Count);
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

            public List<PlannedActionsGroup> ChildrenGroups { get; } = new List<PlannedActionsGroup>();

            public PlannedActionRoot(AcJsonObjectNew objectToUpload) : base(objectToUpload, false) {
                switch (objectToUpload) {
                    case CarObject car:
                        ChildrenGroups.Add(new PlannedActionsGroup("skins", AppStrings.Main_Skins,
                                car.EnabledOnlySkins.Select(x => new PlannedAction(x, true))) {
                                    AtLeastOneIsRequired = true
                                });
                        break;
                }
            }

            public async Task LoadVersionInformationAsync(WorkshopClient client) {
                try {
                    switch (ObjectToUpload) {
                        case CarObject car:
                            RemoteVersion = (await client.GetAsync<JObject>("/cars/" + car.Id))[@"lastVersion"].ToString();
                            break;
                    }
                } catch (WorkshopException e) when (e.Code == HttpStatusCode.NotFound) {
                    RemoteVersion = string.Empty;
                } catch {
                    IsFailed = false;
                    throw;
                }
            }

            private bool _remoteValidationComplete;

            private IEnumerable<PlannedAction> IterateActions() {
                return new[] { this }.Concat(ChildrenGroups.SelectMany(childGroup => childGroup.Children));
            }

            private JObject PrepareDataToSubmit(WorkshopSubmitterParams submitterParams) {
                var ignoring = IterateActions().Select(x => x.ObjectToUpload.IgnoreChanges()).ToList();
                JObject dataToSubmit;

                try {
                    foreach (var workshopValidatedItem in IterateActions().SelectMany(x => x.ValidationFixable)) {
                        workshopValidatedItem.FixCallback?.Invoke();
                    }
                    return WorkshopSubmitterFactory.Create(ObjectToUpload, submitterParams).BuildPayload();
                } finally {
                    foreach (var workshopValidatedItem in IterateActions().SelectMany(x => x.ValidationFixable)) {
                        workshopValidatedItem.RollbackCallback?.Invoke();
                    }
                    ignoring.DisposeEverything();
                }
            }

            public async Task ValidateResourceRemotelyAsync(WorkshopSubmitterParams submitterParams) {
                if (_remoteValidationComplete) return;

                var dataToSubmit = PrepareDataToSubmit(submitterParams);
                try {
                    await submitterParams.Client.PutAsync($"/cars/{ObjectToUpload.Id}/validate", dataToSubmit);
                    ValidationPassed.Insert(0, new WorkshopValidatedItem("Remote validation passed"));
                    _remoteValidationComplete = true;
                } catch (WorkshopException e) {
                    ValidationFailed.Insert(0, new WorkshopValidatedItem("Remote validation failed: " + e.Message, WorkshopValidatedState.Failed));
                    _remoteValidationComplete = true;
                }
            }

            public async Task UploadAsync(WorkshopSubmitterParams submitterParams, [CanBeNull] IUploadLogger log) {
                var client = submitterParams.Client;
                switch (ObjectToUpload) {
                    case CarObject car:
                        var temporaryData = FilesStorage.Instance.GetTemporaryDirectory("CM Workshop Upload");
                        var skins = ChildrenGroups.GetById("skins").Children.Where(x => x.IsActive)
                                .Select(x => x.ObjectToUpload).OfType<CarSkinObject>().ToList();

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
                            await client.PutAsync($"/cars/{car.Id}", PrepareDataToSubmit(submitterParams));
                        }
                        break;
                }
            }
        }

        public enum UploadPhase {
            [Description("Upload failed")]
            Failed = 0,

            [Description("Preparation…")]
            Preparation = 1,

            [Description("Select items to upload")]
            Setup = 2,

            [Description("Uploading…")]
            Upload = 3,

            [Description("Upload complete")]
            Finalization = 5,
        }

        public class ViewModel : NotifyPropertyChanged {
            public List<PlannedActionRoot> PlannedActions { get; }

            private UploadPhase _phase;

            public UploadPhase Phase {
                get => _phase;
                set => Apply(value, ref _phase, () => { PhaseName = value.GetDescription(); });
            }

            private string _phaseName;

            public string PhaseName {
                get => _phaseName;
                set => Apply(value, ref _phaseName);
            }

            public ViewModel(IEnumerable<AcJsonObjectNew> objects) {
                PlannedActions = objects.Select(x => new PlannedActionRoot(x)).ToList();

                if (WorkshopHolder.Model.AuthorizedAs != null) {
                    PrepareUpload();
                }
                WorkshopHolder.Model.SubscribeWeak(OnWorkshopModelChanged);
            }

            private void OnWorkshopModelChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(WorkshopModel.AuthorizedAs)) {
                    PrepareUpload();
                }
            }

            private WorkshopSubmitterParams GetWorkshopSubmitterParams(bool withProgressLogging) {
                return new WorkshopSubmitterParams(WorkshopHolder.Client,
                        withProgressLogging ? new UploadLogger(UploadLog) : null,
                        IsOriginalWork ? WorkshopOriginality.Original : WorkshopOriginality.Ported,
                        PlannedActions
                                .Concat(PlannedActions.SelectMany(x => x.ChildrenGroups).SelectMany(x => x.Children))
                                .Where(x => !x.IsActive).Select(x => x.ObjectToUpload).ToList());
            }

            private Busy _prepareBusy = new Busy();

            private void PrepareUpload() {
                Phase = UploadPhase.Preparation;
                _prepareBusy.Task(async () => {
                    try {
                        var submitterParams = GetWorkshopSubmitterParams(false);
                        foreach (var action in PlannedActions) {
                            await action.LoadVersionInformationAsync(WorkshopHolder.Client);
                            await action.ValidateResourceRemotelyAsync(submitterParams);
                        }
                        CurrentError = null;
                        Phase = UploadPhase.Setup;
                    } catch (Exception e) {
                        CurrentError = WorkshopHelperUtils.GetDisplayErrorMessage(e);
                        Phase = UploadPhase.Failed;
                    }
                }).Ignore();
            }

            // Common values
            private string _currentError;

            public string CurrentError {
                get => _currentError;
                set => Apply(value, ref _currentError);
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

                var submitterParams = GetWorkshopSubmitterParams(true);
                foreach (var action in PlannedActions.Where(action => action.IsAvailable && action.IsActive)) {
                    using (var op = logger.Begin($@"Starting to upload {action.DisplayType.ToSentenceMember()} {action.ObjectToUpload.Name}")) {
                        try {
                            logger.Write($@"Version: {action.ObjectToUpload.Version}");
                            logger.Write($@"Remote version: {action.RemoteVersion.Or("none")}");
                            await action.UploadAsync(submitterParams, logger);
                        } catch (Exception e) {
                            Logging.Error(e);
                            logger.Error(e.FlattenMessage().JoinToString("\n\t"));
                            op.SetFailed();
                        }
                    }
                }
            }, () => PlannedActions.Any(x => x.IsAvailable && x.IsActive
                    && x.ChildrenGroups.All(y => !y.AtLeastOneIsRequired || y.Children.Any(z => z.IsActive)))));

            private DelegateCommand _retryCommand;

            public DelegateCommand RetryCommand => _retryCommand ?? (_retryCommand = new DelegateCommand(PrepareUpload));

            // Phase 3: upload
            public BetterObservableCollection<string> UploadLog { get; } = new BetterObservableCollection<string>();
        }
    }
}