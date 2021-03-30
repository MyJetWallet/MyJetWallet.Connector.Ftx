// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;

namespace MyJetWallet.Connector.Ftx.WebSocket.Models
{
    public class FtxOrderBook
    {
        public double time { get; set; }

        public long checksum { get; set; }

        public string action { get; set; }

        public List<double?[]> bids { get; set; }

        public List<double?[]> asks { get; set; }

        public string id { get; set; }

        public FtxOrderBook Copy()
        {
            var result = new FtxOrderBook()
            {
                id = id,
                action = action,
                checksum = checksum,
                time = time,
                asks = asks.OrderBy(e => e.GetFtxOrderBookPrice()).ToList(),
                bids = bids.OrderByDescending(e => e.GetFtxOrderBookPrice()).ToList()
            };

            return result;
        }

        public DateTimeOffset GetTime()
        {
            var unixtime = (long)Math.Truncate(time);

            var result = DateTimeOffset.FromUnixTimeSeconds(unixtime);
            
            return result;
        }
    }
}