namespace FtxApi
{
    public class Client
    {
        public string ApiKey { get; }

        public string ApiSecret { get; }
        
        public string SubAccount { get; }

        public Client()
        {
            ApiKey = "";
            ApiSecret = "";
            SubAccount = "";
        }

        public Client(string apiKey, string apiSecret, string subAccount = "")
        {
            ApiKey = apiKey;
            ApiSecret = apiSecret;
            SubAccount = subAccount;
        }
    }
}