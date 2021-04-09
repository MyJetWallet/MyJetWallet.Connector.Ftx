using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyJetWallet.Connector.Ftx.Rest;
using MyJetWallet.Connector.Ftx.WebSocket;
using MyJetWallet.Connector.Ftx.WebSocket.Models;
using MyJetWallet.Connector.Ftx.WsEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonConverter = Newtonsoft.Json.JsonConverter;
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
            //TestWebSocket();

            Console.WriteLine(Environment.GetEnvironmentVariable("API-SECRET"));
            Console.WriteLine(Environment.GetEnvironmentVariable("API-KEY"));


            var client = new FtxClient(Environment.GetEnvironmentVariable("API-SECRET"), Environment.GetEnvironmentVariable("API-KEY"));

            var account = await client.GetAccount();


            var acc = JObject.Parse(account);

            Console.WriteLine(acc.ToString(Formatting.Indented));

            Console.WriteLine();
            Console.WriteLine();

            var balance = await client.GetWalletBalance();


            Console.WriteLine(JsonConvert.SerializeObject(balance, Formatting.Indented));

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
        }

        private static void UseFtxWsOrderBooks(ILoggerFactory loggerFactory)
        {
            var client = new FtxWsOrderBooks(loggerFactory.CreateLogger<FtxWsOrderBooks>(), new []{ "BTC/USD" });

            client.Start();

            bool log = true;

            client.ReceiveUpdates = book =>
            {
                Console.WriteLine($"t1: {book.GetTime().UtcDateTime:O}");
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
                        Console.WriteLine(JsonConvert.SerializeObject(orderBook, Formatting.Indented));
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


}

/*
FTX read only

api key
WJEd1LCFNiwlq7sDwjpLkyUJ0xn002ikImua9gkJ

api secret
Ru2czn54hVMy6-QCrmQRCTh6T7IwcIaAW-57FEY5
 */
