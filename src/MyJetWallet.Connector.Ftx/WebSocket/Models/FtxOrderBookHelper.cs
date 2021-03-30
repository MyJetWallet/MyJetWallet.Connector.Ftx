namespace MyJetWallet.Connector.Ftx.WebSocket.Models
{
    public static class FtxOrderBookHelper
    {
        public static double? GetFtxOrderBookPrice(this double?[] array)
        {
            if (array.Length < 1)
            {
                return null;
            }

            return array[0];
        }

        public static double? GetFtxOrderBookVolume(this double?[] array)
        {
            if (array.Length < 2)
            {
                return null;
            }

            return array[1];
        }
    }
}