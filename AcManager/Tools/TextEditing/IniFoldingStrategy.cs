using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace AcManager.Tools.TextEditing {
    internal class IniFoldingStrategy {
        public static void Set(TextEditor editor) {
            new IniFoldingStrategy(editor).Update();
        }

        private readonly TextEditor _editor;
        private readonly FoldingManager _foldingManager;

        private IniFoldingStrategy(TextEditor editor) {
            _foldingManager = FoldingManager.Install(_editor.TextArea);
            _editor = editor;
            _editor.TextChanged += OnTextChanged;
        }

        private int _update;

        private async void OnTextChanged(object sender, EventArgs e) {
            var u = ++_update;
            await Task.Delay(300);
            if (u == _update) {
                Update();
            }
        }

        private void Update() {
            UpdateFoldings(_foldingManager, _editor.Document);
        }

        public void UpdateFoldings(FoldingManager manager, TextDocument document) {
            manager.UpdateFoldings(CreateNewFoldings(document), 0);
        }

        private static readonly Regex SectionRegex = new Regex(@"^\s*\[\w+\]", RegexOptions.Compiled | RegexOptions.Multiline);

        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document) {
            var prev = -1;
            foreach (Match m in SectionRegex.Matches(document.Text)) {
                if (prev != -1) {
                    yield return new NewFolding(prev, m.Index - 1);
                }
                prev = m.Index + m.Length;
            }

            yield return new NewFolding(prev, document.Text.Length) { DefaultClosed = true };
        }
    }
}