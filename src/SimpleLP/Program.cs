using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lykke.HftApi.ApiContract;
using SimpleLP.Domain;
using TradingApi.Client;

namespace SimpleLP
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var key = Environment.GetEnvironmentVariable("HFT_API_KEY");
            var api = new ApiTrader("https://hft-apiv2-grpc.lykke.com", key);

            var trader = new MarketManager(api, "BTCUSD");

            trader.ResetMarke(10940, 0.0001m, 15, 15);
            await trader.PlaceToMarketAsync();

            while (true)
            {
                try
                {
                    var tradeStream = api.Client.PrivateApi.GetTradeUpdates(new Empty());

                    while (await tradeStream.ResponseStream.MoveNext())
                    {
                        foreach (var trade in tradeStream.ResponseStream.Current.Trades)
                        {
                            Console.WriteLine(
                                $"TRADE: orderId: {trade.OrderId}, size: {trade.BaseVolume}, role: {trade.Role}");
                            await trader.HandleTrade(trade.OrderId);
                        }

                        await trader.PlaceToMarketAsync();
                    }

                    tradeStream.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error on stream read:");
                    Console.WriteLine(ex);
                }
            }
        }

        static async Task MainMock()
        {
            var mock = new MockTrader();
            var trader = new MarketManager(mock, "BTCUSD");

            trader.ResetMarke(9000, 0.0001m, 5, 10);
            await trader.PlaceToMarketAsync();

            while (true)
            {
                Console.Write("OrderId: ");
                var orderId = Console.ReadLine();

                if (mock.Orders.Contains(orderId))
                {
                    await mock.CancelOrder(orderId);
                    await trader.HandleTrade(orderId);
                    await trader.PlaceToMarketAsync();
                }
            }
        }
    }

    public class MockTrader : ITrader
    {
        public List<string> Orders = new List<string>();

        public async Task<string> PlaceOrderAsync(string symbol, decimal price, decimal size, MarketSide side)
        {
            var orderId = Guid.NewGuid().ToString();
            Orders.Add(orderId);
            return orderId;
        }

        public async Task<bool> CheckOrderIsOpenedAsync(string orderId, string symbol)
        {
            return Orders.Contains(orderId);
        }

        public async Task CancelOrder(string orderId)
        {
            Orders.Remove(orderId);
        }

        public Task CancelAllOrders(string assetPair)
        {
            return Task.CompletedTask;
        }
    }

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
