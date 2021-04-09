using System.Collections.Generic;

namespace MyJetWallet.Connector.Ftx.Rest.Response
{
    public class SpotMarginOfferResponse : ResponseBase<List<SpotMarginOfferDto>>
    {
    }


    public class SpotMarginOfferDto
    {
        public string coin { get; set; }
        public float size { get; set; }
        public float rate { get; set; }
    }
}