namespace FirstFloor.ModernUI.Windows.Attached {
    public interface IDraggableCloneable {
        bool CanBeCloned { get; }

        object Clone();
    }
}