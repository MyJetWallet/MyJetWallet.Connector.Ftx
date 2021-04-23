using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using FtxApi;
using FtxApi.Enums;
using Microsoft.Extensions.Logging;
using MyJetWallet.Connector.Ftx.WebSocket;
using MyJetWallet.Connector.Ftx.WebSocket.Models;
using MyJetWallet.Connector.Ftx.WsEngine;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TestApp
{
    class Program
    {
        private static object consoleLock = new object();

        private const int receiveChunkSize = 1024*1024*5;

        private const bool verbose = false;

        static async Task Main(string[] args)
        {
            TestWebSocket();

            Console.WriteLine(Environment.GetEnvironmentVariable("API-SECRET"));
            Console.WriteLine(Environment.GetEnvironmentVariable("API-KEY"));




            //await TestRestApi();
        }

        private static async Task TestRestApi()
        {
            var client = new Client(Environment.GetEnvironmentVariable("API-KEY"),
                Environment.GetEnvironmentVariable("API-SECRET"));
            var api = new FtxRestApi(client);

            Console.WriteLine(" ====  account info ==== ");
            var info = await api.GetAccountInfoAsync();
            Console.WriteLine(JsonSerializer.Serialize(info, new JsonSerializerOptions() {WriteIndented = true}));
            Console.WriteLine();
            Console.WriteLine();

            var takerFee = info.Result.TakerFee;

            //Console.WriteLine(" ====  market info ==== ");
            //var markets = await api.GetMarketsAsync();
            //Console.WriteLine(JsonSerializer.Serialize(markets, new JsonSerializerOptions() { WriteIndented = true }));

            //Console.WriteLine();
            //Console.WriteLine();

            Console.WriteLine(" ====  order 1 ==== ");
            var order1 = await api.GetOrderStatusAsync("39134620341");
            Console.WriteLine(JsonSerializer.Serialize(order1, new JsonSerializerOptions() {WriteIndented = true}));

            Console.WriteLine();
            Console.WriteLine();


            Console.WriteLine(" ====  balances ==== ");
            var balances = await api.GetBalancesAsync();
            Console.WriteLine(JsonSerializer.Serialize(balances, new JsonSerializerOptions() {WriteIndented = true}));

            Console.WriteLine();
            Console.WriteLine();


            var newOrderClientId = "my-order-21";
            var size = 10m;
            //var side = SideType.buy;

            //var volume = side == SideType.buy ? size * (1m + takerFee) : ;

            Console.WriteLine($" ====  place: {newOrderClientId} ==== ");
            var marketOrder =
                await api.PlaceOrderAsync("XRP/USD", SideType.buy, 0, OrderType.market, size, newOrderClientId, true);
            Console.WriteLine(JsonSerializer.Serialize(marketOrder, new JsonSerializerOptions() {WriteIndented = true}));

            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine($" ====  cancel order: {newOrderClientId} ==== ");
            var cancel = await api.CancelOrderByClientIdAsync(newOrderClientId);
            Console.WriteLine(JsonSerializer.Serialize(cancel, new JsonSerializerOptions() {WriteIndented = true}));

            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine($" ====  order: {newOrderClientId} ==== ");
            var order3 = await api.GetOrderStatusByClientIdAsync(newOrderClientId);
            Console.WriteLine(JsonSerializer.Serialize(order3, new JsonSerializerOptions() {WriteIndented = true}));

            Console.WriteLine();
            Console.WriteLine();


            Console.WriteLine(" ====  balances ==== ");
            var balances2 = await api.GetBalancesAsync();
            Console.WriteLine($"{balances.Result.FirstOrDefault(e => e.Coin == "XRP")?.Total} XRP");
            Console.WriteLine($"{balances2.Result.FirstOrDefault(e => e.Coin == "XRP")?.Total} XRP");
            Console.WriteLine("-------");
            Console.WriteLine($"{balances.Result.FirstOrDefault(e => e.Coin == "FTT")?.Total} FTT");
            Console.WriteLine($"{balances2.Result.FirstOrDefault(e => e.Coin == "FTT")?.Total} FTT");

            Console.WriteLine();
            Console.WriteLine();


            Console.WriteLine($" ====  fills ==== ");
            var fills = await api.GetFillsAsync(10, DateTime.UtcNow.Date);
            Console.WriteLine(JsonSerializer.Serialize(fills, new JsonSerializerOptions() {WriteIndented = true}));

            Console.WriteLine();
            Console.WriteLine();
        }

        private static void TestWebSocket()
        {
            using ILoggerFactory loggerFactory =
                LoggerFactory.Create(builder =>
                    builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    }));

            ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

            //UseWebSocket(logger);

            //UseFtxWsMarkets(loggerFactory);

            UseFtxWsOrderBooks(loggerFactory);

            //UseFtxWsPrices(loggerFactory);
        }

        private static void UseFtxWsPrices(ILoggerFactory loggerFactory)
        {
            var client = new FtxWsPrices(loggerFactory.CreateLogger<FtxWsPrices>(), new[] { "BTC/USD" });
            //client.ReceiveUpdates += ticker =>
            //{
            //    Console.WriteLine(JsonConvert.SerializeObject(ticker));
            //    return Task.CompletedTask;
            //};
            client.Start();

            var cmd = Console.ReadLine();
            while (cmd != "exit")
            {
                var price = client.GetMarketStateById("BTC/USD");
                Console.WriteLine(JsonConvert.SerializeObject(price));
                Console.WriteLine(price.GetTime());
                cmd = Console.ReadLine();
            }
        }

        private static void UseFtxWsOrderBooks(ILoggerFactory loggerFactory)
        {
            var client = new FtxWsOrderBooks(loggerFactory.CreateLogger<FtxWsOrderBooks>(), new []{ "BTC/USD" });

            client.Start();

            bool log = true;

            client.ReceiveUpdates = book =>
            {
                if (log)
                    Console.WriteLine($"Receive updates for {book.id}");

                log = false;

                return Task.CompletedTask;
            };

            var cmd = Console.ReadLine();
            while (cmd != "exit")
            {
                if (cmd == "count")
                {
                    var books = client.GetOrderBooks().Count;
                    Console.WriteLine($"Count books: {books}");
                }
                else if (cmd == "reset")
                {
                    client.Reset("BTC/USD").Wait();
                }
                else if (cmd == "time")
                {
                    var book = client.GetOrderBookById("BTC/USD");

                    Console.WriteLine($"nw: {DateTimeOffset.UtcNow:O}");
                    Console.WriteLine($"t1: {book.GetTime():O}");




                    client.Reset("BTC/USD").Wait();
                }
                else
                {
                    var orderBook = client.GetOrderBookById(cmd);

                    if (orderBook != null)
                        Console.WriteLine($"{orderBook.id} {orderBook.GetTime():O}  {orderBook.asks.Count}|{orderBook.bids.Count}");
                    else
                        Console.WriteLine("Not found");
                }

                cmd = Console.ReadLine();
            }

        }


        private static void UseFtxWsMarkets(ILoggerFactory loggerFactory)
        {
            var marketStateClient = new FtxWsMarkets(loggerFactory.CreateLogger<FtxWsMarkets>());

            marketStateClient.ReceiveUpdates = list =>
            {
                Console.WriteLine($"Receive updates for {list.Count} markets");
                return Task.CompletedTask;
            };

            marketStateClient.Start();

            var cmd = Console.ReadLine();
            while (cmd != "exit")
            {
                if (cmd == "all")
                {
                    var market = marketStateClient.GetMarketState();
                    Console.WriteLine($"Count spot markets {market.Count(e => e.type == MarketState.SpotType)}, future markets {market.Count(e => e.type == MarketState.FutureType)}");
                }
                if (cmd.StartsWith("market"))
                {
                    var prm = cmd.Split(' ');
                    if (prm.Length != 2)
                        continue;

                    var asset = prm[1];

                    var markets = marketStateClient.GetMarketState().Where(e => e.baseCurrency == asset || e.quoteCurrency == asset);

                    foreach (var market in markets)
                    {
                        Console.WriteLine($"{market.id} {market.type} {market.enabled}");
                    }
                }
                else
                {
                    var market = marketStateClient.GetMarketStateById(cmd);
                    if (market == null)
                        Console.WriteLine("Not found");
                    else
                        Console.WriteLine(JsonConvert.SerializeObject(market, Formatting.Indented));
                }

                cmd = Console.ReadLine();
            }

            marketStateClient.Stop();
        }

        private static void UseWebSocket(ILogger<Program> logger)
        {
            var manager = new WebsocketEngine("FTX", "wss://ftx.com/ws/", 1000, 3000, logger);

            manager.OnConnect = ConnectFtx;
            manager.SendPing = SendPingFtx;
            manager.OnReceive = ReceiveFromFtx;

            manager.Start();

            var cmd = Console.ReadLine();
            while (cmd != "exit")
            {
                cmd = Console.ReadLine();
            }

            manager.Stop();
            manager.Stop();
            manager.Stop();
        }

        private static async Task ReceiveFromFtx(ClientWebSocket webSocket, string msg)
        {
            var packetType = JsonConvert.DeserializeObject<FtxWebsocketReceive>(msg);

            if (packetType.Channel == "markets" && packetType.Type != "subscribed")
            {
                var packet = JsonConvert.DeserializeObject<FtxWebsocketReceive<DataAction<Dictionary<string, MarketState>>>>(msg);

                Console.WriteLine($"Market state {packet.Type}. Count Markets: {packet.Data.Data.Count}");

                foreach (var type in packet.Data.Data.Values.Select(e => e.type).Distinct())
                {
                    var count = packet.Data.Data.Values.Count(e => e.type == type);
                    Console.WriteLine($"{type} : {count}");
                }

                foreach (var str in packet.Data.Data.Values.Where(e => e.future?.moveStart != null))
                {
                    Console.WriteLine($"{str.name} : {str.future.moveStart}");
                }

                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"Receive packet in channel {packetType.Channel}, type: {packetType.Type}");
                Console.WriteLine(msg);
                Console.WriteLine();
                Console.WriteLine();
            }
        }

        private static async Task SendPingFtx(ClientWebSocket webSocket)
        {
            await webSocket.SendFtxPing();
        }

        private static async Task ConnectFtx(ClientWebSocket webSocket)
        {
            //await webSocket.SubscribeFtxChannel("markets", "BTC/USD");

            //await webSocket.SubscribeFtxChannel("ticker", "BTC/USD");


            await webSocket.SubscribeFtxChannel("orderbook", "BTC/USD");
        }
    }


    public class ResultId
    {
        public Data result { get; set; }

        public class Data
        {
            public string id { get; set; }
        }
    }

}

/*
FTX read only

api key
WJEd1LCFNiwlq7sDwjpLkyUJ0xn002ikImua9gkJ

api secret
Ru2czn54hVMy6-QCrmQRCTh6T7IwcIaAW-57FEY5
 */
