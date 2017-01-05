using System.Windows;

namespace FirstFloor.ModernUI.Windows.Controls
{
    /// <summary>
    /// A DataGrid checkbox column using default Modern UI element styles.
    /// </summary>
    public class DataGridComboBoxColumn
        : System.Windows.Controls.DataGridComboBoxColumn
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridComboBoxColumn"/> class.
        /// </summary>
        public DataGridComboBoxColumn()
        {
            var app = Application.Current;
            if (app != null){
                this.EditingElementStyle = app.Resources["DataGridEditingComboBoxStyle"] as Style;
            }
        }
    }
}
