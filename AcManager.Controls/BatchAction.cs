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
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public abstract class BatchAction : Displayable {
        [CanBeNull]
        private readonly string _paramsTemplateKey;

        public sealed override string DisplayName {
            get => base.DisplayName;
            set => base.DisplayName = value;
        }

        public string DisplayApply { get; protected set; }
        public string Description { get; }
        public string GroupPath { get; }

        public BatchAction(string displayName, string description, string groupPath, [CanBeNull] string paramsTemplateKey) {
            _paramsTemplateKey = paramsTemplateKey;
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

        public abstract Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }

    public abstract class BatchAction<T> : BatchAction where T : AcObjectNew {
        public BatchAction(string displayName, string description, string groupPath, [CanBeNull] string paramsTemplateKey)
                : base(displayName, description, groupPath, paramsTemplateKey) { }

        public override void OnSelectionChanged(IList list) {
            OnSelectionChanged(list.OfType<AcItemWrapper>().Select(x => x.Value).OfType<T>());
        }

        public virtual void OnSelectionChanged(IEnumerable<T> enumerable){}

        public override bool IsAvailable(IList list) {
            return IsAvailable(list.OfType<AcItemWrapper>().Select(x => x.Value).OfType<T>());
        }

        public virtual bool IsAvailable(IEnumerable<T> enumerable) {
            return true;
        }

        public override async Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var s = Stopwatch.StartNew();

            foreach (var t in list.OfType<AcItemWrapper>().Select(x => x.Value).OfType<T>().ToList()) {
                ApplyOverride(t);
                if (s.ElapsedMilliseconds > 5) {
                    await Task.Delay(10);
                    if (cancellation.IsCancellationRequested) return;
                    s.Restart();
                }
            }

            OnSelectionChanged(list);
        }

        protected abstract void ApplyOverride(T obj);
    }
}