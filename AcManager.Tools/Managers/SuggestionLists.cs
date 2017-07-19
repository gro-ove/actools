using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;

namespace AcManager.Tools.Managers {
    public static class SuggestionLists {
        public static AutocompleteValuesList CarBrandsList { get; }
        public static ListCollectionView CarBrandsListView => CarBrandsList.View;

        public static AutocompleteValuesList CarClassesList { get; } = new AutocompleteValuesList {
            @"street",
            @"race"
        };

        public static ListCollectionView CarClassesListView => CarClassesList.View;

        public static AutocompleteValuesList CarTagsList { get; } = new AutocompleteValuesList();
        public static ListCollectionView CarTagsListView => CarTagsList.View;

        public static AutocompleteValuesList CarSkinTeamsList { get; } = new AutocompleteValuesList();
        public static ListCollectionView CarSkinTeamsListView => CarSkinTeamsList.View;

        public static AutocompleteValuesList CarSkinDriverNamesList { get; } = new AutocompleteValuesList();
        public static ListCollectionView CarSkinDriverNamesListView => CarSkinDriverNamesList.View;

        public static AutocompleteValuesList CarSkinTagsList { get; } = new AutocompleteValuesList();
        public static ListCollectionView CarSkinTagsListView => CarSkinTagsList.View;

        public static AutocompleteValuesList TrackTagsList { get; } = new AutocompleteValuesList();
        public static ListCollectionView TrackTagsListView => TrackTagsList.View;

        public static AutocompleteValuesList TrackSkinTagsList { get; } = new AutocompleteValuesList();
        public static ListCollectionView TrackSkinTagsListView => TrackSkinTagsList.View;

        public static AutocompleteValuesList TrackSkinCategoriesList { get; } = new AutocompleteValuesList();
        public static ListCollectionView TrackSkinCategoriesListView => TrackSkinCategoriesList.View;

        public static AutocompleteValuesList ShowroomTagsList { get; } = new AutocompleteValuesList();
        public static ListCollectionView ShowroomTagsListView => ShowroomTagsList.View;

        public static AutocompleteValuesList AuthorsList { get; } = new AutocompleteValuesList { @"Kunos" };
        public static ListCollectionView AuthorsListView => AuthorsList.View;

        public static AutocompleteValuesList CountriesList { get; } = new AutocompleteValuesList();
        public static ListCollectionView CountriesListView => CountriesList.View;

        public static AutocompleteValuesList CitiesList { get; } = new AutocompleteValuesList();
        public static ListCollectionView CitiesListView => CitiesList.View;

        static SuggestionLists() {
            CarBrandsList = new AutocompleteValuesList(FilesStorage.Instance.GetContentFiles(ContentCategory.BrandBadges).Select(x => x.Name));
            FilesStorage.Instance.Watcher(ContentCategory.BrandBadges).Update += OnBrandBadgesUpdate;
        }

        private static void OnBrandBadgesUpdate(object sender, EventArgs e) {
            CarBrandsList.ReplaceEverythingBy(FilesStorage.Instance.GetContentFiles(ContentCategory.BrandBadges).Select(x => x.Name));
        }

        public static void RebuildCarClassesList() {
            // CarClassesList.ReplaceEverythingBy(from c in CarsManager.Instance where c.Enabled select c.CarClass);
        }

        public static void RebuildCarTagsList() {
            CarTagsList.ReplaceEverythingBy(CarsManager.Instance.Where(x => x.Enabled).SelectMany(x => x.Tags));
        }

        public static void RebuildCarSkinDriverNamesList() {
            CarSkinDriverNamesList.ReplaceEverythingBy(CarsManager.Instance.Where(x => x.Enabled).SelectMany(
                    x => x.SkinsManager.Where(y => y.Enabled).Select(y => y.DriverName)));
        }

        public static void RebuildCarSkinTeamsList() {
            CarSkinTeamsList.ReplaceEverythingBy(CarsManager.Instance.Where(x => x.Enabled).SelectMany(
                    x => x.SkinsManager.Where(y => y.Enabled).Select(y => y.Team)));
        }

        public static void RebuildCarSkinTagsList() {
            CarSkinTagsList.ReplaceEverythingBy(CarsManager.Instance.Where(x => x.Enabled).SelectMany(
                    x => x.SkinsManager.Where(y => y.Enabled).SelectMany(y => y.Tags)));
        }

        public static void RebuildTrackTagsList() {
            // TODO: layouts
            TrackTagsList.ReplaceEverythingBy(TracksManager.Instance.Where(x => x.Enabled).SelectMany(x => x.Tags));
        }

        public static void RebuildShowroomTagsList() {
            ShowroomTagsList.ReplaceEverythingBy(ShowroomsManager.Instance.Where(x => x.Enabled).SelectMany(x => x.Tags));
        }

        private static IEnumerable<AcJsonObjectNew> JsonObjects => CarsManager.Instance.OfType<AcJsonObjectNew>()
                              .Union(TracksManager.Instance)
                              .Union(ShowroomsManager.Instance);

        private static IEnumerable<IAcObjectFullAuthorshipInformation> AuthorInformationObjects => CarsManager.Instance.OfType<IAcObjectFullAuthorshipInformation>()
                              .Union(TracksManager.Instance)
                              .Union(ShowroomsManager.Instance)
                              .Union(UserChampionshipsManager.Instance);

        public static void RebuildAuthorsList() {
            // TODO: layouts
            AuthorsList.ReplaceEverythingBy(from o in AuthorInformationObjects select o.Author);
        }

        public static void RebuildCountriesList() {
            // TODO: layouts
            CountriesList.ReplaceEverythingBy(from o in JsonObjects where o.Enabled select o.Country);
        }

        public static void RebuildCitiesList() {
            // TODO: layouts
            CitiesList.ReplaceEverythingBy(from o in TracksManager.Instance where o.Enabled select o.City);
        }
    }
}
