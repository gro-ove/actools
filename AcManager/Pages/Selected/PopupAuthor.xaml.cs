namespace AcManager.Pages.Selected {
    public partial class PopupAuthor {
        public PopupAuthor(ISelectedAcObjectViewModel model) {
            DataContext = model;
            InitializeComponent();
        }
    }
}
