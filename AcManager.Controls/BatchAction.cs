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

        public sealed override string DisplayName {
            get => base.DisplayName;
            set => base.DisplayName = value;
        }

        public string Id { get; protected set; }
        public bool InternalWaitingDialog { get; protected set; }
        public string DisplayApply { get; protected set; }
        public string Description { get; }
        public string GroupPath { get; }

        public BatchAction(string displayName, string description, string groupPath, [CanBeNull] string paramsTemplateKey) {
            _paramsTemplateKey = paramsTemplateKey;
            Id = GetType().Name;
            DisplayName = displayName;
            Description = description;
            GroupPath = groupPath;
        }

        private ContentPresenter _params;

        [CanBeNull]
        public ContentPresenter GetParams(FrameworkElement parent) {
            if (_paramsTemplateKey == null) return null;

            if (_params == null) {
                var template = (DataTemplate)parent.Resources[_paramsTemplateKey];
                _params = new ContentPresenter {
                    ContentTemplate = template,
                    Content = this
                };

                _params.Loaded += OnParamsLoaded;
            }

            return _params;
        }

        private void OnParamsLoaded(object sender, RoutedEventArgs args) {
        }

        public virtual void OnSelectionChanged(IList list) {
        }

        public virtual bool IsAvailable(IList list) {
            return true;
        }

        public abstract Task ApplyAsync(IList list, [CanBeNull] IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }

    public abstract class BatchAction<T> : BatchAction where T : AcObjectNew {
        public BatchAction(string displayName, string description, string groupPath, [CanBeNull] string paramsTemplateKey)
                : base(displayName, description, groupPath, paramsTemplateKey) { }

        protected IEnumerable<T> OfType(IList list) {
            return list.OfType<AcItemWrapper>().Select(x => x.Value).OfType<T>();
        }

        public override void OnSelectionChanged(IList list) {
            OnSelectionChanged(OfType(list));
        }

        public virtual void OnSelectionChanged(IEnumerable<T> enumerable){}

        public override bool IsAvailable(IList list) {
            return IsAvailable(OfType(list));
        }

        public virtual bool IsAvailable(IEnumerable<T> enumerable) {
            return true;
        }

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