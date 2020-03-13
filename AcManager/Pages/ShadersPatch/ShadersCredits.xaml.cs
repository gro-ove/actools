using System.IO;
using AcManager.Tools.Data;

namespace AcManager.Pages.ShadersPatch {
    public partial class ShadersCredits {
        public ShadersCredits() {
            InitializeComponent();
            TextBlock.Text = PatchVersionInfo.ChangelogPrepare(File.ReadAllText(PatchHelper.GetConfigFilename("data_credits.txt")));
        }
    }
}