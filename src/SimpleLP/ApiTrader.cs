using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Lykke.HftApi.ApiContract;
using SimpleLP.Domain;
using TradingApi.Client;

namespace SimpleLP
{
    public class ApiTrader : ITrader
    {

        public TradingApiClient Client;

        public ApiTrader(string apiEndpoint, string apiKey)
        {
            Client = new TradingApiClient(apiEndpoint, apiKey);
        }


        public async Task<string> PlaceOrderAsync(string symbol, decimal price, decimal size, MarketSide side)
        {
            try
            {
                var request = new LimitOrderRequest()
                {
                    AssetPairId = symbol,
                    Price = price.ToString(CultureInfo.InvariantCulture),
                    Side = side == MarketSide.Long ? Side.Buy : Side.Sell,
                    Volume = size.ToString(CultureInfo.InvariantCulture)
                };
                var result = await Client.PrivateApi.PlaceLimitOrderAsync(request);

                if (result.Error != null)
                {
                    Console.WriteLine($"ERROR: Cannot place limit order: {result.Error.Message}");
                    return string.Empty;
                }

                return result.Payload.OrderId;
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR: Cannot place limit order:");
                Console.WriteLine(ex);
                return string.Empty;
            }
        }

        public async Task<bool> CheckOrderIsOpenedAsync(string orderId, string symbol)
        {
            try
            {
                var request = new OrdersRequest() {AssetPairId = symbol};
                var result = await Client.PrivateApi.GetActiveOrdersAsync(request);


                if (result.Error != null)
                {
                    Console.WriteLine($"ERROR: Cannot get limit orders: {result.Error.Message}");
                    return true;
                }

                return result.Payload.Any(o => o.Id == orderId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Cannot get limit orders:");
                Console.WriteLine(ex);
                return true;
            }
        }

        public async Task CancelOrder(string orderId)
        {
            while (true)
            {
                try
                {
                    var result = await Client.PrivateApi.CancelOrderAsync(new CancelOrderRequest() {OrderId = orderId});

                    if (result.Error != null)
                    {
                        Console.WriteLine($"ERROR: Cannot cancel limit orders: {result.Error.Message}");
                    }

                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Cannot cancel limit orders:");
                    Console.WriteLine(ex);
                }

                await Task.Delay(5000);
            }
        }

        public async Task CancelAllOrders(string assetPair)
        {
            var result = await Client.PrivateApi.CancelAllOrdersAsync(new CancelOrdersRequest() {AssetPairId = assetPair});

            if (result.Error != null)
            {
                Console.WriteLine($"ERROR: Cannot cancel limit orders: {result.Error.Message}");
            }
        }
    }
}
