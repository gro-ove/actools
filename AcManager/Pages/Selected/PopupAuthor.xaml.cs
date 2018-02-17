namespace AcManager.Pages.Selected {
    public partial class PopupAuthor {
        public PopupAuthor(ISelectedAcObjectViewModel model) {
            InitializeComponent();
            MainGrid.DataContext = model;
        }
    }
}
