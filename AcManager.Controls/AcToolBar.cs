using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace AcManager.Controls {
    [ContentProperty(nameof(ToolBars))]
    public class AcToolBar : Control {
        static AcToolBar() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcToolBar), new FrameworkPropertyMetadata(typeof(AcToolBar)));
        }

        public AcToolBar() {
            SetCurrentValue(ToolBarsProperty, new Collection<ToolBar>());
        }

        private ToolBarTray ToolBarTray => GetTemplateChild(@"PART_ToolBarTray") as ToolBarTray;

        public static readonly DependencyProperty ToolBarsProperty = DependencyProperty.Register(nameof(ToolBars), typeof(Collection<ToolBar>),
                                                                                             typeof(AcToolBar));

        public Collection<ToolBar> ToolBars {
            get => (Collection<ToolBar>)GetValue(ToolBarsProperty);
            set => SetValue(ToolBarsProperty, value);
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            var tray = ToolBarTray;
            if (tray == null) return;
            foreach (var toolBar in ToolBars) {
                tray.ToolBars.Add(toolBar);
            }
        }

        public static readonly DependencyProperty FitWidthProperty = DependencyProperty.Register(nameof(FitWidth), typeof(bool),
                typeof(AcToolBar), new PropertyMetadata(false));

        public bool FitWidth {
            get => GetValue(FitWidthProperty) as bool? == true;
            set => SetValue(FitWidthProperty, value);
        }

        #region Toggling attributes
        public static readonly DependencyProperty IsTogglableProperty = DependencyProperty.Register("IsTogglable", typeof(bool),
            typeof(AcToolBar), new PropertyMetadata());

        public bool IsTogglable {
            get => GetValue(IsTogglableProperty) as bool? == true;
            set => SetValue(IsTogglableProperty, value);
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register("IsActive", typeof(bool),
            typeof(AcToolBar), new PropertyMetadata(false));

        public bool IsActive {
            get => GetValue(IsActiveProperty) as bool? == true;
            set => SetValue(IsActiveProperty, value);
        }
        #endregion
    }
}
