using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.Controls {
    internal abstract class SizeRelatedCondition {
        public static SizeRelatedCondition Create<T, TParent>([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                float widthThreshold, [NotNull] Action<T, Visibility> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold,
                    (t, b) => action(t, b ? Visibility.Visible : Visibility.Collapsed));
        }

        public static SizeRelatedCondition Create<T, TParent>([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, Visibility> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition, (t, b) => action(t, b ? Visibility.Visible : Visibility.Collapsed));
        }

        public static SizeRelatedCondition Create<T, TParent>([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                float widthThreshold) where TParent : FrameworkElement where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold,
                    (t, b) => t.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition Create<T, TParent>([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition) where TParent : FrameworkElement where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition,
                    (t, b) => t.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition Create<T, TParent>([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                float widthThreshold, [NotNull] Action<T, bool> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold, action);
        }

        public static SizeRelatedCondition Create<T, TParent>([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, bool> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition, action);
        }

        public abstract void Update();
    }

    internal class SizeRelatedCondition<T, TParent> : SizeRelatedCondition where TParent : FrameworkElement {
        private readonly TParent _parent;
        private readonly Func<TParent, T> _getChild;
        private readonly Func<TParent, bool> _condition;
        private readonly Action<T, bool> _action;

        private T _element;

        [CanBeNull]
        private T Element {
            get { return _element; }
            set {
                if (Equals(value, _element)) return;

                if (value == null != (_element == null)) {
                    if (value == null) {
                        _parent.SizeChanged -= OnParentSizeChanged;
                    } else {
                        _parent.SizeChanged += OnParentSizeChanged;
                    }
                }

                _element = value;
                _state = null;
                UpdateState();
            }
        }

        public override void Update() {
            Element = _getChild(_parent);
        }

        internal SizeRelatedCondition([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, bool> action) {
            _parent = parent;
            _getChild = getChild;
            _condition = condition;
            _action = action;
        }

        private void OnParentSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            UpdateState();
        }

        private bool? _state;

        private void UpdateState() {
            if (Element == null) return;

            var state = _condition(_parent);
            if (_state != state) {
                _action(Element, state);
            }
        }
    }

    public class RaceGridEditorTable : Control {
        static RaceGridEditorTable() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RaceGridEditorTable), new FrameworkPropertyMetadata(typeof(RaceGridEditorTable)));
        }

        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(RaceGridViewModel),
                typeof(RaceGridEditorTable), new PropertyMetadata(null, (o, e) => {
                    var t = (RaceGridEditorTable)o;
                    t._model = (RaceGridViewModel)e.NewValue;
                    t.UpdateConditions();
                }));

        private RaceGridViewModel _model;

        public RaceGridViewModel Model {
            get { return _model; }
            set { SetValue(ModelProperty, value); }
        }

        public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
                typeof(RaceGridEditorTable), new PropertyMetadata(null, (o, e) => {
                    ((RaceGridEditorTable)o)._closeCommand = (ICommand)e.NewValue;
                }));

        private ICommand _closeCommand;

        public ICommand CloseCommand {
            get { return _closeCommand; }
            set { SetValue(CloseCommandProperty, value); }
        }

        public static readonly DependencyProperty AddOpponentCommandProperty = DependencyProperty.Register(nameof(AddOpponentCommand), typeof(ICommand),
                typeof(RaceGridEditorTable), new PropertyMetadata(null, (o, e) => {
                    ((RaceGridEditorTable)o)._addOpponentCommand = (ICommand)e.NewValue;
                }));

        private ICommand _addOpponentCommand = null;

        public ICommand AddOpponentCommand {
            get { return _addOpponentCommand; }
            set { SetValue(AddOpponentCommandProperty, value); }
        }

        private DataGrid _dataGrid;
        private readonly SizeRelatedCondition[] _sizeRelated;

        public RaceGridEditorTable() {
            _sizeRelated = new[] {
                SizeRelatedCondition.Create(this,
                        t => GetTemplateChild(@"PART_NameColumn") as DataGridColumn, 640, (c, b) => c.Visibility = b),
                SizeRelatedCondition.Create(this,
                        t => GetTemplateChild(@"PART_NationalityColumn") as DataGridColumn, 980, (c, b) => c.Visibility = b),
                SizeRelatedCondition.Create(this,
                        t => GetTemplateChild(@"PART_BallastColumn") as DataGridColumn, 740, (c, b) => c.Visibility = b),
                SizeRelatedCondition.Create(this,
                        t => GetTemplateChild(@"PART_RestrictorColumn") as DataGridColumn, 840, (c, b) => c.Visibility = b),
                SizeRelatedCondition.Create(this,
                        t => GetTemplateChild(@"PART_PlayerBallast") as FrameworkElement, p => p.ActualWidth >= 740 && p._model.PlayerCar != null),
                SizeRelatedCondition.Create(this,
                        t => GetTemplateChild(@"PART_PlayerRestrictor") as FrameworkElement, 840)
            };
        }

        private void UpdateConditions() {
            foreach (var condition in _sizeRelated) {
                condition.Update();
            }
        }

        public override void OnApplyTemplate() {
            if (_dataGrid != null) {
                _dataGrid.Drop -= OnDrop;
            }

            base.OnApplyTemplate();

            _dataGrid = GetTemplateChild(@"PART_DataGrid") as DataGrid;

            if (_dataGrid != null) {
                _dataGrid.Drop += OnDrop;
            }

            UpdateConditions();
        }

        private void OnDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null || Model == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            var newIndex = ((ItemsControl)sender).GetMouseItemIndex();
            if (raceGridEntry != null) {
                Model.InsertEntry(newIndex, raceGridEntry);
            } else {
                Model.InsertEntry(newIndex, carObject);
            }

            e.Effects = DragDropEffects.Move;
        }
    }
}