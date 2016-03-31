namespace Kn5Materials {
    partial class MainForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.treeViewKn5 = new System.Windows.Forms.TreeView();
            this.tableLayoutPanelParams = new System.Windows.Forms.TableLayoutPanel();
            this.dataGridViewTextureMappings = new System.Windows.Forms.DataGridView();
            this.MappingName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MappingTexture = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.MappingSlot = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.labelInfo = new System.Windows.Forms.Label();
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.dataGridViewProperties = new System.Windows.Forms.DataGridView();
            this.PropertyName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValueA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValueB = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValueC = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ValueD = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel = new System.Windows.Forms.Panel();
            this.checkBoxAlphaTested = new System.Windows.Forms.CheckBox();
            this.labelDepthMode = new System.Windows.Forms.Label();
            this.comboBoxDepthMode = new System.Windows.Forms.ComboBox();
            this.labelBlendMode = new System.Windows.Forms.Label();
            this.labelShader = new System.Windows.Forms.Label();
            this.comboBoxBlendMode = new System.Windows.Forms.ComboBox();
            this.textBoxShaderName = new System.Windows.Forms.TextBox();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clostToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tableLayoutPanelMain.SuspendLayout();
            this.tableLayoutPanelParams.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewTextureMappings)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewProperties)).BeginInit();
            this.panel.SuspendLayout();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.treeViewKn5, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelParams, 1, 0);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 24);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 1;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(550, 594);
            this.tableLayoutPanelMain.TabIndex = 1;
            // 
            // treeViewKn5
            // 
            this.treeViewKn5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewKn5.Location = new System.Drawing.Point(3, 3);
            this.treeViewKn5.MinimumSize = new System.Drawing.Size(200, 480);
            this.treeViewKn5.Name = "treeViewKn5";
            this.treeViewKn5.Size = new System.Drawing.Size(200, 588);
            this.treeViewKn5.TabIndex = 5;
            this.treeViewKn5.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeViewKn5_AfterSelect);
            // 
            // tableLayoutPanelParams
            // 
            this.tableLayoutPanelParams.ColumnCount = 1;
            this.tableLayoutPanelParams.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelParams.Controls.Add(this.dataGridViewTextureMappings, 0, 4);
            this.tableLayoutPanelParams.Controls.Add(this.labelInfo, 0, 1);
            this.tableLayoutPanelParams.Controls.Add(this.pictureBox, 0, 0);
            this.tableLayoutPanelParams.Controls.Add(this.dataGridViewProperties, 0, 3);
            this.tableLayoutPanelParams.Controls.Add(this.panel, 0, 2);
            this.tableLayoutPanelParams.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelParams.Location = new System.Drawing.Point(209, 3);
            this.tableLayoutPanelParams.Name = "tableLayoutPanelParams";
            this.tableLayoutPanelParams.RowCount = 6;
            this.tableLayoutPanelParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelParams.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelParams.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanelParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelParams.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelParams.Size = new System.Drawing.Size(338, 588);
            this.tableLayoutPanelParams.TabIndex = 4;
            // 
            // dataGridViewTextureMappings
            // 
            this.dataGridViewTextureMappings.AllowDrop = true;
            this.dataGridViewTextureMappings.AllowUserToAddRows = false;
            this.dataGridViewTextureMappings.AllowUserToDeleteRows = false;
            this.dataGridViewTextureMappings.AllowUserToOrderColumns = true;
            this.dataGridViewTextureMappings.AllowUserToResizeRows = false;
            this.dataGridViewTextureMappings.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewTextureMappings.BackgroundColor = System.Drawing.SystemColors.Control;
            this.dataGridViewTextureMappings.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewTextureMappings.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.MappingName,
            this.MappingTexture,
            this.MappingSlot});
            this.dataGridViewTextureMappings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewTextureMappings.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridViewTextureMappings.Location = new System.Drawing.Point(3, 460);
            this.dataGridViewTextureMappings.Name = "dataGridViewTextureMappings";
            this.dataGridViewTextureMappings.RowHeadersVisible = false;
            this.dataGridViewTextureMappings.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridViewTextureMappings.Size = new System.Drawing.Size(332, 103);
            this.dataGridViewTextureMappings.TabIndex = 5;
            this.dataGridViewTextureMappings.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewTextureMappings_CellValueChanged);
            // 
            // MappingName
            // 
            this.MappingName.HeaderText = "Name";
            this.MappingName.Name = "MappingName";
            this.MappingName.ReadOnly = true;
            // 
            // MappingTexture
            // 
            this.MappingTexture.HeaderText = "Texture";
            this.MappingTexture.Name = "MappingTexture";
            // 
            // MappingSlot
            // 
            this.MappingSlot.HeaderText = "Slot";
            this.MappingSlot.Name = "MappingSlot";
            this.MappingSlot.ReadOnly = true;
            // 
            // labelInfo
            // 
            this.labelInfo.AutoSize = true;
            this.labelInfo.Location = new System.Drawing.Point(3, 109);
            this.labelInfo.Name = "labelInfo";
            this.labelInfo.Size = new System.Drawing.Size(35, 13);
            this.labelInfo.TabIndex = 3;
            this.labelInfo.Text = "Name";
            // 
            // pictureBox
            // 
            this.pictureBox.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.pictureBox.BackgroundImage = global::Kn5Materials.Properties.Resources.Background;
            this.pictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox.Location = new System.Drawing.Point(3, 3);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(332, 103);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox.TabIndex = 2;
            this.pictureBox.TabStop = false;
            // 
            // dataGridViewProperties
            // 
            this.dataGridViewProperties.AllowDrop = true;
            this.dataGridViewProperties.AllowUserToAddRows = false;
            this.dataGridViewProperties.AllowUserToDeleteRows = false;
            this.dataGridViewProperties.AllowUserToOrderColumns = true;
            this.dataGridViewProperties.AllowUserToResizeRows = false;
            this.dataGridViewProperties.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewProperties.BackgroundColor = System.Drawing.SystemColors.Control;
            this.dataGridViewProperties.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewProperties.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.PropertyName,
            this.ValueA,
            this.ValueB,
            this.ValueC,
            this.ValueD});
            this.dataGridViewProperties.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewProperties.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridViewProperties.Location = new System.Drawing.Point(3, 241);
            this.dataGridViewProperties.Name = "dataGridViewProperties";
            this.dataGridViewProperties.RowHeadersVisible = false;
            this.dataGridViewProperties.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dataGridViewProperties.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridViewProperties.Size = new System.Drawing.Size(332, 213);
            this.dataGridViewProperties.TabIndex = 4;
            this.dataGridViewProperties.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewProperties_CellValueChanged);
            // 
            // PropertyName
            // 
            this.PropertyName.HeaderText = "Name";
            this.PropertyName.Name = "PropertyName";
            this.PropertyName.ReadOnly = true;
            // 
            // ValueA
            // 
            this.ValueA.HeaderText = "A";
            this.ValueA.Name = "ValueA";
            // 
            // ValueB
            // 
            this.ValueB.HeaderText = "B";
            this.ValueB.Name = "ValueB";
            // 
            // ValueC
            // 
            this.ValueC.HeaderText = "C";
            this.ValueC.Name = "ValueC";
            // 
            // ValueD
            // 
            this.ValueD.HeaderText = "D";
            this.ValueD.Name = "ValueD";
            // 
            // panel
            // 
            this.panel.Controls.Add(this.checkBoxAlphaTested);
            this.panel.Controls.Add(this.labelDepthMode);
            this.panel.Controls.Add(this.comboBoxDepthMode);
            this.panel.Controls.Add(this.labelBlendMode);
            this.panel.Controls.Add(this.labelShader);
            this.panel.Controls.Add(this.comboBoxBlendMode);
            this.panel.Controls.Add(this.textBoxShaderName);
            this.panel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel.Location = new System.Drawing.Point(3, 125);
            this.panel.MinimumSize = new System.Drawing.Size(275, 110);
            this.panel.Name = "panel";
            this.panel.Size = new System.Drawing.Size(332, 110);
            this.panel.TabIndex = 6;
            // 
            // checkBoxAlphaTested
            // 
            this.checkBoxAlphaTested.AutoSize = true;
            this.checkBoxAlphaTested.Location = new System.Drawing.Point(74, 87);
            this.checkBoxAlphaTested.Name = "checkBoxAlphaTested";
            this.checkBoxAlphaTested.Size = new System.Drawing.Size(85, 17);
            this.checkBoxAlphaTested.TabIndex = 8;
            this.checkBoxAlphaTested.Text = "Alpha tested";
            this.checkBoxAlphaTested.UseVisualStyleBackColor = true;
            this.checkBoxAlphaTested.CheckedChanged += new System.EventHandler(this.checkBoxAlphaTested_CheckedChanged);
            // 
            // labelDepthMode
            // 
            this.labelDepthMode.AutoSize = true;
            this.labelDepthMode.Location = new System.Drawing.Point(3, 63);
            this.labelDepthMode.Name = "labelDepthMode";
            this.labelDepthMode.Size = new System.Drawing.Size(65, 13);
            this.labelDepthMode.TabIndex = 7;
            this.labelDepthMode.Text = "Depth mode";
            // 
            // comboBoxDepthMode
            // 
            this.comboBoxDepthMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDepthMode.FormattingEnabled = true;
            this.comboBoxDepthMode.Items.AddRange(new object[] {
            "Normal",
            "No write",
            "Off"});
            this.comboBoxDepthMode.Location = new System.Drawing.Point(74, 60);
            this.comboBoxDepthMode.Name = "comboBoxDepthMode";
            this.comboBoxDepthMode.Size = new System.Drawing.Size(195, 21);
            this.comboBoxDepthMode.TabIndex = 6;
            this.comboBoxDepthMode.SelectedIndexChanged += new System.EventHandler(this.comboBoxDepthMode_SelectedIndexChanged);
            // 
            // labelBlendMode
            // 
            this.labelBlendMode.AutoSize = true;
            this.labelBlendMode.Location = new System.Drawing.Point(3, 36);
            this.labelBlendMode.Name = "labelBlendMode";
            this.labelBlendMode.Size = new System.Drawing.Size(63, 13);
            this.labelBlendMode.TabIndex = 5;
            this.labelBlendMode.Text = "Blend mode";
            // 
            // labelShader
            // 
            this.labelShader.AutoSize = true;
            this.labelShader.Location = new System.Drawing.Point(3, 10);
            this.labelShader.Name = "labelShader";
            this.labelShader.Size = new System.Drawing.Size(41, 13);
            this.labelShader.TabIndex = 4;
            this.labelShader.Text = "Shader";
            // 
            // comboBoxBlendMode
            // 
            this.comboBoxBlendMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxBlendMode.FormattingEnabled = true;
            this.comboBoxBlendMode.Items.AddRange(new object[] {
            "Opaque",
            "Alpha blend",
            "Alpha to coverage"});
            this.comboBoxBlendMode.Location = new System.Drawing.Point(74, 33);
            this.comboBoxBlendMode.Name = "comboBoxBlendMode";
            this.comboBoxBlendMode.Size = new System.Drawing.Size(195, 21);
            this.comboBoxBlendMode.TabIndex = 1;
            this.comboBoxBlendMode.SelectedIndexChanged += new System.EventHandler(this.comboBoxBlendMode_SelectedIndexChanged);
            // 
            // textBoxShaderName
            // 
            this.textBoxShaderName.Location = new System.Drawing.Point(74, 7);
            this.textBoxShaderName.Name = "textBoxShaderName";
            this.textBoxShaderName.Size = new System.Drawing.Size(195, 20);
            this.textBoxShaderName.TabIndex = 0;
            this.textBoxShaderName.TextChanged += new System.EventHandler(this.textBoxShaderName_TextChanged);
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(550, 24);
            this.menuStrip.TabIndex = 2;
            this.menuStrip.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.saveAsToolStripMenuItem,
            this.clostToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Enabled = false;
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.aboutToolStripMenuItem.Text = "About";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Enabled = false;
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.S)));
            this.saveAsToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.saveAsToolStripMenuItem.Text = "Save As...";
            // 
            // clostToolStripMenuItem
            // 
            this.clostToolStripMenuItem.Enabled = false;
            this.clostToolStripMenuItem.Name = "clostToolStripMenuItem";
            this.clostToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.clostToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.clostToolStripMenuItem.Text = "Close";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4)));
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(195, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(550, 618);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.MinimumSize = new System.Drawing.Size(560, 650);
            this.Name = "MainForm";
            this.Text = "Kn5 Materials";
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelParams.ResumeLayout(false);
            this.tableLayoutPanelParams.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewTextureMappings)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewProperties)).EndInit();
            this.panel.ResumeLayout(false);
            this.panel.PerformLayout();
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelParams;
        private System.Windows.Forms.TreeView treeViewKn5;
        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Label labelInfo;
        private System.Windows.Forms.DataGridView dataGridViewProperties;
        private System.Windows.Forms.DataGridView dataGridViewTextureMappings;
        private System.Windows.Forms.DataGridViewTextBoxColumn PropertyName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValueA;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValueB;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValueC;
        private System.Windows.Forms.DataGridViewTextBoxColumn ValueD;
        private System.Windows.Forms.Panel panel;
        private System.Windows.Forms.Label labelShader;
        private System.Windows.Forms.ComboBox comboBoxBlendMode;
        private System.Windows.Forms.TextBox textBoxShaderName;
        private System.Windows.Forms.Label labelBlendMode;
        private System.Windows.Forms.Label labelDepthMode;
        private System.Windows.Forms.ComboBox comboBoxDepthMode;
        private System.Windows.Forms.CheckBox checkBoxAlphaTested;
        private System.Windows.Forms.DataGridViewTextBoxColumn MappingName;
        private System.Windows.Forms.DataGridViewTextBoxColumn MappingTexture;
        private System.Windows.Forms.DataGridViewTextBoxColumn MappingSlot;
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clostToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
    }
}

