using System;
using System.Collections.Generic;
using System.IO;

namespace AcTools.Utils {
    public class TagFile : Dictionary<string, string> {
        private TagFile(string data) {
            try {
                var startIndex = -1;
                var startKey = -1;
                string currentKey = null;
                for (var i = 0; i < data.Length; i++) {
                    switch (data[i]) {
                        case '[':
                            startKey = i + 1;
                            break;
                        case '\n':
                            startKey = -1;
                            break;
                        case ']':
                            if (startKey != -1) {
                                if (startIndex != -1 && currentKey != null) {
                                    this[currentKey] = data.Substring(startIndex, startKey - startIndex - 1).Trim();
                                }

                                currentKey = data.Substring(startKey, i - startKey);
                                startIndex = i + 1;
                                startKey = -1;
                            }
                            break;
                    }
                }

                if (startIndex != -1 && currentKey != null) {
                    this[currentKey] = data.Substring(startIndex, data.Length - startIndex);
                }
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            }
        }

        public static TagFile FromFile(string filename) {
            return new TagFile(File.ReadAllText(filename));
        }

        public static TagFile FromData(string data) {
            return new TagFile(data);
        }
    }
}