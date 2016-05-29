namespace AcManager.Tools.AcObjectsNew {
    public delegate void AcObjectEventHandler<T>(object sender, AcObjectEventArgs<T> args) where T : AcObjectNew;
}