namespace AcManager.Tools.AcObjectsNew {
    public class AcObjectEventArgs<T> {
        public AcObjectEventArgs(T acObject) {
            AcObject = acObject;
        }

        public T AcObject { get; }
    }
}