using System;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit.Document;

namespace AcManager.Tools.TextEditing {
    internal class ProperUndoStrategy {
        public static void Set(TextDocument document) {
            new ProperUndoStrategy(document).Start();
        }

        private readonly TextDocument _document;

        private ProperUndoStrategy(TextDocument document) {
            _document = document;
            _document.TextChanged += OnTextChanged;
        }

        private int _update;

        private async void OnTextChanged(object sender, EventArgs e) {
            var u = ++_update;
            await Task.Delay(300);
            if (u == _update) {
                _document.UndoStack.EndUndoGroup();
                _document.UndoStack.StartUndoGroup();
            }
        }

        private void Start() {
            _document.UndoStack.StartUndoGroup();
        }
    }
}