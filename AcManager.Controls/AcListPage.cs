using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AcManager.Controls.Helpers;
using AcManager.Controls.ViewModels;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using Binding = System.Windows.Data.Binding;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;

namespace AcManager.Controls {
    public class AcListPage : Control {
        static AcListPage() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcListPage), new FrameworkPropertyMetadata(typeof(AcListPage)));
        }

        private ListBox _list;

        private readonly DelayedPropertyWrapper<AcItemWrapper> _selectedWrapper;
        private AcObjectNew _currentObject;

        public AcObjectNew CurrentObject {
            get => _currentObject;
            set {
                if (_currentObject != null) {
                    _currentObject.AcObjectOutdated -= OnSelectedObjectOutdated;
                }

                _currentObject = value;

                if (_currentObject != null) {
                    _currentObject.AcObjectOutdated += OnSelectedObjectOutdated;
                    SetCurrentValue(SelectedSourceProperty, AcObjectsUriManager.GetUri(_currentObject));
                } else {
                    SetCurrentValue(SelectedSourceProperty, null);
                }
            }
        }

        private static readonly DependencyPropertyKey SelectedWrapperPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedWrapper), typeof(AcItemWrapper),
                typeof(AcListPage), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedWrapperProperty = SelectedWrapperPropertyKey.DependencyProperty;

        public AcItemWrapper SelectedWrapper => (AcItemWrapper)GetValue(SelectedWrapperProperty);

        public AcListPage() {
            _selectedWrapper = new DelayedPropertyWrapper<AcItemWrapper>(async v => {
                CurrentObject = v?.Loaded();

                if (v != null && SettingsHolder.Content.ScrollAutomatically) {
                    await Task.Delay(1);
                    _list?.ScrollIntoView(_list.SelectedItem);
                }
            });

            PreviewMouseRightButtonDown += OnRightMouseDown;
            PreviewKeyDown += OnKeyDown;
            SizeChanged += OnSizeChanged;
        }

        private BatchAction[] _batchActionsArray;
        private BatchAction[] GetBatchActionsArray() {
            return _batchActionsArray ?? (_batchActionsArray = GetBatchActions().ToArrayIfItIsNot());
        }

        protected virtual IEnumerable<BatchAction> GetBatchActions() {
            return new BatchAction[0];
        }

        private void OnKeyDown(object sender, KeyEventArgs args) {
            if (BatchMenuVisible && (args.Key == Key.Escape || args.Key == Key.Back)) {
                args.Handled = true;
                SetMultiSelectionMode(false);
            } else if (Keyboard.Modifiers == ModifierKeys.Control && args.Key == Key.A) {
                SetMultiSelectionMode(true);
            }
        }

        public static readonly DependencyProperty BatchMenuVisibleProperty = DependencyProperty.Register(nameof(BatchMenuVisible), typeof(bool),
                typeof(AcListPage), new PropertyMetadata(false, (o, e) => {
                    ((AcListPage)o)._batchMenuVisible = (bool)e.NewValue;
                }));

        private bool _batchMenuVisible;

        public bool BatchMenuVisible {
            get => _batchMenuVisible;
            set => SetValue(BatchMenuVisibleProperty, value);
        }

        private void OnLeftMouseDown(object sender, MouseButtonEventArgs e) {
            if ((Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
                    Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) && !BatchMenuVisible) {
                var index = _list.GetMouseItemIndex();
                SetMultiSelectionMode(true);
                if (!BatchMenuVisible) return;

                if (index != -1) {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                        var current = _list.SelectedIndex;
                        var addToSelection = new List<object>();
                        for (var i = current; i != index; i += current < index ? 1 : -1) {
                            addToSelection.Add(_list.ItemsSource?.OfType<object>().ElementAtOrDefault(i));
                        }

                        addToSelection.Add(_list.ItemsSource?.OfType<object>().ElementAtOrDefault(index));
                        foreach (var o in addToSelection.NonNull()) {
                            _list.SelectedItems.Add(o);
                        }
                    } else {
                        var item = _list.ItemsSource?.OfType<object>().ElementAtOrDefault(index);
                        if (item != null) {
                            _list.SelectedItems.Add(item);
                        }
                    }
                }
            }
        }

        private void OnRightMouseDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
        }

        private Style _baseItemStyle;
        private Style _checkBoxListBoxItemStyle;
        private readonly BoolHolder _holder = new BoolHolder();

        public class BoolHolder : NotifyPropertyChanged {
            private bool _value;

            public bool Value {
                get => _value;
                set {
                    if (Equals(value, _value)) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public static BoolHolder GetActiveBoolHolder(DependencyObject obj) {
            return (BoolHolder)obj.GetValue(ActiveBoolHolderProperty);
        }

        public static void SetActiveBoolHolder(DependencyObject obj, BoolHolder value) {
            obj.SetValue(ActiveBoolHolderProperty, value);
        }

        public static readonly DependencyProperty ActiveBoolHolderProperty = DependencyProperty.RegisterAttached("ActiveBoolHolder", typeof(BoolHolder),
                typeof(AcListPage), new FrameworkPropertyMetadata(new BoolHolder(), FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetNarrowList(DependencyObject obj) {
            return (bool)obj.GetValue(NarrowListProperty);
        }

        public static void SetNarrowList(DependencyObject obj, bool value) {
            obj.SetValue(NarrowListProperty, value);
        }

        public static readonly DependencyProperty NarrowListProperty = DependencyProperty.RegisterAttached("NarrowList", typeof(bool),
                typeof(AcListPage), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        private bool _batchActionsSet;

        private bool SetMultiSelectionMode(bool? newValue = null) {
            if (BatchMenuVisible == newValue) return false;
            if (!BatchMenuVisible && GetBatchActionsArray().Length == 0) return false;

            if (_checkBoxListBoxItemStyle == null) {
                _baseItemStyle = _list.ItemContainerStyle;

                var originStyle = (Style)FindResource("AcListPageCheckBoxItem");

                var style = new Style(typeof(ListBoxItem));
                foreach (var setter in originStyle.Setters) {
                    style.Setters.Add(setter);
                }

                foreach (var trigger in originStyle.Triggers) {
                    style.Triggers.Add(trigger);
                }

                style.Setters.Add(new Setter(ActiveBoolHolderProperty, _holder));
                style.Seal();

                _checkBoxListBoxItemStyle = style;
            }

            if (newValue ?? !BatchMenuVisible) {
                _list.ItemContainerStyle = _checkBoxListBoxItemStyle;
                _list.SetValue(ListBoxHelper.ProperMultiSelectionModeProperty, true);
                _list.SelectionChanged += OnMultiSelectionChanged;
                SetBatchActions();
                SetCheckBoxMode(true).Forget();
            } else {
                _list.SelectionChanged -= OnMultiSelectionChanged;
                SetCheckBoxMode(false, () => {
                    _list.ItemContainerStyle = _baseItemStyle;
                    _list.SetValue(ListBoxHelper.ProperMultiSelectionModeProperty, false);
                }).Forget();
            }

            BatchMenuVisible = !BatchMenuVisible;
            Draggable.SetForceDisabled(this, BatchMenuVisible);
            return true;
        }

        private class BatchActionComparer : IComparer<object> {
            public static readonly BatchActionComparer Instance = new BatchActionComparer();

            public int Compare(object x, object y) {
                var gx = x as HierarchicalGroup;
                var gy = y as HierarchicalGroup;

                string sx, sy;
                if (gx != null) {
                    if (gy == null) return -1;
                    sx = gx.DisplayName;
                    sy = gy.DisplayName;
                } else {
                    if (gy != null) return 1;
                    sx = (x as BatchAction)?.DisplayName;
                    sy = (y as BatchAction)?.DisplayName;
                }

                return sx == null ?
                        (sy == null ? 0 : 1) :
                        (sy == null ? -1 : string.Compare(sx, sy, StringComparison.InvariantCulture));
            }
        }

        private HierarchicalGroup GroupItems() {
            var result = new HierarchicalGroup();
            var dict = new Dictionary<string, HierarchicalGroup> {
                [""] = result
            };

            HierarchicalGroup GetGroup(string group) {
                if (string.IsNullOrWhiteSpace(group)) return result;
                if (dict.TryGetValue(group, out var v)) return v;
                var i = group.LastIndexOf('/');
                v = new HierarchicalGroup(i == -1 ? group : group.Substring(i + 1));
                (i == -1 ? result : GetGroup(group.Substring(0, i))).Add(v);
                dict[group] = v;
                return v;
            }

            foreach (var action in GetBatchActionsArray()) {
                GetGroup(action.GroupPath).Add(action);
            }

            foreach (var group in dict.Values) {
                group.Sort(BatchActionComparer.Instance);
            }

            return result;
        }

        private string _batchActionKey;

        private void SetBatchActions() {
            if (_batchActionsSet || _batchActions == null) return;
            _batchActionsSet = true;

            var grouped = GroupItems();
            _batchActions.ItemsSource = grouped;
            _batchActions.SetBinding(HierarchicalComboBox.SelectedItemProperty, new Binding(nameof(SelectedBatchAction)) {
                Source = this,
                Mode = BindingMode.TwoWay
            });

            if (_batchActionKey == null) {
                _batchActionKey = "batchAction:" + GetType().Name;
            }

            var selected = ValuesStorage.GetString(_batchActionKey);
            SelectedBatchAction = (selected == null ? null : grouped.GetByIdOrDefault<BatchAction>(selected)) ??
                    grouped.FirstOrDefault<BatchAction>();
        }

        public static readonly DependencyProperty SelectedBatchActionProperty = DependencyProperty.Register(nameof(SelectedBatchAction), typeof(BatchAction),
                typeof(AcListPage), new PropertyMetadata(OnSelectedBatchActionChanged));

        public BatchAction SelectedBatchAction {
            get => (BatchAction)GetValue(SelectedBatchActionProperty);
            set => SetValue(SelectedBatchActionProperty, value);
        }

        private static void OnSelectedBatchActionChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((AcListPage)o).OnSelectedBatchActionChanged((BatchAction)e.NewValue);
        }

        [CanBeNull]
        private BatchAction _selectedBatchAction;

        private void OnSelectedBatchActionChanged([CanBeNull] BatchAction newValue) {
            _selectedBatchAction = newValue;
            if (_batchActionKey != null && _selectedBatchAction != null) {
                ValuesStorage.Set(_batchActionKey, _selectedBatchAction.Id);
            }

            var hasParams = newValue?.GetParams(this) != null;
            if (hasParams != SelectedBatchActionHasParams) {
                _selectedBatchActionHasParams = hasParams;
                SetValue(SelectedBatchActionHasParamsPropertyKey, hasParams);

                if (_batchActionParams != null) {
                    try {
                        var size = _batchActionParams.ActualWidth;
                        var duration = TimeSpan.FromMilliseconds(size / 4);

                        _batchActionParamsTransform.BeginAnimation(TranslateTransform.XProperty, hasParams ? new DoubleAnimation {
                            To = 0d,
                            Duration = duration,
                            FillBehavior = FillBehavior.HoldEnd,
                            EasingFunction = (EasingFunctionBase)FindResource("DecelerationEase")
                        } : new DoubleAnimation {
                            To = -size,
                            Duration = duration,
                            FillBehavior = FillBehavior.HoldEnd,
                            EasingFunction = (EasingFunctionBase)FindResource("AccelerationEase")
                        });
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }

                    if (newValue?.GetParams(this) != null) {
                        _batchAction.Content = newValue.GetParams(this);
                    }
                }
            }

            PrepareBatchAction();
        }

        private void PrepareBatchAction() {
            _selectedBatchAction?.OnSelectionChanged(_list.SelectedItems);
        }

        private static readonly DependencyPropertyKey SelectedBatchActionHasParamsPropertyKey =
                DependencyProperty.RegisterReadOnly(nameof(SelectedBatchActionHasParams), typeof(bool),
                        typeof(AcListPage), new PropertyMetadata(false));

        public static readonly DependencyProperty SelectedBatchActionHasParamsProperty = SelectedBatchActionHasParamsPropertyKey.DependencyProperty;

        private bool _selectedBatchActionHasParams;
        public bool SelectedBatchActionHasParams => _selectedBatchActionHasParams;

        private void OnMultiSelectionChanged(object sender, SelectionChangedEventArgs selectionChangedEventArgs) {
            SetValue(SelectedAmountPropertyKey, (DataContext as IAcListPageViewModel)?.GetNumberString(_list.SelectedItems.Count));
            PrepareBatchAction();
        }

        public static readonly DependencyPropertyKey SelectedAmountPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedAmount), typeof(string),
                typeof(AcListPage), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedAmountProperty = SelectedAmountPropertyKey.DependencyProperty;

        public string SelectedAmount => (string)GetValue(SelectedAmountProperty);

        private async Task SetCheckBoxMode(bool value, Action callback = null) {
            await Task.Delay(1);
            _holder.Value = value;
            if (callback != null) {
                await Task.Delay(200);
                callback.Invoke();
            }
        }

        public static readonly DependencyProperty AddNewCommandProperty = DependencyProperty.Register(nameof(AddNewCommand), typeof(CommandBase),
                typeof(AcListPage), new PropertyMetadata(OnAddNewCommandChanged));

        public CommandBase AddNewCommand {
            get => (CommandBase)GetValue(AddNewCommandProperty);
            set => SetValue(AddNewCommandProperty, value);
        }

        private static void OnAddNewCommandChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((AcListPage)o).OnAddNewCommandChanged((CommandBase)e.NewValue);
        }

        private void OnAddNewCommandChanged(ICommand newValue) {
            InputBindings.Clear();
            InputBindings.Add(new InputBinding(newValue, new KeyGesture(Key.N, ModifierKeys.Control | ModifierKeys.Shift)));
        }

        private Button _addButton, _batchActionRunButton, _batchActionCloseButton;
        private HierarchicalComboBox _batchActions;
        private ContentPresenter _batchAction;
        private FrameworkElement _batchActionParams;
        private TranslateTransform _batchActionParamsTransform;
        //private FrameworkElement _frame;
        //private DoubleAnimation _batchActionParamsAnimation;
        private SizeRelatedCondition[] _listSizeConditions;

        public override void OnApplyTemplate() {
            DisposeHelper.Dispose(ref _listSizeConditions);

            if (_list != null) {
                _list.PreviewMouseLeftButtonDown -= OnLeftMouseDown;
                _list.SizeChanged -= OnListSizeChanged;
            }

            if (_addButton != null) {
                _addButton.Click -= OnAddButtonClick;
            }

            if (_batchActionCloseButton != null) {
                _batchActionCloseButton.Click -= OnBatchActionCloseButtonClick;
            }

            if (_batchActionRunButton != null) {
                _batchActionRunButton.Click -= OnBatchActionRunButtonClick;
            }

            base.OnApplyTemplate();
            _list = GetTemplateChild(@"ItemsList") as ListBox;
            _addButton = GetTemplateChild(@"AddCarButton") as Button;
            _batchActions = GetTemplateChild(@"PART_BatchActions") as HierarchicalComboBox;
            _batchAction = GetTemplateChild(@"PART_BatchAction") as ContentPresenter;
            _batchActionParams = GetTemplateChild(@"PART_BatchActionParams") as FrameworkElement;
            _batchActionRunButton = GetTemplateChild(@"PART_BatchBlock_RunButton") as Button;
            _batchActionCloseButton = GetTemplateChild(@"PART_BatchBlock_CloseButton") as Button;
            //_frame = GetTemplateChild(@"PART_Frame") as FrameworkElement;
            //_batchActionParamsAnimation = GetTemplateChild(@"PART_BatchActionParams_Animation") as DoubleAnimation;

            if (_list != null) {
                _list.ScrollIntoView(_list.SelectedItem);
                _list.PreviewMouseLeftButtonDown += OnLeftMouseDown;
                _list.SizeChanged += OnListSizeChanged;
                _listSizeConditions = new SizeRelatedCondition[] {
                    _list.AddWidthCondition(80)
                         .Add(() => _batchActionCloseButton)
                         .Add(x => (Grid)GetTemplateChild("PART_BatchBlock_ButtonsGrid"),
                                 (c, x) => c.ColumnDefinitions[1].Width = new GridLength(x ? 8 : 0, GridUnitType.Pixel)),
                    _list.AddWidthCondition(140).Add(() => (FrameworkElement)GetTemplateChild("PART_BatchBlock_RunButton_Text")),
                    _list.AddWidthCondition(180)
                         .Add(() => (FrameworkElement)GetTemplateChild("PART_BatchBlock_CloseButton_Text"))
                         .Add(x => (Grid)GetTemplateChild("PART_BatchBlock_ButtonsGrid"),
                                 (c, x) => c.ColumnDefinitions.Last().Width = new GridLength(1, x ? GridUnitType.Star : GridUnitType.Auto)),
                    _list.AddWidthCondition(200).Add(x => SetNarrowList(_list, !x)),
                };
            }

            if (_addButton != null) {
                _addButton.Click += OnAddButtonClick;
            }

            if (_batchActionCloseButton != null) {
                _batchActionCloseButton.Click += OnBatchActionCloseButtonClick;
            }

            if (_batchActionRunButton != null) {
                _batchActionRunButton.Click += OnBatchActionRunButtonClick;
            }

            if (_batchActionParams != null) {
                _batchActionParamsTransform = new TranslateTransform();
                _batchActionParams.RenderTransform = _batchActionParamsTransform;
            }

            UpdateBatchBlocksSizes();
        }

        private readonly Busy _batchActionRunBusy = new Busy();
        private void OnBatchActionRunButtonClick(object sender, RoutedEventArgs args) {
            try {
                _batchActionRunBusy.Task(async () => {
                    if (_selectedBatchAction == null || _list == null) return;

                    if (_selectedBatchAction.InternalWaitingDialog) {
                        await _selectedBatchAction.ApplyAsync(_list.SelectedItems, null, default(CancellationToken));
                    } else {
                        using (var waiting = new WaitingDialog()) {
                            waiting.Report(AsyncProgressEntry.FromStringIndetermitate("Processing…"));
                            await _selectedBatchAction.ApplyAsync(_list.SelectedItems, waiting, waiting.CancellationToken);
                        }
                    }
                });
            } catch (Exception e) {
                NonfatalError.Notify("Batch processing failed", e);
            }
        }

        private void OnListSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            UpdateBatchBlocksSizes();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            UpdateBatchBlocksSizes();
        }

        private void UpdateBatchBlocksSizes() {
            if (_batchActionParams != null) {
                _batchActionParamsTransform.BeginAnimation(TranslateTransform.XProperty, null);
                if (SelectedBatchActionHasParams) {
                    _batchActionParamsTransform.X = 0d;
                } else {
                    _batchActionParamsTransform.X = -_batchActionParams.ActualWidth;
                }
            }
        }

        public static readonly DependencyProperty SaveScrollKeyProperty = DependencyProperty.Register(nameof(SaveScrollKey), typeof(string),
                typeof(AcListPage));

        public string SaveScrollKey {
            get => (string)GetValue(SaveScrollKeyProperty);
            set => SetValue(SaveScrollKeyProperty, value);
        }

        private void OnAddButtonClick(object sender, RoutedEventArgs e) {
            var command = AddNewCommand;
            if (command?.IsAbleToExecute == true) {
                var list = ItemsSource as INotifyCollectionChanged;
                if (list != null) {
                    list.CollectionChanged += OnAddButtonCollectionChanged;
                }

                ((ICommand)command).Execute(null);

                if (list != null) {
                    list.CollectionChanged -= OnAddButtonCollectionChanged;
                }
            }
        }

        private void OnBatchActionCloseButtonClick(object sender, RoutedEventArgs e) {
            SetMultiSelectionMode(false);
        }

        private void OnAddButtonCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems.Count == 1) {
                ItemsSource.MoveCurrentTo(e.NewItems[0]);
            }
        }

        private void OnCurrentChanged(object sender, EventArgs e) {
            var newValue = ItemsSource.CurrentItem as AcItemWrapper;
            SetValue(SelectedWrapperPropertyKey, newValue);

            if (ItemsSource.Contains(_selectedWrapper.Value)) {
                _selectedWrapper.Value = newValue;
            } else {
                _selectedWrapper.ForceValue(newValue);
            }
        }

        private void OnSelectedObjectOutdated(object sender, EventArgs e) {
            SetCurrentValue(SelectedSourceProperty, null);

            var oldItem = _selectedWrapper.Value;
            var id = (sender as AcObjectNew)?.Id;
            if (id == null) return;

            var newItem = ItemsSource.OfType<AcItemWrapper>().GetByIdOrDefault(id);
            if (oldItem != null && newItem == oldItem) {
                CurrentObject = newItem.Loaded();
            } else if (newItem != null) {
                ItemsSource.MoveCurrentTo(newItem);
            } else {
                var replacement = ItemsSource.OfType<AcItemWrapper>().FirstOrDefault(x => (x.Value as AcCommonObject)?.PreviousId == id);
                if (replacement != null) {
                    ItemsSource.MoveCurrentTo(replacement);
                    return;
                }

                if (CurrentObject != null) {
                    SetCurrentValue(SelectedSourceProperty, AcObjectsUriManager.GetUri(CurrentObject));
                } else {
                    ItemsSource.MoveCurrentToFirst();
                }
            }
        }

        #region Control Properies
        public static readonly DependencyProperty SelectedSourceProperty = DependencyProperty.Register(nameof(SelectedSource), typeof(Uri),
            typeof(AcListPage), new PropertyMetadata());

        public Uri SelectedSource => (Uri)GetValue(SelectedSourceProperty);

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(AcWrapperCollectionView),
            typeof(AcListPage), new PropertyMetadata(OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((AcListPage)d).OnItemsSourceChanged((AcWrapperCollectionView)e.OldValue, (AcWrapperCollectionView)e.NewValue);
        }

        private void OnItemsSourceChanged(AcWrapperCollectionView oldValue, AcWrapperCollectionView newValue) {
            if (oldValue != null) {
                oldValue.CurrentChanged -= OnCurrentChanged;
            }

            if (newValue != null) {
                newValue.CurrentChanged += OnCurrentChanged;
                _selectedWrapper.Value = newValue.CurrentItem as AcItemWrapper;
                SetValue(SelectedWrapperPropertyKey, newValue.CurrentItem as AcItemWrapper);
            }
        }

        public AcWrapperCollectionView ItemsSource {
            get => (AcWrapperCollectionView)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty IsGroupingEnabledProperty = DependencyProperty.Register(nameof(IsGroupingEnabled), typeof(bool),
                typeof(AcListPage));

        public bool IsGroupingEnabled {
            get => (bool)GetValue(IsGroupingEnabledProperty);
            set => SetValue(IsGroupingEnabledProperty, value);
        }
        #endregion
    }

    public class AcObjectEventArgs : EventArgs {
        public AcObjectEventArgs(AcObjectNew obj) {
            AcObject = obj;
        }

        public AcObjectNew AcObject { get; }
    }
}
