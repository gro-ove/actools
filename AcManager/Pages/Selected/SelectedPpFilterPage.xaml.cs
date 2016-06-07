using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public partial class SelectedPpFilterPage : ILoadableContent, IParametrizedUriContent {
        public class SelectedPpFilterPageViewModel : SelectedAcObjectViewModel<PpFilterObject> {
            public SelectedPpFilterPageViewModel([NotNull] PpFilterObject acObject) : base(acObject) { }

            public PpFiltersManager Manager => PpFiltersManager.Instance;
        }

        private string _id;

        void IParametrizedUriContent.OnUri(Uri uri) {
            _id = uri.GetQueryParam("Id");
            if (_id == null) {
                throw new Exception("ID is missing");
            }
        }

        private PpFilterObject _object;

        async Task ILoadableContent.LoadAsync(CancellationToken cancellationToken) {
            _object = await PpFiltersManager.Instance.GetByIdAsync(_id);
            _object?.PrepareForEditing();
        }

        void ILoadableContent.Load() {
            _object = PpFiltersManager.Instance.GetById(_id);
            _object?.PrepareForEditing();
        }

        void ILoadableContent.Initialize() {
            if (_object == null) throw new ArgumentException("Can’t find object with provided ID");

            InitializeAcObjectPage(new SelectedPpFilterPageViewModel(_object));
            /*InputBindings.AddRange(new[] {
                new InputBinding(Model.CreateNewFontCommand, new KeyGesture(Key.N, ModifierKeys.Control)),
                new InputBinding(Model.UsingsRescanCommand, new KeyGesture(Key.U, ModifierKeys.Control)),
                new InputBinding(Model.DisableUnusedCommand, new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift))
            });*/
            InitializeComponent();

            TextEditor.SyntaxHighlighting = HighlighterHolder.Get("Ini");
            TextEditor.Options = new TextEditorOptions {
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

            UpdateDocument();
            _object.PropertyChanged += SelectedObject_PropertyChanged;
            // IniFoldingStrategy.Set(TextEditor);
        }

        private void UpdateDocument() {
            try {
                _ignore = true;
                TextEditor.Document = new TextDocument(_object.Content);
                // ProperUndoStrategy.Set(TextEditor.Document);
            } finally {
                _ignore = false;
            }
        }

        private void SelectedObject_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (_ignore) return;
            if (e.PropertyName == nameof(_object.Content)) {
                UpdateDocument();
            }
        }

        private bool _ignore;

        private void TextEditor_OnTextChanged(object sender, EventArgs e) {
            if (_ignore) return;
            try {
                _ignore = true;
                _object.Content = TextEditor.Text;
                _object.SetChanged(true);
            } finally {
                _ignore = false;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            _object.PropertyChanged -= SelectedObject_PropertyChanged;
        }
    }

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

    internal static class HighlighterHolder {
        [CanBeNull]
        public static IHighlightingDefinition Get(string key) {
            using (var s = Application.GetResourceStream(new Uri($"pack://application:,,,/Content Manager;component/Assets/Syntax/{key}.xml",
                    UriKind.Absolute))?.Stream)
            using (var reader = s == null ? null : new XmlTextReader(s)) {
                if (s == null) return null;
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
    }
}
