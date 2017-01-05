using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls
{
    /// <summary>
    /// A DataGrid checkbox column using default Modern UI element styles.
    /// </summary>
    public class DataGridCheckBoxColumn
        : System.Windows.Controls.DataGridCheckBoxColumn
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCheckBoxColumn"/> class.
        /// </summary>
        public DataGridCheckBoxColumn()
        {
            var app = Application.Current;
            if (app != null){
                this.ElementStyle = app.Resources["DataGridCheckBoxStyle"] as Style;
                this.EditingElementStyle = app.Resources["DataGridEditingCheckBoxStyle"] as Style;
            }
        }
    }
}
