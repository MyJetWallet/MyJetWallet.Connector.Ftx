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
    public class FtxWsOrderBooks: IDisposable
    {
        private readonly ILogger<FtxWsOrderBooks> _logger;
        private FtxWebsocketEngine _engine;

        public static string Url { get; set; } = "wss://ftx.com/ws/";

        private readonly Dictionary<string, FtxOrderBook> _data = new();
        private readonly object _sync = new();

        private readonly IReadOnlyCollection<string> _marketList = null;

        public FtxWsOrderBooks(ILogger<FtxWsOrderBooks> logger)
        {
            _logger = logger;
            _engine = new FtxWebsocketEngine(nameof(FtxWsMarkets), Url, 5000, 10000, logger)
            {
                SendPing = SendPing, OnReceive = Receive, OnConnect = Connect
            };
        }

        public FtxWsOrderBooks(ILogger<FtxWsOrderBooks> logger, IReadOnlyCollection<string> marketList)
            :this(logger)
        {
            _marketList = marketList;
        }

        public void Start()
        {
            _engine.Start();
        }

        public void Stop()
        {
            _engine.Stop();
        }

        public FtxOrderBook GetOrderBookById(string id)
        {
            lock (_sync)
            {
                if (_data.TryGetValue(id, out var orderBook))
                {
                    return orderBook.Copy();
                }

                return null;
            }
        }

        public List<FtxOrderBook> GetOrderBooks()
        {
            lock (_sync)
            {
                return _data.Values.Select(e => e.Copy()).ToList();
            }
        }

        public Func<FtxOrderBook, Task> ReceiveUpdates;

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

            await webSocket.UnSubscribeFtxChannel("orderbook", market);
            await webSocket.SubscribeFtxChannel("orderbook", market);
        }

        public async Task Subscribe(string market)
        {
            var webSocket = _engine.GetClientWebSocket();
            if (webSocket == null)
                return;

            await webSocket.SubscribeFtxChannel("orderbook", market);
        }

        public async Task Unsubscribe(string market)
        {
            var webSocket = _engine.GetClientWebSocket();
            if (webSocket == null)
                return;

            await webSocket.UnSubscribeFtxChannel("orderbook", market);
        }

        private async Task Connect(ClientWebSocket webSocket)
        {
            lock (_sync)
            {
                _data.Clear();
            }
            if (_marketList == null)
                await webSocket.SubscribeFtxChannel("markets");
            else
            {
                foreach (var market in _marketList)
                {
                    await webSocket.SubscribeFtxChannel("orderbook", market);
                }
            }
        }

        private async Task Receive(ClientWebSocket webSocket, string msg)
        {
            var packet = JsonConvert.DeserializeObject<FtxWebsocketReceive<FtxOrderBook>>(msg);

            if (packet?.Type == "error")
            {
                _logger.LogError("Receive Error from FTX web Socket: {message}", packet.ErrorMessage);
                return;
            }

            if (packet?.Channel == "markets" && packet.Type == FtxWebsocketReceive.Partial)
            {
                await webSocket.UnSubscribeFtxChannel("markets");

                var markets = JsonConvert.DeserializeObject<FtxWebsocketReceive<DataAction<Dictionary<string, MarketState>>>>(msg);

                foreach (var market in markets.Data.Data.Keys)
                {
                    await webSocket.SubscribeFtxChannel("orderbook", market);
                }
            }

            if (packet?.Channel == "orderbook" && (packet.Type == FtxWebsocketReceive.Partial || packet.Type == FtxWebsocketReceive.Update))
            {
                var orderBook = packet.Data;
                orderBook.id = packet.Market;

                lock (_sync)
                {
                    if (packet.Type == FtxWebsocketReceive.Partial)
                    {
                        _data[orderBook.id] = orderBook;
                    }

                    if (packet.Type == FtxWebsocketReceive.Update)
                    {
                        if (!_data.TryGetValue(orderBook.id, out var book))
                        {
                            _logger.LogError("Receive update for {symbol}, but do not found book in cash", orderBook.id);
                            
                        }
                        else
                        {
                            foreach (var level in packet.Data.asks)
                            {
                                if (level.Length != 2)
                                    continue;

                                var index = book.asks.FindIndex(e => Equals(e.GetFtxOrderBookPrice(), level[0]));

                                if (index >= 0)
                                {
                                    book.asks.RemoveAt(index);
                                }
                                
                                if (level.GetFtxOrderBookVolume() != 0)
                                {
                                    book.asks.Add(level);
                                }
                            }

                            foreach (var level in packet.Data.bids)
                            {
                                if (level.Length != 2)
                                    continue;

                                var index = book.bids.FindIndex(e => Equals(e.GetFtxOrderBookPrice(), level[0]));

                                if (index >= 0)
                                {
                                    book.bids.RemoveAt(index);
                                }

                                if (level.GetFtxOrderBookVolume() != 0)
                                {
                                    book.bids.Add(level);
                                }
                            }

                            book.checksum = packet.Data.checksum;
                            book.time = packet.Data.time;
                        }
                    }
                }

                await OnReceiveUpdates(packet.Data);
            }
        }

        private async Task SendPing(ClientWebSocket webSocket)
        {
            await webSocket.SendFtxPing();
        }

        private async Task OnReceiveUpdates(FtxOrderBook orderBook)
        {
            try
            {
                var action = ReceiveUpdates;
                if (action != null)
                    await action.Invoke(orderBook);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception from method OnReceiveUpdates from client code");
            }
        }
    }
}