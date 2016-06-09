using System;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace AcManager.Tools.TextEditing {
    internal static class TextEditorExtension {
        public static void SetAsIniEditor(this TextEditor editor, Action<string> changedHandler) {
            editor.SyntaxHighlighting = HighlighterHolder.Get("Ini");
            editor.Options = new TextEditorOptions {
                AllowScrollBelowDocument = true,
                CutCopyWholeLine = true,
                EnableEmailHyperlinks = true,
                EnableHyperlinks = true,
                EnableRectangularSelection = true,
                EnableTextDragDrop = true,
                EnableVirtualSpace = false,
                HideCursorWhileTyping = false,
                HighlightCurrentLine = true,
                IndentationSize = 4,
                RequireControlModifierForHyperlinkClick = true,
                WordWrapIndentation = 20d,
                ConvertTabsToSpaces = false
            };
            // IniFoldingStrategy.Set(TextEditor);

            editor.TextChanged += (sender, args) => {
                if (_isBusy) return;
                try {
                    _isBusy = true;
                    changedHandler?.Invoke(editor.Text);
                } finally {
                    _isBusy = false;
                }
            };
        }

        public static void SetDocument(this TextEditor editor, string content) {
            try {
                _isBusy = true;
                editor.Document = new TextDocument(content);
                // ProperUndoStrategy.Set(TextEditor.Document);
            } finally {
                _isBusy = false;
            }
        }

        private static bool _isBusy;

        public static bool IsBusy(this TextEditor editor) {
            return _isBusy;
        }
    }
}