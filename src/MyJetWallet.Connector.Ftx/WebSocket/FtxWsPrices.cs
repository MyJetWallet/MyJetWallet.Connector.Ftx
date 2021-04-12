using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyJetWallet.Connector.Ftx.WebSocket.Models;
using MyJetWallet.Connector.Ftx.WsEngine;
using Newtonsoft.Json;

namespace MyJetWallet.Connector.Ftx.WebSocket
{
    public class FtxWsPrices: IDisposable
    {
        private readonly ILogger<FtxWsPrices> _logger;
        private readonly IReadOnlyCollection<string> _marketList;
        private WebsocketEngine _engine;

        public static string Url { get; set; } = "wss://ftx.com/ws/";

        private Dictionary<string, FtxTicker> _data = new Dictionary<string, FtxTicker>();
        private object _sync = new object();

        public FtxWsPrices(ILogger<FtxWsPrices> logger, IReadOnlyCollection<string> marketList)
        {
            _logger = logger;
            _marketList = marketList;
            _engine = new WebsocketEngine(nameof(FtxWsMarkets), Url, 5000, 10000, logger);
            _engine.SendPing = SendPing;
            _engine.OnReceive = Receive;
            _engine.OnConnect = Connect;
        }

        public void Start()
        {
            _engine.Start();
        }

        public void Stop()
        {
            _engine.Stop();
        }

        public FtxTicker GetMarketStateById(string id)
        {
            lock (_sync)
            {
                if (_data.TryGetValue(id, out var price))
                    return price;

                return null;
            }
        }

        public List<FtxTicker> GetPrices()
        {
            lock (_sync)
            {
                return _data.Values.ToList();
            }
        }

        public Func<FtxTicker, Task> ReceiveUpdates;

        public void Dispose()
        {
            _engine.Stop();
            _engine.Dispose();
        }

        public async Task Reset(string market)
        {
            var webSocket = _engine.GetClientWebSocket();
            if (webSocket == null)
                return;

            await webSocket.UnSubscribeFtxChannel("ticker", market);
            await webSocket.SubscribeFtxChannel("ticker", market);
        }

        private async Task Connect(ClientWebSocket webSocket)
        {
            foreach (var market in _marketList)
            {
                await webSocket.SubscribeFtxChannel("ticker", market);
            }
        }

        private async Task Receive(ClientWebSocket webSocket, string msg)
        {
            var packet = JsonConvert.DeserializeObject<FtxWebsocketReceive<FtxTicker>>(msg);
            
            if (packet?.Channel == "ticker" && (packet.Type == FtxWebsocketReceive.Partial || packet.Type == FtxWebsocketReceive.Update))
            {
                packet.Data.id = packet.Market;
                lock (_sync)
                {
                    _data[packet.Data.id] = packet.Data;
                }

                await OnReceiveUpdates(packet.Data);
            }
        }

        private async Task SendPing(ClientWebSocket webSocket)
        {
            await webSocket.SendFtxPing();
        }



        private async Task OnReceiveUpdates(FtxTicker price)
        {
            try
            {
                var action = ReceiveUpdates;
                if (action != null)
                    await action?.Invoke(price);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception from method OnReceiveUpdates from client code");
            }
        }
    }
}