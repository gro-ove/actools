using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public enum SplitButtonMode {
        Split,
        Dropdown,
        Button
    }

    /// <summary>
    /// Implemetation of a Split Button
    /// </summary>
    [TemplatePart(Name = "PART_DropDown", Type = typeof(Button))]
    [ContentProperty("Items")]
    [DefaultProperty("Items")]
    public class SplitButton : Button {
        // AddOwner Dependency properties
        public static readonly DependencyProperty HorizontalOffsetProperty;
        public static readonly DependencyProperty IsContextMenuOpenProperty;
        public static readonly DependencyProperty ModeProperty;
        public static readonly DependencyProperty PlacementProperty;
        public static readonly DependencyProperty PlacementRectangleProperty;
        public static readonly DependencyProperty VerticalOffsetProperty;

        /// <summary>
        /// Static Constructor
        /// </summary>
        static SplitButton() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
            IsContextMenuOpenProperty = DependencyProperty.Register("IsContextMenuOpen", typeof(bool), typeof(SplitButton),
                    new FrameworkPropertyMetadata(false, OnIsContextMenuOpenChanged));
            ModeProperty = DependencyProperty.Register("Mode", typeof(SplitButtonMode), typeof(SplitButton),
                    new FrameworkPropertyMetadata(SplitButtonMode.Split));

            // AddOwner properties from the ContextMenuService class, we need callbacks from these properties
            // to update the Buttons ContextMenu properties
            PlacementProperty = ContextMenuService.PlacementProperty.AddOwner(typeof(SplitButton),
                    new FrameworkPropertyMetadata(PlacementMode.Bottom, OnPlacementChanged));
            PlacementRectangleProperty = ContextMenuService.PlacementRectangleProperty.AddOwner(typeof(SplitButton),
                    new FrameworkPropertyMetadata(Rect.Empty, OnPlacementRectangleChanged));
            HorizontalOffsetProperty = ContextMenuService.HorizontalOffsetProperty.AddOwner(typeof(SplitButton),
                    new FrameworkPropertyMetadata(0.0, OnHorizontalOffsetChanged));
            VerticalOffsetProperty = ContextMenuService.VerticalOffsetProperty.AddOwner(typeof(SplitButton),
                    new FrameworkPropertyMetadata(0.0, OnVerticalOffsetChanged));
        }

        /*
         * Overrides
         *
        */

        /// <summary>
        /// OnApplyTemplate override, set up the click event for the dropdown if present in the template
        /// </summary>
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            // set up the click event handler for the dropdown button
            if (Template.FindName("PART_DropDown", this) is ButtonBase dropDown) dropDown.Click += Dropdown_Click;
        }

        /// <summary>
        ///     Handles the Base Buttons OnClick event
        /// </summary>
        protected override void OnClick() {
            switch (Mode) {
                case SplitButtonMode.Dropdown:
                    OnDropdown();
                    break;

                default:
                    base.OnClick(); // forward on the Click event to the user
                    break;
            }
        }

        /*
         * Properties
         *
        */

        /// <summary>
        /// The Split Button’s Items property maps to the base classes ContextMenu.Items property
        /// </summary>
        public ItemCollection Items => ContextMenu.Items;

        /*
         * DependencyProperty CLR wrappers
         *
        */

        /// <summary>
        /// Gets or sets the IsContextMenuOpen property.
        /// </summary>
        public bool IsContextMenuOpen {
            get => GetValue(IsContextMenuOpenProperty) as bool? ?? default;
            set => SetValue(IsContextMenuOpenProperty, value);
        }

        /// <summary>
        /// Placement of the Context menu
        /// </summary>
        public PlacementMode Placement {
            get => GetValue(PlacementProperty) as PlacementMode? ?? default;
            set => SetValue(PlacementProperty, value);
        }

        /// <summary>
        /// PlacementRectangle of the Context menu
        /// </summary>
        public Rect PlacementRectangle {
            get => GetValue(PlacementRectangleProperty) as Rect? ?? default;
            set => SetValue(PlacementRectangleProperty, value);
        }

        /// <summary>
        /// HorizontalOffset of the Context menu
        /// </summary>
        public double HorizontalOffset {
            get => GetValue(HorizontalOffsetProperty) as double? ?? default;
            set => SetValue(HorizontalOffsetProperty, value);
        }

        /// <summary>
        /// VerticalOffset of the Context menu
        /// </summary>
        public double VerticalOffset {
            get => GetValue(VerticalOffsetProperty) as double? ?? default;
            set => SetValue(VerticalOffsetProperty, value);
        }

        /// <summary>
        /// Defines the Mode of operation of the Button
        /// </summary>
        /// <remarks>
        ///     The SplitButton two Modes are
        ///     Split (default),    - the button has two parts, a normal button and a dropdown which exposes the ContextMenu
        ///     Dropdown            - the button acts like a combobox, clicking anywhere on the button opens the Context Menu
        /// </remarks>
        public SplitButtonMode Mode {
            get => GetValue(ModeProperty) as SplitButtonMode? ?? default;
            set => SetValue(ModeProperty, value);
        }

        /*
         * DependencyPropertyChanged callbacks
         *
        */

        private static void OnIsContextMenuOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var s = (SplitButton)d;
            if (!s.ContextMenu.HasItems) return;

            var value = (bool)e.NewValue;
            if (value && !s.ContextMenu.IsOpen) s.ContextMenu.IsOpen = true;
            else if (!value && s.ContextMenu.IsOpen) s.ContextMenu.IsOpen = false;
        }

        /// <summary>
        /// Placement Property changed callback, pass the value through to the buttons context menu
        /// </summary>
        private static void OnPlacementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is SplitButton s) {
                s.ContextMenu.Placement = (PlacementMode)e.NewValue;
            }
        }

        /// <summary>
        /// PlacementRectangle Property changed callback, pass the value through to the buttons context menu
        /// </summary>
        private static void OnPlacementRectangleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is SplitButton s) {
                s.ContextMenu.PlacementRectangle = (Rect)e.NewValue;
            }
        }

        /// <summary>
        /// HorizontalOffset Property changed callback, pass the value through to the buttons context menu
        /// </summary>
        private static void OnHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is SplitButton s) {
                s.ContextMenu.HorizontalOffset = (double)e.NewValue;
            }
        }

        /// <summary>
        /// VerticalOffset Property changed callback, pass the value through to the buttons context menu
        /// </summary>
        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is SplitButton s) {
                s.ContextMenu.VerticalOffset = (double)e.NewValue;
            }
        }

        [NotNull]
        public new ContextMenu ContextMenu {
            get {
                if (base.ContextMenu == null) {
                    base.ContextMenu = new ContextMenu();
                    ContextMenu.PlacementTarget = this;
                    ContextMenu.Placement = Placement;

                    ContextMenu.Opened += ((sender, routedEventArgs) => IsContextMenuOpen = true);
                    ContextMenu.Closed += ((sender, routedEventArgs) => IsContextMenuOpen = false);
                }

                return base.ContextMenu;
            }
        }

        /*
         * Helper Methods
         *
        */

        /// <summary>
        /// Make sure the Context menu is not null
        /// </summary>
        private void OnDropdown() {
            if (ContextMenu.HasItems) {
                ContextMenu.IsOpen = !IsContextMenuOpen; // open it if closed, close it if open
            }
        }

        /*
         * Events
         *
        */

        /// <summary>
        /// Event Handler for the Drop Down Button’s Click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dropdown_Click(object sender, RoutedEventArgs e) {
            OnDropdown();
            e.Handled = true;
        }
    }
}