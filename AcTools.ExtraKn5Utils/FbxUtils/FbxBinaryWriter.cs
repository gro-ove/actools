// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System;
using System.IO;
using System.Text;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    /// <summary>
    /// Writes an FBX document to a binary stream
    /// </summary>
    public class FbxBinaryWriter : FbxBinary {
        private readonly Stream output;
        private readonly MemoryStream memory;
        private readonly BinaryWriter stream;

        /// <summary>
        /// The minimum size of an array in bytes before it is compressed
        /// </summary>
        public int CompressionThreshold { get; set; } = 1024;

        /// <summary>
        /// Creates a new writer
        /// </summary>
        /// <param name="stream"></param>
        public FbxBinaryWriter(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            output = stream;
            // Wrap in a memory stream to guarantee seeking
            memory = new MemoryStream();
            this.stream = new BinaryWriter(memory, Encoding.ASCII);
        }

        void WriteProperty(FbxVersion version, Token obj) {
            if (obj == null) {
                return;
            }

            obj.WriteBinary(version, stream);
        }

        // Data for a null node
        static readonly byte[] nullData = new byte[13];
        static readonly byte[] nullData7500 = new byte[25];

        // Writes a single document to the buffer
        void WriteNode(FbxDocument document, FbxNode node) {
            if (node == null) {
                var data = document.Version >= FbxVersion.v7_5 ? nullData7500 : nullData;
                stream.BaseStream.Write(data, 0, data.Length);
            } else {
                // Header
                var endOffsetPos = stream.BaseStream.Position;
                long propertyLengthPos;
                if (document.Version >= FbxVersion.v7_5) {
                    stream.Write((long)0); // End offset placeholder
                    stream.Write((long)node.Properties.Count);
                    propertyLengthPos = stream.BaseStream.Position;
                    stream.Write((long)0); // Property length placeholder
                } else {
                    stream.Write(0); // End offset placeholder
                    stream.Write(node.Properties.Count);
                    propertyLengthPos = stream.BaseStream.Position;
                    stream.Write(0); // Property length placeholder
                }

                node.Identifier.WriteBinary(document.Version, stream);

                // Write properties and length
                var propertyBegin = stream.BaseStream.Position;
                for (int i = 0; i < node.Properties.Count; i++) {
                    WriteProperty(document.Version, node.Properties[i]);
                }
                var propertyEnd = stream.BaseStream.Position;
                stream.BaseStream.Position = propertyLengthPos;
                if (document.Version >= FbxVersion.v7_5) {
                    stream.Write(propertyEnd - propertyBegin);
                } else {
                    stream.Write((int)(propertyEnd - propertyBegin));
                }

                stream.BaseStream.Position = propertyEnd;

                // Write child nodes
                if (node.Nodes.Count > 0) {
                    foreach (var n in node.Nodes) {
                        if (n == null) {
                            continue;
                        }

                        WriteNode(document, n);
                    }
                    WriteNode(document, null);
                }

                // Write end offset
                var dataEnd = stream.BaseStream.Position;
                stream.BaseStream.Position = endOffsetPos;
                if (document.Version >= FbxVersion.v7_5) {
                    stream.Write(dataEnd);
                } else {
                    stream.Write((int)dataEnd);
                }

                stream.BaseStream.Position = dataEnd;
            }
        }

        /// <summary>
        /// Writes an FBX file to the output
        /// </summary>
        /// <param name="document"></param>
        public void Write(FbxDocument document) {
            stream.BaseStream.Position = 0;
            WriteHeader(stream.BaseStream);
            stream.Write((int)document.Version);
            // TODO: Do we write a top level node or not? Maybe check the version?
            foreach (var node in document.Nodes) {
                WriteNode(document, node);
            }

            WriteNode(document, null);
            stream.Write(GenerateFooterCode(document));
            WriteFooter(stream, (int)document.Version);
            output.Write(memory.ToArray(), 0, (int)memory.Position);
        }
    }
}