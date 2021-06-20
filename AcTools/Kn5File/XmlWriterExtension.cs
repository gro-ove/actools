using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AcTools.Numerics;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Kn5File {
    public static class XmlWriterExtension {
        private static readonly Regex InvalidXmlChars = new Regex(
                @"(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uD800-\uDBFF](?![\uDC00-\uDFFF])|[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F\uFEFF\uFFFE\uFFFF]",
                RegexOptions.Compiled);

        public static string RemoveInvalidXmlChars(string text) {
            return string.IsNullOrEmpty(text) ? "" : InvalidXmlChars.Replace(text, "");
        }

        public static void WriteAttributeStringSafe(this XmlWriter xml, string key, string value) {
            xml.WriteAttributeString(key, RemoveInvalidXmlChars(value));
        }

        public static void WriteAttributeString(this XmlWriter xml, string key, int value) {
            xml.WriteAttributeString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttributeString(this XmlWriter xml, string key, float value) {
            xml.WriteAttributeString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteAttributeString(this XmlWriter xml, string key, double value) {
            xml.WriteAttributeString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteString(this XmlWriter xml, int value) {
            xml.WriteString(value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteString(this XmlWriter xml, float value) {
            xml.WriteString(value.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteString(this XmlWriter xml, double value) {
            xml.WriteString(value.ToString(CultureInfo.InvariantCulture));
        }

        public static string MatrixToCollada(float[] matrix) {
            var sb = new StringBuilder();
            for (var i = 0; i < 4; i++) {
                for (var j = 0; j < 4; j++) {
                    if (i > 0 || j > 0) {
                        sb.Append(" ");
                    }

                    sb.Append(matrix[j * 4 + i].ToString(CultureInfo.InvariantCulture));
                }
            }

            return sb.ToString();
        }

        public static string MatrixToCollada(Mat4x4 matrix) {
            var sb = new StringBuilder();
            sb.Append(matrix.M11);
            sb.Append(' ');
            sb.Append(matrix.M21);
            sb.Append(' ');
            sb.Append(matrix.M31);
            sb.Append(' ');
            sb.Append(matrix.M41);
            sb.Append(' ');
            sb.Append(matrix.M12);
            sb.Append(' ');
            sb.Append(matrix.M22);
            sb.Append(' ');
            sb.Append(matrix.M32);
            sb.Append(' ');
            sb.Append(matrix.M42);
            sb.Append(' ');
            sb.Append(matrix.M13);
            sb.Append(' ');
            sb.Append(matrix.M23);
            sb.Append(' ');
            sb.Append(matrix.M33);
            sb.Append(' ');
            sb.Append(matrix.M43);
            sb.Append(' ');
            sb.Append(matrix.M14);
            sb.Append(' ');
            sb.Append(matrix.M24);
            sb.Append(' ');
            sb.Append(matrix.M34);
            sb.Append(' ');
            sb.Append(matrix.M44);
            return sb.ToString();
        }

        public static void WriteMatrixAsString(this XmlWriter xml, float[] matrix) {
            xml.WriteString(MatrixToCollada(matrix));
        }

        public static void WriteElementStringSafe(this XmlWriter xml, string key, string value) {
            xml.WriteElementString(key, RemoveInvalidXmlChars(value));
        }

        public static void WriteElement(this XmlWriter xml, [NotNull] string localName, [NotNull] params object[] attributes) {
            xml.WriteStartElement(localName);
            int i;
            for (i = 0; i < attributes.Length - 1; i += 2) {
                xml.WriteAttributeStringSafe(attributes[i].ToInvariantString(), attributes[i + 1].ToInvariantString());
            }
            if (i < attributes.Length) {
                xml.WriteString(attributes[i].ToInvariantString());
            }
            xml.WriteEndElement();
        }
    }
}