using AcTools.Kn5File;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kn5Materials {
    public partial class MainForm : Form {
        string _text;
        TreeNode _library;

        public MainForm(string[] args) {
            InitializeComponent();

            _text = Text;
            _library = new TreeNode("Library");
            treeViewKn5.Nodes.Add(_library);

            treeViewKn5.SelectedNode = _library;
            foreach (var filename in args) {
                treeViewKn5.SelectedNode = Add(filename);
            }
        }

        TreeNode Add(string filename) {
            var kn5 = Kn5.FromFile(filename);
            var node = new TreeNode("File: " + Path.GetFileName(filename));
            node.Tag = kn5;

            var materials = new TreeNode("Materials");
            node.Nodes.Add(materials);

            foreach (var material in kn5.Materials.Values) {
                var materialNode = new TreeNode(material.Name);
                materials.Nodes.Add(materialNode);
                materialNode.Tag = material;
            }

            var textures = new TreeNode("Textures");
            node.Nodes.Add(textures);

            foreach (var texture in kn5.Textures.Values) {
                var textureNode = new TreeNode(texture.Name);
                textures.Nodes.Add(textureNode);
                textureNode.Tag = texture;
            }

            treeViewKn5.Nodes.Add(node);

            node.Toggle();
            materials.Toggle();

            return node;
        }

        private Kn5 GetKn5(TreeNode node) {
            while (node != null && (node.Tag == null || !(node.Tag is Kn5))) {
                node = node.Parent;
            }

            return node == null ? null : (Kn5)node.Tag;
        }

        Dictionary<string, DDSImage> _textures = new Dictionary<string, DDSImage>();

        private DDSImage PreviewTexture(Kn5 kn5, Kn5Texture texture) {
            var data = kn5.TexturesData[texture.Filename];
            var key = kn5.OriginalFilename + "/" + texture.Filename;

            if (!_textures.ContainsKey(key)) {
                try {
                    _textures[key] = new DDSImage(data);
                } catch (Exception) {
                    pictureBox.Image = null;
                    return null;
                }
            }

            var bitmap = _textures[key].Bitmap == null ? null :
                new Bitmap(_textures[key].Bitmap, new Size(512, 512));
            pictureBox.Image = bitmap;
            return _textures[key];
        }

        bool _listMode;

        bool ListMode {
            set {
                if (value != _listMode) {
                    if (value) {
                        tableLayoutPanelMain.ColumnStyles[0].SizeType = SizeType.Percent;
                        tableLayoutPanelMain.ColumnStyles[0].Width = 100;
                        tableLayoutPanelMain.ColumnStyles[1].Width = 0;
                    } else {
                        tableLayoutPanelMain.ColumnStyles[0].SizeType = SizeType.AutoSize;
                        tableLayoutPanelMain.ColumnStyles[1].Width = 50;
                    }

                    _listMode = value;
                }
            }
        }

        bool _materialMode;

        bool MaterialMode {
            set {
                if (value != _materialMode) {
                    if (value) {
                        tableLayoutPanelParams.RowStyles[2].SizeType = SizeType.AutoSize;
                        tableLayoutPanelParams.RowStyles[3].Height = 50;
                        tableLayoutPanelParams.RowStyles[4].Height = 25;
                    } else {
                        tableLayoutPanelParams.RowStyles[2].SizeType = SizeType.Absolute;
                        tableLayoutPanelParams.RowStyles[3].Height = 0;
                        tableLayoutPanelParams.RowStyles[4].Height = 0;
                    }

                    _materialMode = value;
                }
            }
        }

        Kn5 _kn5;
        Kn5Material _material;
        Kn5Texture _texture;

        private void treeViewKn5_AfterSelect(object sender, TreeViewEventArgs e) {
            var selected = treeViewKn5.SelectedNode;
            _kn5 = GetKn5(selected);

            if (_kn5 != null) {
                Text = Path.GetFileName(_kn5.OriginalFilename) + " - " + _text;
            } else {
                Text = _text;
            }

            _material = null;
            _texture = null;

            var listMode = true;
            var materialMode = false;

            if (selected.Tag != null && _kn5 != null) {
                if (selected.Tag is Kn5Material) {
                    listMode = false;
                    materialMode = true;

                    _material = (Kn5Material)selected.Tag;
                    labelInfo.Text = "Name: " + _material.Name;

                    Kn5Texture texture = null;
                    foreach (var mapping in _material.TextureMappings) {
                        texture = _kn5.Textures[mapping.Texture];
                        break;
                    }

                    if (texture == null) {
                        pictureBox.Image = null;
                    } else {
                        PreviewTexture(_kn5, texture);
                    }

                    textBoxShaderName.Text = _material.ShaderName;
                    comboBoxBlendMode.SelectedIndex = (int)_material.BlendMode;
                    comboBoxDepthMode.SelectedIndex = (int)_material.DepthMode;
                    checkBoxAlphaTested.Checked = _material.AlphaTested;

                    dataGridViewProperties.Rows.Clear();
                    foreach (var property in _material.ShaderProperties) {
                        dataGridViewProperties.Rows.Add(new object[]{
                            property.Name,
                            Convert.ToString(property.ValueA),
                            String.Join(", ", property.ValueB),
                            String.Join(", ", property.ValueC),
                            String.Join(", ", property.ValueD)
                        });
                    }

                    dataGridViewTextureMappings.Rows.Clear();
                    foreach (var mapping in _material.TextureMappings) {
                        dataGridViewTextureMappings.Rows.Add(new object[]{
                            mapping.Name,
                            mapping.Texture,
                            Convert.ToString(mapping.Slot)
                        });
                    }
                } else if (selected.Tag is Kn5Texture) {
                    listMode = false;
                    tableLayoutPanelMain.ColumnStyles[0].SizeType = SizeType.AutoSize;
                    tableLayoutPanelMain.ColumnStyles[1].Width = 50;

                    _texture = (Kn5Texture)selected.Tag;
                    var dds = PreviewTexture(_kn5, _texture);
                    labelInfo.Text = "Name: " + _texture.Name + "\r\n" +
                        (dds == null || dds.Bitmap == null ? "Read failed" :
                        "Size: " + dds.Bitmap.Width + "×" + dds.Bitmap.Height + "\r\n" +
                        "Pixel format: " + dds.Format);
                }
            }

            ListMode = listMode;
            MaterialMode = materialMode;
        }

        private void textBoxShaderName_TextChanged(object sender, EventArgs e) {
            _material.ShaderName = textBoxShaderName.Text;
        }

        private void comboBoxBlendMode_SelectedIndexChanged(object sender, EventArgs e) {
            _material.BlendMode = (Kn5MaterialBlendMode)comboBoxBlendMode.SelectedIndex;
        }

        private void comboBoxDepthMode_SelectedIndexChanged(object sender, EventArgs e) {
            _material.DepthMode = (Kn5MaterialDepthMode)comboBoxBlendMode.SelectedIndex;
        }

        private void checkBoxAlphaTested_CheckedChanged(object sender, EventArgs e) {
            _material.AlphaTested = checkBoxAlphaTested.Checked;
        }

        private void dataGridViewProperties_CellValueChanged(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex >= 0 && _material != null) {
                var property = _material.ShaderProperties[e.RowIndex];
                var cell = dataGridViewProperties.Rows[e.RowIndex].Cells[e.ColumnIndex];

                try {
                    var values = ((string)cell.Value).Split(new char[] { ',' }).Select(x => Convert.ToSingle(x.Trim())).ToArray();

                    switch (e.ColumnIndex) {
                        case 1:
                            property.ValueA = values[0];
                            break;

                        case 2:
                            if (values.Length != 2) {
                                throw new InvalidCastException();
                            }

                            property.ValueB = values;
                            break;

                        case 3:
                            if (values.Length != 3) {
                                throw new InvalidCastException();
                            }

                            property.ValueC = values;
                            break;

                        case 4:
                            if (values.Length != 4) {
                                throw new InvalidCastException();
                            }

                            property.ValueD = values;
                            break;
                    }

                    cell.ErrorText = null;
                } catch (InvalidCastException) {
                    cell.ErrorText = "Invalid format!";
                }
            }
        }

        private void dataGridViewTextureMappings_CellValueChanged(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex >= 0 && _material != null) {
                var mapping = _material.TextureMappings[e.RowIndex];
                var cell = dataGridViewTextureMappings.Rows[e.RowIndex].Cells[e.ColumnIndex];

                try {
                    var value = (string)cell.Value;

                    switch (e.ColumnIndex) {
                        case 1:
                            if (!_kn5.Textures.ContainsKey(value)) {
                                throw new InvalidCastException();
                            }

                            mapping.Texture = value;
                            break;
                    }

                    cell.ErrorText = null;
                } catch (InvalidCastException) {
                    cell.ErrorText = "Invalid format!";
                }
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
            Close();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Open File";
            dialog.Filter = "KN5 files|*.kn5";
            dialog.Multiselect = true;
            if (dialog.ShowDialog() == DialogResult.OK) {
                foreach (var filename in dialog.FileNames) {
                    Add(filename);
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
            if (_kn5 != null) {
                if (File.Exists(_kn5.OriginalFilename + "~bak")) {
                    File.Delete(_kn5.OriginalFilename);
                } else {
                    File.Move(_kn5.OriginalFilename, _kn5.OriginalFilename + "~bak");
                }
                _kn5.Save(_kn5.OriginalFilename);
            }
        }
    }
}
