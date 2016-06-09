using System;
using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using JetBrains.Annotations;

namespace AcManager.Tools.TextEditing {
    internal static class HighlighterHolder {
        [CanBeNull]
        public static IHighlightingDefinition Get(string key) {
            using (var s = Application.GetResourceStream(new Uri($"pack://application:,,,/Content Manager;component/Assets/Syntax/{key}.xml",
                    UriKind.Absolute))?.Stream)
            using (var reader = s == null ? null : new XmlTextReader(s)) {
                return s == null ? null : HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
    }
}