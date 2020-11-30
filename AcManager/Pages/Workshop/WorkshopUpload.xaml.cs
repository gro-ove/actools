using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.AcObjectsNew;
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
using Newtonsoft.Json.Linq;

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

            public AcJsonObjectNew Target { get; }

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

            public PlannedAction(AcJsonObjectNew target, bool isChildObject) {
                Target = target;
                switch (target) {
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

                var validationResults = WorkshopValidatorFactory.Create(target, isChildObject).Validate().ToList();
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
                } else {
                    var localVersion = Target.Version.Or(@"0");
                    var remoteStatus = RemoteVersion == null ? "checking version online…"
                            : RemoteVersion == string.Empty ? "it’s going to be a new entry"
                                    : localVersion.IsVersionNewerThan(RemoteVersion) ? $"online version is {RemoteVersion}, this will be an update"
                                            : localVersion == RemoteVersion ? "the same version is already available online"
                                                    : $"online version is {RemoteVersion}, can’t downgrade";
                    IsAvailable = RemoteVersion != null && (RemoteVersion == string.Empty || localVersion.IsVersionNewerThan(RemoteVersion));
                    DisplayStatus = $"Local version is {localVersion}, {remoteStatus}";
                }
            }

            public List<PlannedActionsGroup> ChildrenGroups { get; } = new List<PlannedActionsGroup>();

            public PlannedActionRoot(AcJsonObjectNew target) : base(target, false) {
                switch (target) {
                    case CarObject car:
                        ChildrenGroups.Add(new PlannedActionsGroup("skins", AppStrings.Main_Skins,
                                car.EnabledOnlySkins.Select(x => new PlannedAction(x, true))) {
                                    AtLeastOneIsRequired = true
                                });
                        break;
                }
            }

            public async Task LoadVersionInformationAsync(WorkshopClient client, WorkshopCollabModel collabModel) {
                try {
                    switch (Target) {
                        case CarObject car:
                            if (car.SoundDonor != null && car.SoundDonor.Author == @"Kunos" && car.SoundDonorId != @"tatuusfa1") {
                                var userId = WorkshopClient.GetUserId(@"Kunos");
                                if (collabModel.References.GetByIdOrDefault(userId) == null) {
                                    collabModel.References.Add(new WorkshopCollabReference {
                                        UserId = userId,
                                        Role = "Sound"
                                    });
                                }
                            }

                            var existing = await client.GetAsync<WorkshopContentCar>("/cars/" + car.Id);
                            RemoteVersion = existing.Version;
                            if (existing.Collabs.MainUserRole != null) {
                                collabModel.UserRole = existing.Collabs.MainUserRole;
                            }
                            foreach (var reference in existing.Collabs.CollabReferences) {
                                if (collabModel.References.GetByIdOrDefault(reference.UserId) == null) {
                                    collabModel.References.Add(new WorkshopCollabReference {
                                        UserId = reference.UserId,
                                        Role = reference.Role
                                    });
                                }
                            }
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

            public void ApplyAutoFixes() {
                foreach (var action in IterateActions()) {
                    foreach (var item in action.ValidationFixable) {
                        item.FixCallback?.Invoke();
                    }
                }
            }

            public async Task SaveChanged() {
                foreach (var action in IterateActions()) {
                    if (action.Target.Changed) {
                        await action.Target.SaveAsync();
                    }
                }
            }

            private JObject PrepareDataToSubmit(WorkshopSubmitterParams submitterParams) {
                var ignoring = IterateActions().Select(x => x.Target.IgnoreChanges()).ToList();
                try {
                    ApplyAutoFixes();
                    return WorkshopSubmitterFactory.Create(Target, submitterParams).BuildPayload();
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
                    await submitterParams.Client.PutAsync($"/cars/{Target.Id}/validate", dataToSubmit);
                    ValidationPassed.Insert(0, new WorkshopValidatedItem("Remote validation passed"));
                    _remoteValidationComplete = true;
                } catch (WorkshopException e) {
                    ValidationFailed.Insert(0, new WorkshopValidatedItem("Remote validation failed: " + e.Message, WorkshopValidatedState.Failed));
                    _remoteValidationComplete = true;
                }
            }

            public async Task UploadAsync(WorkshopSubmitterParams submitterParams) {
                var client = submitterParams.Client;
                switch (Target) {
                    case CarObject car:
                        var submitter = WorkshopSubmitterFactory.Create(car, submitterParams);
                        await submitter.PrepareAsync();
                        using (submitterParams.Log?.BeginParallel("Submitting car to CM Workshop")) {
                            await client.PutAsync($"/cars/{car.Id}", submitter.BuildPayload());
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
                        CollabModel,
                        PlannedActions
                                .Concat(PlannedActions.SelectMany(x => x.ChildrenGroups).SelectMany(x => x.Children))
                                .Where(x => !x.IsActive).Select(x => x.Target).ToList());
            }

            private Busy _prepareBusy = new Busy();

            private void PrepareUpload() {
                Phase = UploadPhase.Preparation;
                _prepareBusy.Task(async () => {
                    try {
                        var submitterParams = GetWorkshopSubmitterParams(false);
                        foreach (var action in PlannedActions) {
                            await action.LoadVersionInformationAsync(WorkshopHolder.Client, CollabModel);
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

            public WorkshopCollabModel CollabModel { get; } = new WorkshopCollabModel();

            private AsyncCommand _uploadCommand;

            public AsyncCommand UploadCommand => _uploadCommand ?? (_uploadCommand = new AsyncCommand(async () => {
                Phase = UploadPhase.Upload;
                foreach (var action in PlannedActions) {
                    action.IsBusy = true;
                }

                var submitterParams = GetWorkshopSubmitterParams(true);
                foreach (var action in PlannedActions.Where(action => action.IsAvailable && action.IsActive)) {
                    using (var op = submitterParams.Log?.Begin($@"Starting to upload {action.DisplayType.ToSentenceMember()} {action.Target.Name}")) {
                        try {
                            submitterParams.Log?.Write($@"Version: {action.Target.Version?.Or(@"0")}");
                            submitterParams.Log?.Write($@"Remote version: {action.RemoteVersion.Or("none")}");
                            using (submitterParams.Log?.BeginParallel("Applying auto-fixes")) {
                                action.ApplyAutoFixes();
                                await action.SaveChanged();
                            }
                            await action.UploadAsync(submitterParams);
                        } catch (Exception e) {
                            Logging.Error(e);
                            submitterParams.Log?.Error(e.FlattenMessage().JoinToString("\n\t"));
                            op?.SetFailed();
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