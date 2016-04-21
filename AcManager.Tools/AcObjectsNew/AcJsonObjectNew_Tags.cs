using System;
using System.Collections.Specialized;
using System.ComponentModel;
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

                Changed = true;
            }
        }

        private void Tags_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            foreach (var tag in _tags) {
                GetTagsList().AddUnique(tag);
            }

            Changed = true;
        }

        protected abstract AutocompleteValuesList GetTagsList();

        private static ListCollectionView _tagsListView;

        public ListCollectionView TagsList {
            get {
                if (_tagsListView != null) return _tagsListView;

                _tagsListView = (ListCollectionView)CollectionViewSource.GetDefaultView(GetTagsList());
                _tagsListView.SortDescriptions.Add(new SortDescription());
                return _tagsListView;
            }
        }

        private static ListCollectionView _countiesListView;

        public ListCollectionView CountriesList {
            get {
                if (_countiesListView != null) return _countiesListView;

                _countiesListView = (ListCollectionView)CollectionViewSource.GetDefaultView(SuggestionLists.CountriesList);
                _countiesListView.SortDescriptions.Add(new SortDescription());
                return _countiesListView;
            }
        }
    }
}
