using System.ComponentModel;

namespace AcTools.Utils.Helpers {
    public class JObjectRestorationScheme {
        public class Field {
            public readonly string Name, ParentName;
            public readonly FieldType Type;

            public Field([Localizable(false)] string name, FieldType type) {
                Name = name;
                Type = type;
            }

            public Field([Localizable(false)] string name, [Localizable(false)] string parentName, FieldType type) {
                Name = name;
                ParentName = parentName;
                Type = type;
            }

            public bool IsMultiline => Type == FieldType.StringMultiline || Type == FieldType.StringsArray ||
                    Type == FieldType.PairsArray;
        }

        public enum FieldType {
            String,
            NonNullString,
            StringMultiline,
            Number,
            Boolean,
            StringsArray,
            PairsArray
        }

        public readonly Field[] Fields;

        public JObjectRestorationScheme(params Field[] fields) {
            Fields = fields;
        }
    }
}