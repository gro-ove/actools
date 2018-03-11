using System.Collections.Generic;
using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Presentation {
    public class LinkGroupCollection : ObservableCollection<LinkGroup> {
        public LinkGroupCollection() { }
        public LinkGroupCollection([NotNull] List<LinkGroup> list) : base(list) { }
        public LinkGroupCollection([NotNull] IEnumerable<LinkGroup> collection) : base(collection) { }
    }
}

