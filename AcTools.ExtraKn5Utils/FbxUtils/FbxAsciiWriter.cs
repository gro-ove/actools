// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.IO;
using System.Text;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    /// <summary>
    /// Writes an FBX document in a text format
    /// </summary>
    public class FbxAsciiWriter {
        private readonly Stream stream;

        /// <summary>
        /// Creates a new reader
        /// </summary>
        /// <param name="stream"></param>
        public FbxAsciiWriter(Stream stream) {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        // Adds the given node text to the string
        void BuildString(FbxNode node, LineStringBuilder sb, FbxVersion version, int indentLevel = 0) {
            // Write identifier
            sb.Indent(indentLevel);

            node.Identifier.WriteAscii(version, sb, indentLevel);

            // Write properties
            var first = true;
            for (int j = 0; j < node.Properties.Count; j++) {
                var p = node.Properties[j];
                if (p == null) {
                    continue;
                }

                if (!first) {
                    sb.Append(",");
                }

                sb.Append(" ");

                p.WriteAscii(version, sb, indentLevel);

                first = false;
            }

            // Write child nodes
            if (node.Nodes.Count > 0) {
                sb.Append(" {\n");
                foreach (var n in node.Nodes) {
                    if (n == null) {
                        continue;
                    }

                    BuildString(n, sb, version, indentLevel + 1);
                }

                sb.Indent(indentLevel);

                sb.Append("}");
            }
            sb.Append("\n");
        }

        /// <summary>
        /// Writes an FBX document to the stream
        /// </summary>
        /// <param name="document"></param>
        /// <remarks>
        /// ASCII FBX files have no header or footer, so you can call this multiple times
        /// </remarks>
        public void Write(FbxDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            var sb = new LineStringBuilder();

            // Write version header (a comment, but required for many importers)
            var vMajor = (int)document.Version / 1000;
            var vMinor = ((int)document.Version % 1000) / 100;
            var vRev = ((int)document.Version % 100) / 10;
            sb.Append($"; FBX {vMajor}.{vMinor}.{vRev} project file\n\n");

            foreach (var n in document.Nodes) {
                if (n == null) {
                    continue;
                }

                BuildString(n, sb, document.Version);
                sb.Append("\n");
            }
            var b = Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(b, 0, b.Length);
        }
    }
}