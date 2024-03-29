﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FtxApi.Enums;
using FtxApi.Models;
using FtxApi.Models.LeveragedTokens;
using FtxApi.Models.Markets;

namespace FtxApi
{
    public class FtxRestApi
    {
        private const string Url = "https://ftx.com/";

        private readonly Client _client;

        private readonly HttpClient _httpClient;

        private readonly HMACSHA256 _hashMaker;

        private long _nonce;
        
        private readonly string _urlEncodedSubAccount;

        public FtxRestApi(Client client)
        {
            _client = client;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Url),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(_client.ApiSecret));
            
            if (!string.IsNullOrEmpty(_client.SubAccount))
            {
                _urlEncodedSubAccount = Uri.EscapeDataString(_client.SubAccount);
            }
        }

        #region Coins

        public async Task<FtxResult<List<Coin>>> GetCoinsAsync()
        {
            var resultString = $"api/coins";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<List<Coin>>>(result);
        }

        #endregion

        #region Futures

        public async Task<FtxResult<List<Future>>> GetAllFuturesAsync()
        {
            var resultString = $"api/futures";

            var result = await CallAsync(HttpMethod.Get, resultString);
            return JsonSerializer.Deserialize<FtxResult<List<Future>>>(result);
        }

        public async Task<FtxResult<Future>> GetFutureAsync(string future)
        {
            var resultString = $"api/futures/{future}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<Future>>(result);
        }

        public async Task<FtxResult<FutureStats>> GetFutureStatsAsync(string future)
        {
            var resultString = $"api/futures/{future}/stats";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<FutureStats>>(result);
        }

        public async Task<List<FundingRate>> GetFundingRatesAsync(DateTime start, DateTime end)
        {
            List<FundingRate> allResults = new List<FundingRate>();
            int resultLength;

            do
            {
                var resultString = $"api/funding_rates?start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";
                var result = await CallAsync(HttpMethod.Get, resultString);
                var deserializedResult = JsonSerializer.Deserialize<FtxResult<List<FundingRate>>>(result);

                var rates = deserializedResult.Result;
                resultLength = rates.Count();

                if(resultLength != 0)
                {
                    allResults.AddRange(rates);
                    end = rates.Last().Time.ToUniversalTime().AddMinutes(-1); //Set the end time to the earliest retrieved to get more
                }
            } while(resultLength == 500);
            return allResults;
        }

        public async Task<FtxResult<List<Candle>>> GetHistoricalPricesAsync(string futureName, int resolution, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/futures/{futureName}/mark_candles?resolution={resolution}&limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<List<Candle>>>(result);
        }

        #endregion

        #region Markets

        public async Task<FtxResult<List<Market>>> GetMarketsAsync()
        {
            var resultString = $"api/markets";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<List<Market>>>(result);
        }

        public async Task<FtxResult<Market>> GetSingleMarketsAsync(string marketName)
        {
            var resultString = $"api/markets/{marketName}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<Market>>(result);
        }

        public async Task<FtxResult<Orderbook>> GetMarketOrderBookAsync(string marketName, int depth = 20)
        {
            var resultString = $"api/markets/{marketName}/orderbook?depth={depth}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<Orderbook>>(result);
        }

        public async Task<FtxResult<List<Trade>>> GetMarketTradesAsync(string marketName, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/markets/{marketName}/trades?limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<List<Trade>>>(result);
        }

        #endregion

        #region Account

        public async Task<FtxResult<AccountInfo>> GetAccountInfoAsync()
        {
            var resultString = $"api/account";
            var sign = GenerateSignature(HttpMethod.Get, "/api/account", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<AccountInfo>>(result);
        }

        public async Task<FtxResult<List<Position>>> GetPositionsAsync()
        {
            var resultString = $"api/positions";
            var sign = GenerateSignature(HttpMethod.Get, "/api/positions", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Position>>>(result);
        }

        public async Task<AccountLeverage> ChangeAccountLeverageAsync(int leverage)
        {
            var resultString = $"api/account/leverage";
         
            var body = $"{{\"leverage\": {leverage}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/account/leverage", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return JsonSerializer.Deserialize<AccountLeverage>(result);
        }

        #endregion

        #region Wallet

        public async Task<FtxResult<List<Coin>>> GetCoinAsync()
        {
            var resultString = $"api/wallet/coins";
           
            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/coins", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Coin>>>(result);
        }

        public async Task<FtxResult<List<Balance>>> GetBalancesAsync()
        {
            var resultString = $"api/wallet/balances";

            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/balances", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Balance>>>(result);
        }

        public async Task<FtxResult<DepositAddress>> GetDepositAddressAsync(string coin)
        {
            var resultString = $"api/wallet/deposit_address/{coin}";
            
            var sign = GenerateSignature(HttpMethod.Get, $"/api/wallet/deposit_address/{coin}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<DepositAddress>>(result);
        }

        public async Task<FtxResult<List<DepositHistory>>> GetDepositHistoryAsync()
        {
            var resultString = $"api/wallet/deposits";

            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/deposits", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<DepositHistory>>>(result);
        }

        public async Task<FtxResult<List<WithdrawalHistory>>> GetWithdrawalHistoryAsync()
        {
            var resultString = $"api/wallet/withdrawals";

            var sign = GenerateSignature(HttpMethod.Get, "/api/wallet/withdrawals", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<WithdrawalHistory>>>(result);
        }

        public async Task<FtxResult<List<WithdrawalHistory>>> GetWithdrawalHistoryAsync(DateTime start, DateTime end)
        {
            var resultString = $"api/wallet/withdrawals?start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");
            
            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<WithdrawalHistory>>>(result);
        }
        
        public async Task<FtxResult<WithdrawalHistory>> RequestWithdrawalAsync(string coin, decimal size, string addr, string tag, string pass, string code)
        {
            var resultString = $"api/wallet/withdrawals";

            var body = $"{{"+
                $"\"coin\": \"{coin}\"," +
                $"\"size\": {size},"+
                $"\"address\": \"{addr}\","+
                $"\"tag\": {tag},"+
                $"\"password\": \"{pass}\","+
                $"\"code\": {code}" +
                "}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/wallet/withdrawals", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return JsonSerializer.Deserialize<FtxResult<WithdrawalHistory>>(result);
        }

        #endregion

        #region Orders

        public async Task<FtxResult<Order>> PlaceOrderAsync(string instrument, SideType side, decimal price, OrderType orderType, decimal amount, string clientId=null, bool ioc = false, bool reduceOnly = false)
        {
            var path = $"api/orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side}\"," +
                $"\"price\": {price}," +
                $"\"type\": \"{orderType}\"," +
                $"\"size\": {amount}," +
                $"\"clientId\": \"{clientId}\"," +
                $"\"ioc\": {ioc.ToString().ToLower()}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return JsonSerializer.Deserialize<FtxResult<Order>>(result);
        }

        public async Task<FtxResult<TriggerOrder>> PlaceStopOrderAsync(string instrument, SideType side, decimal triggerPrice, decimal amount, bool reduceOnly = false)
        {
            var path = $"api/conditional_orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side}\"," +
                $"\"triggerPrice\": {triggerPrice}," +
                $"\"type\": \"stop\"," +
                $"\"size\": {amount}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/conditional_orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return JsonSerializer.Deserialize<FtxResult<TriggerOrder>>(result);
        }

        public async Task<FtxResult<TriggerOrder>> PlaceTrailingStopOrderAsync(string instrument, SideType side, decimal trailValue, decimal amount, bool reduceOnly = false)
        {
            var path = $"api/conditional_orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side}\"," +
                $"\"trailValue\": {trailValue}," +
                $"\"type\": \"trailingStop\"," +
                $"\"size\": {amount}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/conditional_orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return JsonSerializer.Deserialize<FtxResult<TriggerOrder>>(result);
        }

        public async Task<FtxResult<TriggerOrder>> PlaceTakeProfitOrderAsync(string instrument, SideType side, decimal triggerPrice, decimal amount, bool reduceOnly = false)
        {
            var path = $"api/conditional_orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side}\"," +
                $"\"triggerPrice\": {triggerPrice}," +
                $"\"type\": \"takeProfit\"," +
                $"\"size\": {amount}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/conditional_orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return JsonSerializer.Deserialize<FtxResult<TriggerOrder>>(result);
        }
        
        public async Task<FtxResult<List<Order>>> GetOrdersAsync(string instrument)
        {
            var path = $"api/orders?market={instrument}";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/orders?market={instrument}", "");
           
            var result = await CallAsyncSign(HttpMethod.Get, path, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Order>>>(result);
        }

        public async Task<FtxResult<List<Order>>> GetOpenOrdersAsync(string instrument)
        {
            var path = $"api/orders?market={instrument}";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/orders?market={instrument}", "");
           
            var result = await CallAsyncSign(HttpMethod.Get, path, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Order>>>(result);
        }

        public async Task<FtxResult<OrderStatus>> GetOrderStatusAsync(string id)
        {
            var resultString = $"api/orders/{id}";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/orders/{id}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<OrderStatus>>(result);
        }

        public async Task<FtxResult<Order>> GetOrderStatusByClientIdAsync(string clientOrderId)
        {
            var resultString = $"api/orders/by_client_id/{clientOrderId}";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/orders/by_client_id/{clientOrderId}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<Order>>(result);
        }

        public async Task<FtxResult<string>> CancelOrderAsync(string id)
        {
            var resultString = $"api/orders/{id}";

            var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders/{id}", "");

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<string>>(result);
        }

        public async Task<FtxResult<string>> CancelOrderByClientIdAsync(string clientOrderId)
        {
            var resultString = $"api/orders/by_client_id/{clientOrderId}";

            var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders/by_client_id/{clientOrderId}", "");

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<string>>(result);
        }

        public async Task<FtxResult<string>> CancelAllOrdersAsync(string instrument)
        {
            var resultString = $"api/orders";

            var body = $"{{\"market\": \"{instrument}\"}}";

            var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders", body);

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign, body);

            return JsonSerializer.Deserialize<FtxResult<string>>(result);
        }

        #endregion

        #region Fills

        public async Task<FtxResult<List<Fill>>> GetFillsAsync(string market, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/fills?market={market}&limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Fill>>>(result);
        }

        public async Task<FtxResult<List<Fill>>> GetFillsAsync(int limit, DateTime start)
        {
            var resultString = $"api/fills?limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Fill>>>(result);
        }
        
        public async Task<FtxResult<List<Fill>>> GetFillsAsync(DateTime start, DateTime end)
        {
            var resultString = $"api/fills?start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Fill>>>(result);
        }
        
        public async Task<FtxResult<List<Fill>>> GetFillsAsync(decimal orderId)
        {
            var resultString = $"api/fills?orderId={orderId}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<Fill>>>(result);
        }

        #endregion

        #region Funding

        public async Task<FtxResult<List<FundingPayment>>> GetFundingPaymentAsync(DateTime start, DateTime end)
        {
            var resultString = $"api/funding_payments?start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var sign = GenerateSignature(HttpMethod.Get, $"/{resultString}", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<FundingPayment>>>(result);
        }

        #endregion

        #region Leveraged Tokens

        public async Task<FtxResult<List<LeveragedToken>>> GetLeveragedTokensListAsync()
        {
            var resultString = $"api/lt/tokens";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<List<LeveragedToken>>>(result);
        }

        public async Task<FtxResult<LeveragedToken>> GetTokenInfoAsync(string tokenName)
        {
            var resultString = $"api/lt/{tokenName}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return JsonSerializer.Deserialize<FtxResult<LeveragedToken>>(result);
        }

        public async Task<FtxResult<List<LeveragedTokenBalance>>> GetLeveragedTokenBalancesAsync()
        {
            var resultString = $"api/lt/balances";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/lt/balances", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<LeveragedTokenBalance>>>(result);
        }

        public async Task<FtxResult<List<LeveragedTokenCreation>>> GetLeveragedTokenCreationListAsync()
        {
            var resultString = $"api/lt/creations";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/lt/creations", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<LeveragedTokenCreation>>>(result);
        }

        public async Task<FtxResult<LeveragedTokenCreationRequest>> RequestLeveragedTokenCreationAsync(string tokenName, decimal size)
        {
            var resultString = $"api/lt/{tokenName}/create";
          
            var body = $"{{\"size\": {size}}}";

            var sign = GenerateSignature(HttpMethod.Post, $"/api/lt/{tokenName}/create", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return JsonSerializer.Deserialize<FtxResult<LeveragedTokenCreationRequest>>(result);
        }

        public async Task<FtxResult<List<LeveragedTokenRedemptionRequest>>> GetLeveragedTokenRedemptionListAsync()
        {
            var resultString = $"api/lt/redemptions";

            var sign = GenerateSignature(HttpMethod.Get, $"/api/lt/redemptions", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return JsonSerializer.Deserialize<FtxResult<List<LeveragedTokenRedemptionRequest>>>(result);
        }

        public async Task<FtxResult<LeveragedTokenRedemption>> RequestLeveragedTokenRedemptionAsync(string tokenName, decimal size)
        {
            var resultString = $"api/lt/{tokenName}/redeem";

            var body = $"{{\"size\": {size}}}";

            var sign = GenerateSignature(HttpMethod.Post, $"/api/lt/{tokenName}/redeem", body);

            var result = await CallAsyncSign(HttpMethod.Post, resultString, sign, body);

            return JsonSerializer.Deserialize<FtxResult<LeveragedTokenRedemption>>(result);
        }

        #endregion

        #region Util

        private async Task<string> CallAsync(HttpMethod method, string endpoint, string body = null)
        {
            var request = new HttpRequestMessage(method, endpoint);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return result;
        }

        private async Task<string> CallAsyncSign(HttpMethod method, string endpoint, string sign, string body = null)
        {
            var request = new HttpRequestMessage(method, endpoint);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            request.Headers.Add("FTX-KEY", _client.ApiKey);
            request.Headers.Add("FTX-SIGN", sign);
            request.Headers.Add("FTX-TS", _nonce.ToString());
            
            if (!string.IsNullOrEmpty(_urlEncodedSubAccount))
            {
                request.Headers.Add("FTX-SUBACCOUNT", _urlEncodedSubAccount);
            }

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return result;
        }

        private string GenerateSignature(HttpMethod method, string url, string requestBody)
        {
            _nonce = GetNonce();
            var signature = $"{_nonce}{method.ToString().ToUpper()}{url}{requestBody}";
            var hash = _hashMaker.ComputeHash(Encoding.UTF8.GetBytes(signature));
            var hashStringBase64 = BitConverter.ToString(hash).Replace("-", string.Empty);
            return hashStringBase64.ToLower();
        }

        private long GetNonce()
        {
            return Util.Util.GetMillisecondsFromEpochStart();
        }

        private dynamic ParseResponce(string responce)
        {
            return (dynamic)responce;
        }


        #endregion
    }
}