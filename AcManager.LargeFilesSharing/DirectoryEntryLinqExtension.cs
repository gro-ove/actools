using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace AcManager.LargeFilesSharing {
    public static class DirectoryEntryLinqExtension {
        [NotNull]
        [Pure]
        public static DirectoryEntry GetChildById([NotNull] this IEnumerable<DirectoryEntry> source, string id) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                if (Equals(i.Id, id)) return i;
                var child = i.Children.GetChildByIdOrDefault(id);
                if (child != null) return child;
            }
            throw new Exception(@"Element with given ID not found");
        }

        [CanBeNull]
        [Pure]
        public static DirectoryEntry GetChildByIdOrDefault([NotNull] this IEnumerable<DirectoryEntry> source, string id) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var i in source) {
                if (Equals(i.Id, id)) return i;
                var child = i.Children.GetChildByIdOrDefault(id);
                if (child != null) return child;
            }
            return null;
        }
    }
}