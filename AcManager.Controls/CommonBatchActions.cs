using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Controls {
    public static class CommonBatchActions {
        #region Rating
        public class BatchAction_SetRating : BatchAction<AcObjectNew> {
            public static readonly BatchAction_SetRating Instance = new BatchAction_SetRating();
            public BatchAction_SetRating() : base("Set Rating", "Set rating of several objects at once", "Rating", "Batch.SetRating") { }

            private double _rating = ValuesStorage.GetDouble("_ba.setRating.value", 4d);
            public double Rating {
                get => _rating;
                set {
                    if (Equals(value, _rating)) return;
                    _rating = value;
                    ValuesStorage.Set("_ba.setRating.value", value);
                    OnPropertyChanged();
                }
            }

            private bool _removeRating = ValuesStorage.GetBool("_ba.setRating.remove", true);
            public bool RemoveRating {
                get => _removeRating;
                set {
                    if (Equals(value, _removeRating)) return;
                    _removeRating = value;
                    ValuesStorage.Set("_ba.setRating.remove", value);
                    OnPropertyChanged();
                }
            }

            protected override void ApplyOverride(AcObjectNew obj) {
                if (RemoveRating) {
                    obj.Rating = null;
                } else {
                    obj.Rating = Rating;
                }
            }
        }

        public class BatchAction_AddToFavourites : BatchAction<AcObjectNew> {
            public static readonly BatchAction_AddToFavourites Instance = new BatchAction_AddToFavourites();
            public BatchAction_AddToFavourites() : base("Add To Favourites", "Add several objects at once", "Rating", null) { }

            protected override void ApplyOverride(AcObjectNew obj) {
                obj.IsFavourite = true;
            }
        }

        public class BatchAction_RemoveFromFavourites : BatchAction<AcObjectNew> {
            public static readonly BatchAction_RemoveFromFavourites Instance = new BatchAction_RemoveFromFavourites();
            public BatchAction_RemoveFromFavourites() : base("Remove From Favourites", "Remove several objects at once", "Rating", null) { }

            protected override void ApplyOverride(AcObjectNew obj) {
                obj.IsFavourite = true;
            }
        }
        #endregion

        #region Miscellaneous
        public class BatchAction_Pack : BatchAction<AcCommonObject> {
            public static readonly BatchAction_Pack Instance = new BatchAction_Pack();
            public BatchAction_Pack() : base("Pack To Archive", "Pack to a ZIP-archive", null, null) { }

            public override Task ApplyAsync(IList list, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {


                return base.ApplyAsync(list, progress, cancellation);
            }

            protected override void ApplyOverride(AcCommonObject obj) {
                obj.PackCommand
                obj.IsFavourite = true;
            }
        }
        #endregion
    }
}