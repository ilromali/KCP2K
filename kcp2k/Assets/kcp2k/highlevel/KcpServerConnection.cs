using System.Net;
using System.Net.Sockets;

namespace kcp2k
{
    public class KcpServerConnection : KcpConnection
    {
        public KcpServerConnection(Socket socket, EndPoint remoteEndpoint, bool noDelay, uint interval = Kcp.INTERVAL, int fastResend = 0, bool congestionWindow = true, uint SendWindowSize = Kcp.WND_SND, uint ReceiveWindowSize = Kcp.WND_RCV)
        {
            this.socket = socket;
            this.remoteEndpoint = remoteEndpoint;
            SetupKcp(noDelay, interval, fastResend, congestionWindow, SendWindowSize, ReceiveWindowSize);
        }

        protected override void RawSend(byte[] data, int length)
        {
            socket.SendTo(data, 0, length, SocketFlags.None, remoteEndpoint);
        }
    }
}
