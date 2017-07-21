using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing {
    public static class Uploaders {
        private static readonly List<Type> Types = new List<Type>();

        public static void AddType<T>() where T: ILargeFileUploader {
            Types.Add(typeof(T));
        }

        public static IEnumerable<ILargeFileUploader> GetUploaders([NotNull] IStorage storage) {
            return Assembly.GetExecutingAssembly().GetTypes()
                           .Concat(Types)
                           .Where(x => !x.IsAbstract && x.GetInterface(typeof(ILargeFileUploader).FullName) != null)
                           .Select(x => (ILargeFileUploader)Activator.CreateInstance(x, storage));
        }
    }
}