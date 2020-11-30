using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.Objects;

namespace AcManager.Pages.Selected {
    public class RoundsListDataTemplateSelector : DataTemplateSelector {
        public DataTemplate RoundDataTemplate { get; set; }

        public DataTemplate NewRoundDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is UserChampionshipRoundExtended ? RoundDataTemplate : NewRoundDataTemplate;
        }
    }
}