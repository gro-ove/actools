using System;
using System.Drawing;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public class SelectRating : SelectCategoryBase {
        public int? Rating { get; }

        /// <summary>
        /// Create new instance.
        /// </summary>
        /// <param name="rating">If null, favourites mode.</param>
        public SelectRating(int? rating) : base(rating.HasValue ? PluralizingConverter.PluralizeExt(rating.Value, "{0} Star") : "Favourites") {
            Rating = rating;
        }

        internal override string Serialize() {
            return Rating.HasValue ? Rating.ToInvariantString() : @"-";
        }

        [CanBeNull]
        internal static SelectRating Deserialize(string data) {
            return data == @"-" ? new SelectRating(null) : new SelectRating(FlexibleParser.TryParseInt(data));
        }
    }
}