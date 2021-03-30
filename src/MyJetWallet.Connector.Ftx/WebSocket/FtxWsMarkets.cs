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
    public class FtxWsMarkets: IDisposable
    {
        private readonly ILogger<FtxWsMarkets> _logger;
        private WebsocketEngine _engine;

        public static string Url { get; set; } = "wss://ftx.com/ws/";

        private Dictionary<string, MarketState> _data = new Dictionary<string, MarketState>();
        private object _sync = new object();

        public FtxWsMarkets(ILogger<FtxWsMarkets> logger)
        {
            _logger = logger;
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

        public MarketState GetMarketStateById(string id, string type = default)
        {
            lock (_sync)
            {
                if (_data.TryGetValue(id, out var marketState) && (string.IsNullOrEmpty(type) || marketState.type == type))
                    return marketState;

                return null;
            }
        }

        public List<MarketState> GetMarketState(string type = default)
        {
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(type))
                    return _data.Values.Where(e => e.type == type).ToList();

                return _data.Values.ToList();
            }
        }

        public Func<List<MarketState>, Task> ReceiveUpdates;

        public void Dispose()
        {
            _engine.Stop();
            _engine.Dispose();
        }

        private async Task Connect(ClientWebSocket webSocket)
        {
            await webSocket.SubscribeFtxChannel("markets");
        }

        private async Task Receive(ClientWebSocket webSocket, string msg)
        {
            var packet = JsonConvert.DeserializeObject<FtxWebsocketReceive<DataAction<Dictionary<string, MarketState>>>>(msg);
            
            if (packet?.Channel == "markets" && (packet.Type == FtxWebsocketReceive.Partial || packet.Type == FtxWebsocketReceive.Update))
            {
                lock (_sync)
                {
                    if (packet.Type == FtxWebsocketReceive.Partial)
                        _data.Clear();

                    foreach (var marketState in packet.Data.Data)
                    {
                        marketState.Value.id = marketState.Key;
                        _data[marketState.Key] = marketState.Value;
                    }
                }

                await OnReceiveUpdates(packet.Data.Data.Values.ToList());
            }
        }

        private async Task SendPing(ClientWebSocket webSocket)
        {
            await webSocket.SendFtxPing();
        }

        private async Task OnReceiveUpdates(List<MarketState> markets)
        {
            try
            {
                var action = ReceiveUpdates;
                if (action != null)
                    await action?.Invoke(markets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception from method OnReceiveUpdates from client code");
            }
        }
    }
}