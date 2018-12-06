using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AcManager.Tools.Helpers.Tyres {
    public class TyresPlace : Border {
        static TyresPlace() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TyresPlace), new FrameworkPropertyMetadata(typeof(TyresPlace)));
        }

        public static readonly DependencyProperty HighlightColorProperty = DependencyProperty.Register(nameof(HighlightColor), typeof(Brush),
                typeof(TyresPlace));

        public Brush HighlightColor {
            get => (Brush)GetValue(HighlightColorProperty);
            set => SetValue(HighlightColorProperty, value);
        }

        public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(TyresAppropriateLevel),
                typeof(TyresPlace));

        public TyresAppropriateLevel Level {
            get => GetValue(LevelProperty) as TyresAppropriateLevel? ?? TyresAppropriateLevel.F;
            set => SetValue(LevelProperty, value);
        }
    }
}