using FirstFloor.ModernUI.Presentation;

namespace AcManager.CustomShowroom {
    public class CustomShowroomLodDefinition : Displayable {
        private string _details;

        public string Details {
            get => _details;
            set => Apply(value, ref _details);
        }

        private string _filename;

        public string Filename {
            get => _filename;
            set => Apply(value, ref _filename);
        }

        private int _order;

        public int Order {
            get => _order;
            set => Apply(value, ref _order);
        }
    }
}