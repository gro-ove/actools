namespace AcManager.Controls {
    public interface IPreviewProvider {
        object GetPreview(string serializedData);
    }
}