﻿using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Presets;
using AcTools.GenericMods;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class GenericModsListPage : ILoadableContent, IParametrizedUriContent {
        private IFilter<string> _filter;

        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            _filter = filter == null ? null : Filter.Create(StringTester.Instance, filter);
        }

        public ViewModel Model => (ViewModel)DataContext;

        [CanBeNull]
        private GenericModsEnabler _enabler;

        public async Task LoadAsync(CancellationToken cancellationToken) {
            var mods = SettingsHolder.GenericMods.GetModsDirectory();
            if (Directory.Exists(mods)) {
                _enabler = await GenericModsEnabler.GetInstanceAsync(AcRootDirectory.Instance.RequireValue,
                        mods, SettingsHolder.GenericMods.UseHardLinks);
            }
        }

        public void Load() {
            var mods = SettingsHolder.GenericMods.GetModsDirectory();
            if (Directory.Exists(mods)) {
                _enabler = GenericModsEnabler.GetInstanceAsync(AcRootDirectory.Instance.RequireValue,
                        mods, SettingsHolder.GenericMods.UseHardLinks).Result;
            }
        }

        public void Initialize() {
            DataContext = new ViewModel(_enabler, _filter);
            InitializeComponent();
            this.OnActualUnload(Model.Unload);
        }

        public class ViewModel : IUserPresetable {
            [CanBeNull]
            private readonly GenericModsEnabler _enabler;

            public bool IsNothing => _enabler == null;

            [CanBeNull]
            private readonly IFilter<string> _filter;

            [CanBeNull]
            public BetterListCollectionView Disabled { get; }

            [CanBeNull]
            public BetterListCollectionView Enabled { get; }

            public ViewModel([CanBeNull] GenericModsEnabler enabler, IFilter<string> filter) {
                _enabler = enabler;
                _filter = filter;

                if (enabler != null) {
                    Disabled = new BetterListCollectionView(enabler.Mods) { Filter = CreateFilter(false) };
                    Enabled = new BetterListCollectionView(enabler.Mods) { Filter = CreateFilter(true) };

                    Prepare(Enabled, "enabled");
                    Prepare(Disabled, "disabled");

                    enabler.Mods.ItemPropertyChanged += OnItemPropertyChanged;
                    enabler.Mods.CollectionChanged += OnCollectionChanged;
                    PresetableCategory = new PresetsCategory(enabler.ModsDirectory, ".mep");
                } else {
                    PresetableCategory = new PresetsCategory(SettingsHolder.GenericMods.GetModsDirectory(), ".mep");
                }
            }

            public void Unload() {
                var enabler = _enabler;
                if (enabler != null) {
                    enabler.Mods.ItemPropertyChanged -= OnItemPropertyChanged;
                    enabler.Mods.CollectionChanged -= OnCollectionChanged;
                }
            }

            private Predicate<object> CreateFilter(bool enabled) {
                return _filter == null
                        ? (enabled ?
                                (Predicate<object>)(o => (o as GenericMod)?.IsEnabled == true) :
                                (o => (o as GenericMod)?.IsEnabled == false))
                        : (enabled ?
                                (Predicate<object>)(o => o is GenericMod m && _filter.Test(m.DisplayName) && m.IsEnabled) :
                                (o => o is GenericMod m && _filter.Test(m.DisplayName) && !m.IsEnabled));
            }

            private class SortComparer : IComparer {
                public int Compare(object x, object y) {
                    var ix = x as GenericMod;
                    var iy = y as GenericMod;
                    if (ix == null) return iy == null ? 0 : 1;
                    if (iy == null) return -1;
                    if (ix.AppliedOrder != iy.AppliedOrder) return ix.AppliedOrder - iy.AppliedOrder;
                    return AlphanumComparatorFast.Compare(ix.DisplayName, iy.DisplayName);
                }
            }

            private void Prepare(BetterListCollectionView view, string key) {
                view.CustomSort = new SortComparer();

                var storageKey = $".genericMods.selected:{key}";

                void LoadCurrent() {
                    var selected = ValuesStorage.Get<string>(storageKey);
                    if (selected != null) {
                        view.MoveCurrentTo(view.OfType<GenericMod>().FirstOrDefault(x => x.DisplayName == selected) ??
                                view.OfType<GenericMod>().FirstOrDefault());
                    }
                }

                LoadCurrent();
                view.CurrentChanged += async (sender, args) => {
                    if (view.CurrentItem is GenericMod selected) {
                        ValuesStorage.Set(storageKey, selected.DisplayName);
                    } else {
                        await Task.Delay(1);
                        LoadCurrent();
                    }
                };
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
                // _loadCurrent.ForEach(x => x.Invoke());
            }

            private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
                if (propertyChangedEventArgs.PropertyName == nameof(GenericMod.AppliedOrder)) {
                    _enabler?.Mods.RefreshFilter((GenericMod)sender);
                    _busy.Do(() => Changed?.Invoke(this, EventArgs.Empty));
                }
            }

            private readonly Busy _busy = new Busy();

            public Task EnableMod(GenericMod mod) {
                return _busy.Task(async () => {
                    if (_enabler == null || mod.IsEnabled) return;

                    var conflicts = await _enabler.CheckConflictsAsync(mod);
                    if (conflicts.Length > 0 && ModernDialog.ShowMessage(
                            conflicts.Select(x => $@"• “{Path.GetFileName(x.RelativeName)}” has already been altered by the “{x.ModName}” mod;")
                                    .JoinToString("\n").ToSentence
                                    ()
                                    + $"\n\nEnabling {BbCodeBlock.Encode(mod.DisplayName)} may have adverse effects. Are you sure you want to enable this mod?",
                            "Conflict", MessageBoxButton.YesNo, "genericMods.conflict") != MessageBoxResult.Yes) {
                        return;
                    }

                    try {
                        using (var waiting = WaitingDialog.Create("Enabling mod…")) {
                            await _enabler.EnableAsync(mod, waiting, waiting.CancellationToken);
                            Changed?.Invoke(this, EventArgs.Empty);

                            if (waiting.CancellationToken.IsCancellationRequested) {
                                waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Cancellation…"));
                                await _enabler.DisableAsync(mod);
                            }
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t enable mod", e);
                    }
                });
            }

            public Task DisableMod(GenericMod mod) {
                return _busy.Task(async () => {
                    if (_enabler == null || !mod.IsEnabled) return;

                    try {
                        using (var waiting = WaitingDialog.Create("Disabling mod…")) {
                            await _enabler.DisableAsync(mod, waiting, waiting.CancellationToken);
                            Changed?.Invoke(this, EventArgs.Empty);

                            if (waiting.CancellationToken.IsCancellationRequested) {
                                waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Cancellation…"));
                                await _enabler.EnableAsync(mod);
                            }
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t disable mod", e);
                    }
                });
            }

            private AsyncCommand _enableCommand;

            public AsyncCommand EnableCommand => _enableCommand ?? (_enableCommand = new AsyncCommand(() =>
                    !(Disabled?.CurrentItem is GenericMod selected) ? Task.Delay(0) : EnableMod(selected)));

            private AsyncCommand _disableCommand;

            public AsyncCommand DisableCommand => _disableCommand ?? (_disableCommand = new AsyncCommand(() =>
                    !(Enabled?.CurrentItem is GenericMod selected) ? Task.Delay(0) : DisableMod(selected)));

            private AsyncCommand _disableAllCommand;

            public AsyncCommand DisableAllCommand => _disableAllCommand ?? (_disableAllCommand = new AsyncCommand(() => _busy.Task(async () => {
                if (_enabler == null || Enabled == null) return;

                var enabled = Enabled.OfType<GenericMod>().OrderByDescending(x => x.AppliedOrder).ToList();
                if (enabled.Count == 0) return;

                try {
                    using (var waiting = WaitingDialog.Create("Disabling mods…")) {
                        for (var i = 0; i < enabled.Count; i++) {
                            var mod = enabled[i];
                            waiting.Report(mod.DisplayName, i, enabled.Count);
                            if (waiting.CancellationToken.IsCancellationRequested) return;

                            await _enabler.DisableAsync(mod, null, waiting.CancellationToken);
                            Changed?.Invoke(this, EventArgs.Empty);

                            if (waiting.CancellationToken.IsCancellationRequested) {
                                waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Cancellation…"));
                                await _enabler.EnableAsync(mod);
                            }
                        }
                    }
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t disable all mods", e);
                }
            })));

            #region Presetable
            public bool CanBeSaved => true;
            public string PresetableKey => "jsgme";
            public PresetsCategory PresetableCategory { get; }
            public event EventHandler Changed;

            string IUserPresetable.ExportToPresetData() {
                return Enabled?.OfType<GenericMod>().OrderBy(x => x.AppliedOrder).Select(x => x.DisplayName).JoinToString(Environment.NewLine);
            }

            void IUserPresetable.ImportFromPresetData(string data) {
                if (_enabler == null || Enabled == null || Disabled == null) return;

                _busy.Task(async () => {
                    await Task.Yield();

                    var names = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var enabled = Enabled.OfType<GenericMod>().OrderByDescending(x => x.AppliedOrder).ToList();

                    try {
                        using (var waiting = WaitingDialog.Create("Loading mod profile…")) {
                            for (var i = 0; i < enabled.Count; i++) {
                                var mod = enabled[i];
                                waiting.Report(mod.DisplayName, i, enabled.Count);
                                if (waiting.CancellationToken.IsCancellationRequested) return;

                                await _enabler.DisableAsync(mod, null, waiting.CancellationToken);
                                if (waiting.CancellationToken.IsCancellationRequested) {
                                    waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Cancellation…"));
                                    await _enabler.EnableAsync(mod);
                                }
                            }

                            for (var i = 0; i < names.Length; i++) {
                                var mod = _enabler.GetByName(names[i]);
                                if (mod == null) continue;

                                waiting.Report(mod.DisplayName, i, enabled.Count);
                                if (waiting.CancellationToken.IsCancellationRequested) return;

                                await _enabler.EnableAsync(mod, null, waiting.CancellationToken);
                                if (waiting.CancellationToken.IsCancellationRequested) {
                                    waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Cancellation…"));
                                    await _enabler.DisableAsync(mod);
                                }
                            }
                        }
                    } catch (Exception e) {
                        NonfatalError.Notify("Can’t load mod profile", e);
                    }
                }).Ignore();
            }
            #endregion
        }

        private void OnItemClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount != 2) return;
            if (!((sender as FrameworkElement)?.DataContext is GenericMod item)) return;

            if (item.IsEnabled) {
                Model.DisableCommand.ExecuteAsync().Ignore();
            } else {
                Model.EnableCommand.ExecuteAsync().Ignore();
            }

            e.Handled = true;
        }

        private void CreateDirectoryButtonClick(object sender, RoutedEventArgs e) {
            FileUtils.EnsureDirectoryExists(SettingsHolder.GenericMods.GetModsDirectory());
            NavigationCommands.Refresh.Execute(null, this);
            // this.GetParents().OfType<ModernFrame>().FirstOrDefault().
        }

        private void OnDisabledListDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(GenericMod.DraggableFormat) is GenericMod mod) {
                Model.DisableMod(mod);
            }
        }

        private void OnEnabledListDrop(object sender, DragEventArgs e) {
            if (e.Data.GetData(GenericMod.DraggableFormat) is GenericMod mod) {
                Model.EnableMod(mod);
            }
        }
    }
}