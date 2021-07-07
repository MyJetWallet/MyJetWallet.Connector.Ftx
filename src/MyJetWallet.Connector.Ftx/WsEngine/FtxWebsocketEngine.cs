using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.WebSocket;

namespace MyJetWallet.Connector.Ftx.WsEngine
{
    public class FtxWebsocketEngine : WebsocketEngine
    {
        public FtxWebsocketEngine(string name, string url, int pingIntervalMSec, int silenceDisconnectIntervalMSec,
            ILogger logger) : base(name, url, pingIntervalMSec, silenceDisconnectIntervalMSec, logger)
        {
        }

        protected override void InitHeaders(ClientWebSocket clientWebSocket)
        {
        }
    }
}