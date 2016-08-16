using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Presentation;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Attached;
using FirstFloor.ModernUI.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class TagsList : Control {
        private RelayCommand _closeCommand;

        public RelayCommand CloseCommand => _closeCommand ?? (_closeCommand = new RelayCommand(o => {
            ItemsSource.Remove(o as string);
        }));

        private RelayCommand _changeCommand;

        public RelayCommand ChangeCommand => _changeCommand ?? (_changeCommand = new RelayCommand(o => {
            var target = o as TextBox;
            var originalValue = target?.DataContext as string;
            if (originalValue == null) return;

            var newValue = target.Text.Trim();
            if (Equals(originalValue, newValue)) return;

            if (string.IsNullOrEmpty(newValue)) {
                ItemsSource.Remove(originalValue);
            } else {
                var index = ItemsSource.IndexOf(originalValue);
                if (index == -1) return;

                ItemsSource[index] = target.Text;
            }
        }));

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(ObservableCollection<string>),
            typeof(TagsList));

        public static readonly DependencyProperty SuggestionsSourceProperty = DependencyProperty.Register("SuggestionsSource", typeof(CollectionView),
            typeof(TagsList));

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
        
        private ComboBox _previousTextBox;

        public override void OnApplyTemplate() {
            if (_previousTextBox != null) {
                _previousTextBox.PreviewKeyDown -= TextBox_KeyDown;
                _previousTextBox.LostFocus -= TextBox_LostFocus;
                _previousTextBox = null;
            }

            base.OnApplyTemplate();

            var textBox = Template.FindName("PART_NewTagTextBox", this) as ComboBox;
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

        private static bool IsCaretAtFront(DependencyObject d) {
            var t = d.FindVisualChild<TextBox>();
            return t?.SelectionStart == 0 && t.SelectionLength == 0;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key != Key.Back && e.Key != Key.Enter && e.Key != Key.Tab &&
                    e.Key != Key.Escape) return;

            var textBox = sender as ComboBox;
            if (textBox == null) return;

            if (e.Key != Key.Back) {
                if (string.IsNullOrWhiteSpace(textBox.Text)) return;

                AddNewTag(textBox.Text.Trim());
                textBox.Text = "";

                if (e.Key == Key.Tab) {
                    FocusAdvancement.MoveFocus(textBox);
                } else if (e.Key == Key.Escape) {
                    FocusAdvancement.RemoveFocus(textBox);
                }
            } else if (string.IsNullOrWhiteSpace(textBox.Text) || IsCaretAtFront(textBox)) {
                FocusAdvancement.MoveFocus(textBox, FocusNavigationDirection.Previous);
            } else {
                return;
            }

            e.Handled = true;
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

    public class TagsListDataTemplateSelector : DataTemplateSelector {
        public DataTemplate TagDataTemplate { get; set; }

        public DataTemplate NewTagDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is string ? TagDataTemplate : NewTagDataTemplate;
        }
    }
}
