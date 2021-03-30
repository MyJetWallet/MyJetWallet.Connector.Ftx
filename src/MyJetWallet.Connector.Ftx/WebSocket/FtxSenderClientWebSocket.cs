using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyJetWallet.Connector.Ftx.WebSocket
{
    public static class FtxSenderClientWebSocket
    {
        public static async Task SendFtxPing(this ClientWebSocket webSocket)
        {
            var msg = JsonSerializer.Serialize(new { op = "ping" });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)),
                WebSocketMessageType.Text, true, new CancellationTokenSource(3000).Token);
        }


        public static async Task SubscribeFtxChannel(this ClientWebSocket webSocket, string channel, string market)
        {
            var msg = JsonSerializer.Serialize(new { op = "subscribe", channel = channel, market = market });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task SubscribeFtxChannel(this ClientWebSocket webSocket, string channel)
        {
            var msg = JsonSerializer.Serialize(new { op = "subscribe", channel = channel });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task UnSubscribeFtxChannel(this ClientWebSocket webSocket, string channel)
        {
            var msg = JsonSerializer.Serialize(new { op = "unsubscribe", channel = channel });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task UnSubscribeFtxChannel(this ClientWebSocket webSocket, string channel, string market)
        {
            var msg = JsonSerializer.Serialize(new { op = "unsubscribe", channel = channel, market = market });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}