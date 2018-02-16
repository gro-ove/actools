using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public abstract class BatchAction : Displayable, IWithId {
        [CanBeNull]
        private readonly string _paramsTemplateKey;

        // This is a very bad place: I’ll change DisplayName later when building
        // a HierarchicalGroup, and I’ll need to know the original name. Very stupid,
        // of course, but, you know… If you know how to make a better version of
        // HierarchicalComboBox, let me know.
        public string BaseDisplayName { get; }

        public sealed override string DisplayName {
            get => base.DisplayName;
            set => base.DisplayName = value;
        }

        public string Id { get; protected set; }
        public bool InternalWaitingDialog { get; protected set; }
        public int Priority { get; protected set; }
        public string Description { get; }
        public string GroupPath { get; }

        private string _displayApply;

        public string DisplayApply {
            get => _displayApply;
            protected set => Apply(value, ref _displayApply);
        }

        protected BatchAction(string displayName, string description, string groupPath, [CanBeNull] string paramsTemplateKey) {
            _paramsTemplateKey = paramsTemplateKey;
            Id = GetType().Name;
            BaseDisplayName = displayName;
            DisplayName = displayName;
            Description = description;
            GroupPath = groupPath;
        }

        public virtual void OnActionSelected() {}

        private static ResourceDictionary _commonBatchActionsResources;

        private static ResourceDictionary CommonBatchActionsResources
            => _commonBatchActionsResources ?? (_commonBatchActionsResources = new SharedResourceDictionary {
                Source = new Uri("/AcManager.Controls;component/Themes/AcListPage.CommonBatchActions.xaml", UriKind.Relative)
            });

        private ContentPresenter _params;

        [CanBeNull]
        public ContentPresenter GetParams(FrameworkElement parent) {
            if (_paramsTemplateKey == null) return null;

            try {
                return _params ?? (_params = new ContentPresenter {
                    ContentTemplate = (DataTemplate)(parent.Resources[_paramsTemplateKey] ?? CommonBatchActionsResources[_paramsTemplateKey]),
                    Content = this
                });
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        public virtual int OnSelectionChanged(IList list) {
            return list.Count;
        }

        public abstract Task ApplyAsync(IList list, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);

        public event EventHandler AvailabilityChanged;

        protected void RaiseAvailabilityChanged() {
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public abstract class BatchAction<T> : BatchAction where T : AcObjectNew {
        protected BatchAction(string displayName, string description, string groupPath, [CanBeNull] string paramsTemplateKey)
                : base(displayName, description, groupPath, paramsTemplateKey) { }

        protected IEnumerable<T> OfType(IList list) {
            return list.OfType<AcItemWrapper>().Select(x => x.Value).OfType<T>();
        }

        public override int OnSelectionChanged(IList list) {
            return OnSelectionChanged(OfType(list));
        }

        public virtual int OnSelectionChanged(IEnumerable<T> enumerable) {
            return enumerable.Count(IsAvailable);
        }

        public abstract bool IsAvailable(T obj);

        public override async Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var s = Stopwatch.StartNew();

            var l = OfType(list).ToList();
            for (var i = 0; i < l.Count; i++) {
                await ApplyOverrideAsync(l[i]);
                if (s.ElapsedMilliseconds > 5) {
                    progress?.Report(l[i].DisplayName, i, l.Count);
                    await Task.Delay(5);
                    if (cancellation.IsCancellationRequested) return;
                    s.Restart();
                }
            }

            OnSelectionChanged(list);
        }

        protected virtual Task ApplyOverrideAsync(T obj) {
            ApplyOverride(obj);
            return Task.Delay(0);
        }

        protected virtual void ApplyOverride(T obj) { }
    }
}