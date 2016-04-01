using System.Collections.ObjectModel;
using AcManager.Tools.Lists;

namespace AcManager.Tools.Managers {
    public static class SuggestionLists {
        public static readonly AutocompleteValuesList CarBrandsList = new AutocompleteValuesList();
        public static readonly AutocompleteValuesList CarClassesList = new AutocompleteValuesList();
        public static readonly AutocompleteValuesList CarTagsList = new AutocompleteValuesList();

        public static readonly AutocompleteValuesList CarSkinTeamsList = new AutocompleteValuesList();

        public static readonly AutocompleteValuesList CarSkinTagsList = new AutocompleteValuesList();
        public static readonly AutocompleteValuesList TrackTagsList = new AutocompleteValuesList();
        public static readonly AutocompleteValuesList ShowroomTagsList = new AutocompleteValuesList();

        public static readonly AutocompleteValuesList AuthorsList = new AutocompleteValuesList { "Kunos" };
        public static readonly AutocompleteValuesList CountriesList = new AutocompleteValuesList();
        public static readonly AutocompleteValuesList CitiesList = new AutocompleteValuesList();

        static SuggestionLists (){
        }
    }
}
