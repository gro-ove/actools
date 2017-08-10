namespace AcTools.DataFile {
    public interface ISyntaxErrorsCatcher {
        void Catch(DataFileBase file, int line);
    }
}