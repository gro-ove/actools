using System;

namespace AcManager.Pages.SelectionLists {
    public class SelectCategory : SelectCategoryBase {
        public SelectCategoryDescription Description { get; }

        public string Group { get; }
        public double Order { get; }

        public SelectCategory(SelectCategoryDescription description) : base(description.Name ?? @"?") {
            Description = description;
            Group = description.Source == @"List" ? "Main" : description.Source;
            Order = description.Order;
        }

        internal override string Serialize() {
            throw new NotSupportedException();
        }
    }
}