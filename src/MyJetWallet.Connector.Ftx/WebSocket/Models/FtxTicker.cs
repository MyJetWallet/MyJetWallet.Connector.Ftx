using System;

namespace MyJetWallet.Connector.Ftx.WebSocket.Models
{
    public class FtxTicker
    {
        public double bid { get; set; }
        public double ask { get; set; }

        public double bidSize { get; set; }
        public double askSize { get; set; }

        public double last { get; set; }

        public double time { get; set; }

        public string id { get; set; }

        public DateTimeOffset GetTime()
        {
            var unixtime = (long)Math.Truncate(time);

            var result = DateTimeOffset.FromUnixTimeSeconds(unixtime);

            return result;
        }
    }
}

//{"bid": 59839.0, "ask": 59840.0, "bidSize": 2.0003, "askSize": 0.1161, "last": 59840.0, "time": 1618247243.5760598}