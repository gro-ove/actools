namespace AcManager.Controls.UserControls.Web {
    public interface ICustomStyleProvider {
        string GetStyle(string url, bool transparentBackgroundSupported);
    }
}