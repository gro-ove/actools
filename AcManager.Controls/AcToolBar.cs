using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace AcManager.Controls {
    [ContentProperty("ToolBars")]
    public class AcToolBar : Control {
        static AcToolBar() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AcToolBar), new FrameworkPropertyMetadata(typeof(AcToolBar)));
        }

        public AcToolBar() {
            SetCurrentValue(ToolBarsProperty, new Collection<ToolBar>());
        }

        private ToolBarTray ToolBarTray => GetTemplateChild("PART_ToolBarTray") as ToolBarTray;

        public static readonly DependencyProperty ToolBarsProperty = DependencyProperty.Register(nameof(ToolBars), typeof(Collection<ToolBar>),
                                                                                             typeof(AcToolBar));

        public Collection<ToolBar> ToolBars {
            get { return (Collection<ToolBar>)GetValue(ToolBarsProperty); }
            set { SetValue(ToolBarsProperty, value); }
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (ToolBarTray == null) return;
            foreach (var toolBar in ToolBars) {
                ToolBarTray.ToolBars.Add(toolBar);
            }
        }

        #region Toggling attributes
        public static readonly DependencyProperty IsTogglableProperty = DependencyProperty.Register("IsTogglable", typeof(bool),
            typeof(AcToolBar), new PropertyMetadata());
        
        public bool IsTogglable {
            get { return (bool)GetValue(IsTogglableProperty); }
            set { SetValue(IsTogglableProperty, value); }
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register("IsActive", typeof(bool),
            typeof(AcToolBar), new PropertyMetadata(false));

        public bool IsActive {
            get { return (bool)GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }
        #endregion
    }
}
