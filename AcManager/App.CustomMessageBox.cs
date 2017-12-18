using System;
using System.Drawing;
using System.Windows.Forms;

namespace AcManager {
    public partial class App {
        /// <summary>
        /// Something very primitive based on Windows Forms for very special occasions.
        /// </summary>
        public sealed class CustomMessageBox : Form {
            private readonly RichTextBox _message = new RichTextBox();
            readonly Button _b1 = new Button();
            readonly Button _b2 = new Button();

            public CustomMessageBox() { }

            public CustomMessageBox(string body, string title, string yesButton, string noButton) {
                ClientSize = new Size(490, 150);
                Text = title;

                _b1.Location = new Point(290, 112);
                _b1.Size = new Size(190, 28);
                _b1.Text = yesButton;
                _b1.BackColor = DefaultBackColor;

                _b2.Location = new Point(10, 112);
                _b2.Size = new Size(270, 28);
                _b2.Text = noButton;
                _b2.BackColor = DefaultBackColor;

                _message.Location = new Point(10, 10);
                _message.Size = new Size(470, 130);
                _message.Text = body;
                _message.Font = DefaultFont;
                _message.AutoSize = true;
                _message.WordWrap = true;
                _message.ReadOnly = true;
                _message.BackColor = BackColor;
                _message.BorderStyle = BorderStyle.None;
                _message.Padding = Padding.Empty;
                _message.Enabled = true;

                FormBorderStyle = FormBorderStyle.FixedSingle;
                ShowIcon = false;
                ShowInTaskbar = false;
                TopMost = true;

                Controls.Add(_b1);
                Controls.Add(_b2);
                Controls.Add(_message);

                _b1.Click += OnClick;
                _b2.Click += OnClick;
            }

            private void OnClick(object sender, EventArgs eventArgs) {
                DialogResult = sender == _b1 ? DialogResult.Yes : DialogResult.No;
            }
        }
    }
}