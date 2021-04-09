using System.Collections.Generic;

namespace MyJetWallet.Connector.Ftx.Rest.Response
{
    public class ResponseBase<T>
    {
        public List<T> result { get; set; }
        public bool success { get; set; }
        public string error { get; set; }
    }
}