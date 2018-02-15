namespace FirstFloor.ModernUI.Presentation {
    public class Displayable : NotifyPropertyChanged {
        private string _displayName;

        public virtual string DisplayName {
            get => _displayName;
            set => Apply(value, ref _displayName);
        }
    }
}
