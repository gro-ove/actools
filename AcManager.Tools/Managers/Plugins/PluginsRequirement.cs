using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Managers.Plugins {
    public class PluginsRequirement : NotifyPropertyChanged {
        private readonly Func<PluginEntry, bool> _filter;
        public BetterListCollectionView ListView { get; }

        public PluginsRequirement(Func<PluginEntry, bool> filter) {
            _filter = filter;

            ListView = new BetterListCollectionView(PluginsManager.Instance.List);
            ListView.SortDescriptions.Add(new SortDescription(nameof(PluginEntry.Name), ListSortDirection.Ascending));
            ListView.Filter = Filter;
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

        public PluginsRequirement(params string[] ids) : this(p => ids.ArrayContains(p.Id)) {}

        private AsyncCommand _installAllCommand;

        public AsyncCommand InstallAllCommand => _installAllCommand ?? (_installAllCommand = new AsyncCommand(() => {
            return ListView.OfType<PluginEntry>().Select(x => x.InstallCommand.ExecuteAsync(null)).WhenAll();
        }, () => !IsReady));

        private bool _isReady;

        public bool IsReady {
            get => _isReady;
            set => Apply(value, ref _isReady, () => {
                _installAllCommand?.RaiseCanExecuteChanged();
                Ready?.Invoke(this, EventArgs.Empty);
            });
        }

        private void UpdateReady() {
            IsReady = ListView.OfType<PluginEntry>().All(x => x.IsReady);
        }

        public event EventHandler Ready;
    }
}