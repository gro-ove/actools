using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public enum AvalonEditMode {
        Text, Ini
    }

    public class AvalonExtension {
        private class LinesArrayToTextConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return (value as IEnumerable<string>)?.JoinToString('\n');
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                return value?.ToString().Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            }
        }

        public static IValueConverter LinesArrayToTextConverter = new LinesArrayToTextConverterInner();

        public static bool GetInitialized(DependencyObject obj) {
            return obj.GetValue(InitializedProperty) as bool? == true;
        }

        public static void SetInitialized(DependencyObject obj, bool value) {
            obj.SetValue(InitializedProperty, value);
        }

        public static readonly DependencyProperty InitializedProperty = DependencyProperty.RegisterAttached("Initialized", typeof(bool),
                typeof(AvalonExtension), new UIPropertyMetadata(OnInitializedChanged));

        private static bool _isBusy;

        private static void OnInitializedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is TextEditor element) || !(e.NewValue is bool)) return;
            var newValue = (bool)e.NewValue;

            if (newValue) {
                element.Options = new TextEditorOptions {
                    AllowScrollBelowDocument = true,
                    CutCopyWholeLine = true,
                    EnableEmailHyperlinks = true,
                    EnableHyperlinks = true,
                    EnableRectangularSelection = true,
                    EnableTextDragDrop = true,
                    EnableVirtualSpace = false,
                    HideCursorWhileTyping = false,
                    HighlightCurrentLine = false,
                    IndentationSize = 2,
                    RequireControlModifierForHyperlinkClick = true,
                    WordWrapIndentation = 20d,
                    ConvertTabsToSpaces = false
                };

                element.TextChanged += OnTextChanged;
            } else {
                element.Options = new TextEditorOptions();
                element.TextChanged -= OnTextChanged;
            }
        }

        private static void OnTextChanged(object sender, EventArgs eventArgs) {
            if (_isBusy) return;
            try {
                _isBusy = true;
                var element = (TextEditor)sender;
                SetText(element, element.Text);
            } finally {
                _isBusy = false;
            }
        }

        public static string GetText(DependencyObject obj) {
            return (string)obj.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject obj, string value) {
            obj.SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(AvalonExtension),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged, null, true,
                        UpdateSourceTrigger.PropertyChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (_isBusy) return;

            if (!(d is TextEditor element) || !(e.NewValue is string)) return;
            SetInitialized(element, true);
            var newValue = (string)e.NewValue;

            try {
                _isBusy = true;
                if (element.Document != null) {
                    element.Document.Text = newValue;
                    element.Document.UndoStack.ClearAll();
                } else {
                    element.Document = newValue == null ? new TextDocument() : new TextDocument(newValue);
                }
            } finally {
                _isBusy = false;
            }
        }

        public static AvalonEditMode GetMode(DependencyObject obj) {
            return obj.GetValue(ModeProperty) as AvalonEditMode? ?? default;
        }

        public static void SetMode(DependencyObject obj, AvalonEditMode value) {
            obj.SetValue(ModeProperty, value);
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached("Mode", typeof(AvalonEditMode),
                typeof(AvalonExtension), new UIPropertyMetadata(AvalonEditMode.Text, OnModeChanged));

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!(d is TextEditor element) || !(e.NewValue is AvalonEditMode)) return;

            // TODO: Live changing
            element.Loaded += OnTextEditorLoaded;
        }

        private static void OnTextEditorLoaded(object sender, RoutedEventArgs routedEventArgs) {
            var element = (TextEditor)sender;
            switch (GetMode(element)) {
                case AvalonEditMode.Ini:
                    element.SyntaxHighlighting = HighlighterHolder.Get(element.TryFindResource("TextEditorSyntaxIni") as string);
                    // IniFoldingStrategy.Set(TextEditor);
                    break;
            }
        }
    }

    internal class HighlightCurrentLineBackgroundRenderer : IBackgroundRenderer {
        private readonly TextEditor _editor;

        public HighlightCurrentLineBackgroundRenderer(TextEditor editor) {
            _editor = editor;
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext) {
            if (_editor.Document == null) return;

            textView.EnsureVisualLines();
            var currentLine = _editor.Document.GetLineByOffset(_editor.CaretOffset);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, currentLine)) {
                drawingContext.DrawRectangle(
                        new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0xFF)), null,
                        new Rect(rect.Location, new Size(textView.ActualWidth - 32, rect.Height)));
            }
        }
    }

    public static class HighlighterHolder {
        [CanBeNull]
        public static IHighlightingDefinition Get(string data) {
            if (data == null) return null;

            try {
                using (var stringReader = new StringReader(data))
                using (var reader = new XmlTextReader(stringReader)) {
                    return HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
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