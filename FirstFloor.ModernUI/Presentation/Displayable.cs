namespace FirstFloor.ModernUI.Presentation {
    /// <summary>
    /// Provides a base implementation for objects that are displayed in the UI.
    /// </summary>
    public class Displayable : NotifyPropertyChanged {
        private string _displayName;

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        /// <value>The display name.</value>
        public virtual string DisplayName {
            get { return _displayName; }
            set {
                if (_displayName == value) return;
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
}
