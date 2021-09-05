namespace AcManager.Tools.AcPlugins.Extras {
    public interface IAcLeaderboardCommandHelper {
        void KickPlayer(int carId);
        void BanPlayer(int carId);
        void MentionInChat(int carId);
        void SendMessageDirectly(int carId);
    }
}