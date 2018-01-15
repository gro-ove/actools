using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AcManager.Tools.Helpers {
    public static class SocketExtension {
        public static Task ConnectTaskAsync(this Socket socket, EndPoint endpoint) {
            return Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null);
        }

        public static Task<int> SendTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None) {
            var result = socket.BeginSend(buffer, offset, size, flags, _ => { }, socket);
            return result == null ? Task.FromResult(0) : Task.Factory.FromAsync(result, socket.EndSend);
        }

        public static Task<int> SendTaskAsync(this Socket socket, byte[] buffer, SocketFlags flags = SocketFlags.None) {
            return socket.SendTaskAsync(buffer, 0, buffer.Length, flags);
        }

        public static Task<int> ReceiveTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None) {
            var result = socket.BeginReceive(buffer, offset, size, flags, _ => { }, socket);
            return result == null ? Task.FromResult(0) : Task.Factory.FromAsync(result, socket.EndReceive);
        }

        public static Task<int> ReceiveTaskAsync(this Socket socket, byte[] buffer, SocketFlags flags = SocketFlags.None) {
            return socket.ReceiveTaskAsync(buffer, 0, buffer.Length, flags);
        }
    }
}