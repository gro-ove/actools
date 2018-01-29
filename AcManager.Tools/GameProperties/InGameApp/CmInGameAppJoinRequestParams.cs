using System;

namespace AcManager.Tools.GameProperties.InGameApp {
    public class CmInGameAppJoinRequestParams : CmInGameAppParamsBase {
        public string UserName;
        public string UserId;
        public string AvatarUrl;
        public double YesProgress, NoProgress;
        public Action<bool> ChoiseCallback;

        public CmInGameAppJoinRequestParams(string userName, string userId, string avatarUrl, Action<bool> choiseCallback) {
            UserName = userName;
            UserId = userId;
            AvatarUrl = avatarUrl;
            ChoiseCallback = choiseCallback;
        }
    }
}