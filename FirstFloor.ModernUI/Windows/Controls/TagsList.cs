using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Presentation;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Data;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Attached;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class TagsListItem : Control {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(string),
            typeof(TagsListItem), new PropertyMetadata(OnValueChanged));

        private static void OnValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((TagsListItem)o).OnValueChanged((string)e.OldValue, (string)e.NewValue);
        }

        private void OnValueChanged(string oldValue, string newValue) {
            if (_parent == null || oldValue == null) return;
            if (string.IsNullOrWhiteSpace(newValue)) {
                _parent.RemoveTag(oldValue);
            } else {
                _parent.ReplaceTag(oldValue, newValue.Trim());
            }
        }

        public TagsListItem(string value, TagsList parent) {
            DefaultStyleKey = typeof(TagsListItem);
            Value = value;
            _parent = parent;

            SetValue(ContextMenuProperty, _parent.ItemContextMenu);
            SetValue(ContextMenuAdvancement.PropagateToChildrenProperty, true);
        }

        public string Value {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        private readonly TagsList _parent;

        private ICommand _removeTagCommand;

        public ICommand RemoveTagCommand => _removeTagCommand ?? (_removeTagCommand = new RelayCommand(o => {
            if (_parent == null || Value == null) return;
            _parent.RemoveTag(Value);
        }));
    }

    public class TagsListCollection : ObservableCollection<TagsListItem> {
        public TagsListCollection(IEnumerable<TagsListItem> list)
            : base(list) {
        }
    }

    public class ReadOnlyTagsListCollection : ReadOnlyObservableCollection<TagsListItem> {
        public ReadOnlyTagsListCollection(TagsListCollection list)
            : base(list) {
            List = list;
        }

        public ReadOnlyTagsListCollection(IEnumerable<TagsListItem> list)
            : this(new TagsListCollection(list)) {
        }

        internal TagsListCollection List { get; private set; }
    }

    public partial class TagsList : Control {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(ObservableCollection<string>),
            typeof(TagsList), new PropertyMetadata(OnItemsSourceChanged));

        public static readonly DependencyProperty SuggestionsSourceProperty = DependencyProperty.Register("SuggestionsSource", typeof(CollectionView),
            typeof(TagsList));

        private static readonly DependencyPropertyKey ItemsPropertyKey = DependencyProperty.RegisterReadOnly("Items", typeof(ReadOnlyTagsListCollection),
            typeof(TagsList), null);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;

        public static readonly DependencyProperty ItemContextMenuProperty = DependencyProperty.Register(nameof(ItemContextMenu), typeof(ContextMenu),
                typeof(TagsList));

        public ContextMenu ItemContextMenu {
            get { return (ContextMenu)GetValue(ItemContextMenuProperty); }
            set { SetValue(ItemContextMenuProperty, value); }
        }

        public TagsList() {
            DefaultStyleKey = typeof(TagsList);
        }

        public ObservableCollection<string> ItemsSource {
            get { return (ObservableCollection<string>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public CollectionView SuggestionsSource {
            get { return (CollectionView)GetValue(SuggestionsSourceProperty); }
            set { SetValue(SuggestionsSourceProperty, value); }
        }

        private static void OnItemsSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((TagsList)o).OnItemsSourceChanged((ObservableCollection<string>)e.OldValue, (ObservableCollection<string>)e.NewValue);
        }

        private void OnItemsSourceChanged(ObservableCollection<string> oldValue, ObservableCollection<string> newValue) {
            if (oldValue != null) {
                oldValue.CollectionChanged -= ItemsSource_CollectionChanged;
            }

            UpdateReadOnlyCollection(newValue);

            if (newValue != null) {
                newValue.CollectionChanged += ItemsSource_CollectionChanged;
            }
        }

        private void UpdateReadOnlyCollection(ObservableCollection<string> newValue) {
            SetValue(ItemsPropertyKey, newValue == null ? null
                    : new ReadOnlyTagsListCollection(from x in newValue select new TagsListItem(x, this)));
        }

        void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateReadOnlyCollection(sender as ObservableCollection<string>);
        }

        public ReadOnlyTagsListCollection Items => (ReadOnlyTagsListCollection)GetValue(ItemsProperty);

        private ComboBox _previousTextBox;

        public override void OnApplyTemplate() {
            if (_previousTextBox != null) {
                _previousTextBox.PreviewKeyDown -= TextBox_KeyDown;
                _previousTextBox.LostFocus -= TextBox_LostFocus;
                _previousTextBox = null;
            }

            base.OnApplyTemplate();

            var textBox = Template.FindName("NewTagTextBox", this) as ComboBox;
            if (textBox == null) return;

            textBox.PreviewKeyDown += TextBox_KeyDown;
            textBox.LostFocus += TextBox_LostFocus;
            _previousTextBox = textBox;
        }

        private void AddNewTag(string tag) {
            var tagLower = tag.ToLower();
            if (ItemsSource.FirstOrDefault(x => x.ToLower() == tagLower) != null) return;
            ItemsSource.Add(tag);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key != Key.Enter && e.Key != Key.Tab) return;

            var textBox = sender as ComboBox;
            if (string.IsNullOrWhiteSpace(textBox?.Text)) return;

            AddNewTag(textBox.Text.Trim());
            textBox.Text = "";

            if (e.Key == Key.Tab) {
                e.Handled = true;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e) {
            var textBox = sender as ComboBox;
            if (string.IsNullOrWhiteSpace(textBox?.Text)) return;

            AddNewTag(textBox.Text.Trim());
            textBox.Text = "";
        }

        public void RemoveTag(string value) {
            ItemsSource.Remove(value);
        }

        public void ReplaceTag(string oldValue, string newValue) {
            if (ItemsSource.Contains(oldValue)) {
                ItemsSource[ItemsSource.IndexOf(oldValue)] = newValue;
            } else {
                ItemsSource.Add(newValue);
            }
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool),
                typeof(TagsList));

        public bool IsReadOnly {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }
    }
}
