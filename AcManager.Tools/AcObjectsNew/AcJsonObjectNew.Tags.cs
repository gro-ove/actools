using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Data;
using AcManager.Tools.Data;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcJsonObjectNew {
        private TagsCollection _tags;

        [NotNull]
        public TagsCollection Tags {
            get { return _tags; }
            set {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (value == _tags || _tags != null && value.SequenceEqual(_tags)) return;

                if (_tags != null) {
                    _tags.CollectionChanged -= Tags_CollectionChanged;
                }

                _tags = value;
                OnPropertyChanged();

                _tags.CollectionChanged += Tags_CollectionChanged;

                if (Loaded) {
                    Changed = true;
                    RebuildTagsList();
                }
            }
        }

        protected virtual void RebuildTagsList() {
            GetTagsList().ReplaceEverythingBy(Manager.OfType<AcJsonObjectNew>().SelectMany(x => x.Tags));
        }

        private void Tags_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (Loaded) {
                RebuildTagsList();
                Changed = true;
            }
        }

        protected abstract AutocompleteValuesList GetTagsList();

        public ListCollectionView TagsList => GetTagsList().View;

        public ListCollectionView CountriesList => SuggestionLists.CountriesList.View;
    }
}
