#if MIRROR
using System;
using System.Linq;
using System.Net;
using UnityEngine;
using Mirror;

namespace kcp2k
{
    [DisallowMultipleComponent]
    public class KcpTransport : Transport
    {
        // scheme used by this transport
        public const string Scheme = "kcp";

        // common
        [Header("Transport Configuration")]
        public ushort Port = 7777;
        [Tooltip("NoDelay is recommended to reduce latency. This also scales better without buffers getting full.")]
        public bool NoDelay = true;
        [Tooltip("KCP internal update interval. 100ms is KCP default, but a lower interval is recommended to minimize latency and to scale to more networked entities.")]
        public uint Interval = 10;
        [Header("Advanced")]
        [Tooltip("KCP fastresend parameter. Faster resend for the cost of higher bandwidth. 0 in normal mode, 2 in turbo mode.")]
        public int FastResend = 2;
        [Tooltip("KCP congestion window. Enabled in normal mode, disabled in turbo mode. Disable this for high scale games if connections get chocked regularly.")]
        public bool CongestionWindow = false; // KCP 'NoCongestionWindow' is false by default. here we negate it for ease of use.
        [Tooltip("KCP window size can be modified to support higher loads.")]
        public uint SendWindowSize = 2048; //Kcp.WND_SND; 32 by default. Mirror sends a lot, so we need a lot more.
        [Tooltip("KCP window size can be modified to support higher loads.")]
        public uint ReceiveWindowSize = 2048; //Kcp.WND_RCV; 128 by default. Mirror sends a lot, so we need a lot more.

        // server & client
        KcpServer server;
        KcpClient client;

        // debugging
        [Header("Debug")]
        public bool debugLog;
        public bool debugGUI;

        void Awake()
        {
            // logging
            //   Log.Info should use Debug.Log if enabled, or nothing otherwise
            //   (don't want to spam the console on headless servers)
            if (debugLog)
                Log.Info = Debug.Log;
            else
                Log.Info = _ => {};
            Log.Warning = Debug.LogWarning;
            Log.Error = Debug.LogError;

            // client
            client = new KcpClient(
                () => OnClientConnected.Invoke(),
                (message) => OnClientDataReceived.Invoke(message, Channels.DefaultReliable),
                () => OnClientDisconnected.Invoke()
            );

            // server
            server = new KcpServer(
                (connectionId) => OnServerConnected.Invoke(connectionId),
                (connectionId, message) => OnServerDataReceived.Invoke(connectionId, message, Channels.DefaultReliable),
                (connectionId) => OnServerDisconnected.Invoke(connectionId),
                NoDelay,
                Interval,
                FastResend,
                CongestionWindow,
                SendWindowSize,
                ReceiveWindowSize
            );

            // scene change message will disable transports.
            // kcp processes messages in an internal loop which should be
            // stopped immediately after scene change (= after disabled)
            client.OnCheckEnabled = () => enabled;
            server.OnCheckEnabled = () => enabled;

            Debug.Log("KcpTransport initialized!");
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // client
        public override bool ClientConnected() => client.connected;
        public override void ClientConnect(string address)
        {
            client.Connect(address, Port, NoDelay, Interval, FastResend, CongestionWindow, SendWindowSize, ReceiveWindowSize);
        }
        public override void ClientSend(int channelId, ArraySegment<byte> segment)
        {
            client.Send(segment);
        }
        public override void ClientDisconnect() => client.Disconnect();

        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // scene change messages disable transports to stop them from
            // processing while changing the scene.
            // -> we need to check enabled here
            // -> and in kcp's internal loops, see Awake() OnCheckEnabled setup!
            // (see also: https://github.com/vis2k/Mirror/pull/379)
            if (!enabled)
                return;

            server.Tick();
            client.Tick();
        }

        // server
        public override Uri ServerUri()
        {
            UriBuilder builder = new UriBuilder();
            builder.Scheme = Scheme;
            builder.Host = Dns.GetHostName();
            builder.Port = Port;
            return builder.Uri;
        }
        public override bool ServerActive() => server.IsActive();
        public override void ServerStart() => server.Start(Port);
        public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            server.Send(connectionId, segment);
        }
        public override bool ServerDisconnect(int connectionId)
        {
            server.Disconnect(connectionId);
            return true;
        }
        public override string ServerGetClientAddress(int connectionId) => server.GetClientAddress(connectionId);
        public override void ServerStop() => server.Stop();

        // common
        public override void Shutdown() {}

        // max message size
        public override int GetMaxPacketSize(int channelId = Channels.DefaultReliable) => KcpConnection.MaxMessageSize;

        public override string ToString()
        {
            return "KCP";
        }

        int GetTotalSendQueue() =>
            server.connections.Values.Sum(conn => conn.SendQueueCount);
        int GetTotalReceiveQueue() =>
            server.connections.Values.Sum(conn => conn.ReceiveQueueCount);
        int GetTotalSendBuffer() =>
            server.connections.Values.Sum(conn => conn.SendBufferCount);
        int GetTotalReceiveBuffer() =>
            server.connections.Values.Sum(conn => conn.ReceiveBufferCount);

        void OnGUI()
        {
            if (!debugGUI) return;

            GUILayout.BeginArea(new Rect(5, 100, 300, 300));

            if (ServerActive())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("SERVER");
                GUILayout.Label("  connections: " + server.connections.Count);
                GUILayout.Label("  SendQueue: " + GetTotalSendQueue());
                GUILayout.Label("  ReceiveQueue: " + GetTotalReceiveQueue());
                GUILayout.Label("  SendBuffer: " + GetTotalSendBuffer());
                GUILayout.Label("  ReceiveBuffer: " + GetTotalReceiveBuffer());
                GUILayout.EndVertical();
            }

            if (ClientConnected())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("CLIENT");
                GUILayout.Label("  SendQueue: " + client.connection.SendQueueCount);
                GUILayout.Label("  ReceiveQueue: " + client.connection.ReceiveQueueCount);
                GUILayout.Label("  SendBuffer: " + client.connection.SendBufferCount);
                GUILayout.Label("  ReceiveBuffer: " + client.connection.ReceiveBufferCount);
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }
    }
}
#endif
