using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Tools.Data {
    public sealed class KunosDlcInformation : Displayable {
        public int Id { get; }

        public string ShortName { get; }

        public string[] Cars { get; }

        public string[] Tracks { get; }

        [JsonConstructor]
        public KunosDlcInformation(int id, string shortName, string name, string[] cars, string[] tracks) {
            Id = id;
            ShortName = shortName;
            DisplayName = name;
            Cars = cars ?? new string[0];
            Tracks = tracks ?? new string[0];
        }

        public string Url => $"http://store.steampowered.com/app/{Id}/";
    }
}