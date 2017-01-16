using AcManager.Tools.Data;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.SelectionLists {
    public sealed class SelectDlc : SelectCategoryBase {
        [NotNull]
        public KunosDlcInformation Information { get; }

        public SelectDlc([NotNull] KunosDlcInformation information) : base(information.DisplayName) {
            Information = information;
        }

        public override bool IsSameAs(SelectCategoryBase category) {
            return (category as SelectDlc)?.Information.Id == Information.Id;
        }
        
        internal override string Serialize() {
            return JsonConvert.SerializeObject(Information);
        }

        [CanBeNull]
        internal static SelectDlc Deserialize(string data) {
            return new SelectDlc(JsonConvert.DeserializeObject<KunosDlcInformation>(data));
        }
    }
}