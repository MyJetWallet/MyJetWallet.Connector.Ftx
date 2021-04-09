namespace MyJetWallet.Connector.Ftx.Rest.Requests
{
    public class PostSpotMarginOffersRequest
    {
        public string coin { get; set; }
        public float size { get; set; }

        public float rate { get; set; }
    }
}