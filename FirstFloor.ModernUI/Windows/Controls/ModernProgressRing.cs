using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <inheritdoc />
    /// <summary>
    /// Represents a control that indicates that an operation is ongoing.
    /// </summary>
    [TemplateVisualState(GroupName = GroupActiveStates, Name = StateInactive), TemplateVisualState(GroupName = GroupActiveStates, Name = StateActive)]
    public class ModernProgressRing : Control {
        private const string GroupActiveStates = "ActiveStates";
        private const string StateInactive = "Inactive";
        private const string StateActive = "Active";

        /// <summary>
        /// Identifies the IsActive property.
        /// </summary>
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register("IsActive", typeof(bool), typeof(ModernProgressRing),
                new PropertyMetadata(false, OnIsActiveChanged));

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the <see cref="T:FirstFloor.ModernUI.Windows.Controls.ModernProgressRing" /> class.
        /// </summary>
        public ModernProgressRing() {
            DefaultStyleKey = typeof(ModernProgressRing);
        }

        private void GotoCurrentState(bool animate) {
            var state = IsActive ? StateActive : StateInactive;

            VisualStateManager.GoToState(this, state, animate);
        }

        /// <inheritdoc />
        /// <summary>
        /// When overridden in a derived class, is invoked whenever application code or internal processes call <see cref="M:System.Windows.FrameworkElement.ApplyTemplate" />.
        /// </summary>
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            GotoCurrentState(false);
        }

        private static void OnIsActiveChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernProgressRing)o).GotoCurrentState(true);
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the <see cref="ModernProgressRing"/> is showing progress.
        /// </summary>
        public bool IsActive {
            get => GetValue(IsActiveProperty) as bool? == true;
            set => SetValue(IsActiveProperty, value);
        }

        #region For glowing
        public static readonly DependencyProperty DensityMultiplierProperty = DependencyProperty.Register(nameof(DensityMultiplier), typeof(double),
                typeof(ModernProgressRing), new FrameworkPropertyMetadata(1d));

        public double DensityMultiplier {
            get => GetValue(DensityMultiplierProperty) as double? ?? 0d;
            set => SetValue(DensityMultiplierProperty, value);
        }
        #endregion
    }
}
