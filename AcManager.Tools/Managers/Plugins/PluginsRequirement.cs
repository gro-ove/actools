using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Plugins {
    public class PluginsRequirement : NotifyPropertyChanged {
        [NotNull]
        private readonly Func<PluginEntry, bool> _filter;

        [CanBeNull]
        private readonly string[] _required;

        [NotNull]
        public BetterListCollectionView ListView { get; }

        private PluginsRequirement([CanBeNull] Func<PluginEntry, bool> filter, [CanBeNull] string[] ids) {
            if (PluginsManager.Instance == null) {
                throw new Exception("PluginsManager.Instance is not set");
            }

            _required = ids;
            _filter = filter ?? (p => ids?.ArrayContains(p.Id) != false);

            ListView = new BetterListCollectionView(PluginsManager.Instance.List);
            ListView.SortDescriptions.Add(new SortDescription(nameof(PluginEntry.Name), ListSortDirection.Ascending));

            try {
                ListView.Filter = Filter;
            } catch (Exception e) {
                // What the hell is that?
                Logging.Error(e);
            }

            UpdateReady();

            if (!IsReady) {
                PluginsManager.Instance.UpdateIfObsolete();
            }

            WeakEventManager<PluginsManager, PluginEventArgs>.AddHandler(PluginsManager.Instance, nameof(PluginsManager.PluginEnabled),
                    (sender, args) => UpdateReady());
            WeakEventManager<PluginsManager, PluginEventArgs>.AddHandler(PluginsManager.Instance, nameof(PluginsManager.PluginDisabled),
                    (sender, args) => UpdateReady());
            WeakEventManager<PluginsManager, EventArgs>.AddHandler(PluginsManager.Instance, nameof(PluginsManager.ListUpdated),
                    (sender, args) => UpdateReady());
        }

        private bool Filter(object o) {
            return o is PluginEntry p && _filter.Invoke(p);
        }

        public PluginsRequirement(Func<PluginEntry, bool> filter) : this(filter, null) { }
        public PluginsRequirement(params string[] ids) : this(null, ids) { }

        private AsyncCommand _installAllCommand;

        public AsyncCommand InstallAllCommand => _installAllCommand ?? (_installAllCommand = new AsyncCommand(
                () => ListView.OfType<PluginEntry>().Select(x => x.InstallCommand.ExecuteAsync(null)).WhenAll(),
                () => !IsReady));

        private bool _isReady;

        public bool IsReady {
            get => _isReady;
            set => Apply(value, ref _isReady, () => {
                _installAllCommand?.RaiseCanExecuteChanged();
                Ready?.Invoke(this, EventArgs.Empty);
            });
        }

        private void UpdateReady() {
            IsReady = _required?.All(x => PluginsManager.Instance.IsPluginEnabled(x))
                    ?? ListView.OfType<PluginEntry>().All(x => x.IsReady);
        }

        public event EventHandler Ready;
    }
}