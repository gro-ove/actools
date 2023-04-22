using System.Linq;
using System.Windows.Markup;

namespace FirstFloor.ModernUI.Presentation {
    [ContentProperty(nameof(Children))]
    public class LinksList : Link, IAddChild {
        public BetterObservableCollection<Link> Children { get; } = new BetterObservableCollection<Link>();
        
        public void AddChild(object value) {
            if (value is Link link) {
                Children.Add(link);
            }
        }

        public void AddText(string text) {
            throw new System.NotImplementedException();
        }

        private Link _selectedLink;

        public Link SelectedLink {
            get => _selectedLink ?? (_selectedLink = Children.FirstOrDefault());
            set => Apply(value, ref _selectedLink);
        }
    }
}