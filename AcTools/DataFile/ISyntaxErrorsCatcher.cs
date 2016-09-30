namespace AcTools.DataFile {
    public interface ISyntaxErrorsCatcher {
        void Catch(AbstractDataFile file, int line);
    }
}