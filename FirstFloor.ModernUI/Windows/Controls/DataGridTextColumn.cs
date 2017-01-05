using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls
{
    /// <summary>
    /// A DataGrid text column using default Modern UI element styles.
    /// </summary>
    public class DataGridTextColumn
        : System.Windows.Controls.DataGridTextColumn
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridTextColumn"/> class.
        /// </summary>
        public DataGridTextColumn()
        {
            var app = Application.Current;
            if (app != null){
                this.ElementStyle = app.Resources["DataGridTextStyle"] as Style;
                this.EditingElementStyle = app.Resources["DataGridEditingTextStyle"] as Style;
            }
        }
    }
}
