using System;
using System.Threading;

namespace AcManager.DiscordRpc {
    public interface IDiscordHandler {
        bool HandlesJoin { get; }
        bool HandlesSpectate { get; }
        bool HandlesJoinRequest { get; }

        void OnFirstConnectionEstablished(string appId);
        void JoinRequest(DiscordJoinRequest request, CancellationToken cancellation, Action<DiscordJoinRequestReply> callback);
        void Spectate(string secret);
        void Join(string secret);
    }
}