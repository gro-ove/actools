using System;
using System.Collections.Generic;
using System.IO;
using StringBasedFilter;

namespace AcManager.Tools.Filters.Testers {
    public class FileTester : ITester<FileInfo>, ITesterDescription {
        public static readonly FileTester Instance = new FileTester();

        public string ParameterFromKey(string key) {
            return null;
        }

        public bool Test(FileInfo obj, string key, ITestEntry value) {
            switch (key) {
                case "date":
                    return value.Test(obj.CreationTime);
                case "age":
                    return value.Test(DateTime.Now - obj.CreationTime);
                case "size":
                    return value.Test(obj.Length);
            }

            return value.Test(obj.FullName);
        }

        public IEnumerable<KeywordDescription> GetDescriptions() {
            return new[] {
                new KeywordDescription("size", "Size", KeywordType.FileSize, KeywordPriority.Normal),
                new KeywordDescription("date", "Date", KeywordType.DateTime, KeywordPriority.Normal),
                new KeywordDescription("age", "Age", KeywordType.TimeSpan, KeywordPriority.Normal)
            };
        }
    }
}