namespace FirstFloor.ModernUI {
    public class ListenersChangedEventHandlerArgs {
        public readonly int NewListenersCount, OldListenersCount;

        public ListenersChangedEventHandlerArgs(int newCount, int oldCount) {
            NewListenersCount = newCount;
            OldListenersCount = oldCount;
        }
    }
}