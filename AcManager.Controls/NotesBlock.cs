using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Tools;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Controls {
    public class NotesBlock : Control {
        static NotesBlock() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NotesBlock), new FrameworkPropertyMetadata(typeof(NotesBlock)));
        }

        public static readonly DependencyProperty AcObjectProperty = DependencyProperty.Register(nameof(AcObject), typeof(AcObjectNew),
                typeof(NotesBlock));

        public AcObjectNew AcObject {
            get => (AcObjectNew)GetValue(AcObjectProperty);
            set => SetValue(AcObjectProperty, value);
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            if (GetTemplateChild(@"PART_EditButton") is Button button) {
                button.Click += OnEditClick;
            }
        }

        private void Edit() {
            var obj = AcObject;
            if (obj != null) {
                obj.Notes = Prompt.Show(null, $"Notes for {obj.DisplayName}", obj.Notes, ToolsStrings.Common_None, multiline: true,
                        comment: "Notes are for personal, saved on this computer information. You can sort by notes as well, with “notes:word” query.")
                        ?? obj.Notes;
            }
        }

        private void OnEditClick(object sender, RoutedEventArgs args) {
            args.Handled = true;
            Edit();
        }

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseDown(e);
            if (!e.Handled && e.ChangedButton == MouseButton.Left) {
                e.Handled = true;
                Edit();
            }
        }
    }
}