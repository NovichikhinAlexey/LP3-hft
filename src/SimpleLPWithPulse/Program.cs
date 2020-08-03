using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using SimpleLP;
using SimpleLP.Domain;

namespace SimpleLPWithPulse
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var key = Environment.GetEnvironmentVariable("HFT_API_KEY");
            var api = new ApiTrader("https://hft-apiv2-grpc.lykke.com", key);

            var trader = new MarketManager(api, "ETHUSD");

            await trader.ResetMarke(350, 0.001m, 1, 50,
                0.1m, 4, 
                0m, 0);

            await trader.PlaceToMarketAsync();

            var tradeReader = TradeReader(api, trader);

            var pulser = Pulser(trader);

            var cmd = Console.ReadLine();
            while (cmd != "exit")
            {
                cmd = Console.ReadLine();
            }
        }

        private static async Task TradeReader(ApiTrader api, MarketManager trader)
        {
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
                            await trader.HandleTrade(trade.OrderId, decimal.Parse(trade.BaseVolume));
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

        private static async Task Pulser(MarketManager trader)
        {
            while (true)
            {
                try
                {
                    await trader.Pulse();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: PULSER receve exception");
                    Console.WriteLine(ex);
                }

                await Task.Delay(5000);
            }
        }

        static async Task MainMock(string[] args)
        {

            var api = new Mock();

            var trader = new MarketManager(api, "BTCUSD");

            await trader.ResetMarke(10500, 0.0001m, 10, 10,
                1m, 4, 0m, 0);

            await trader.PlaceToMarketAsync();

            Console.WriteLine();
            Console.WriteLine("-------------------------");
            Console.WriteLine();
            Console.ReadLine();
            await trader.Pulse();

            Console.WriteLine();
            Console.WriteLine("-------------------------");
            Console.WriteLine();
            Console.ReadLine();
            await trader.Pulse();

            Console.WriteLine();
            Console.WriteLine("-------------------------");
            Console.Write("orderId: ");
            var oid = Console.ReadLine();
            Console.Write("size: ");
            var sz = decimal.Parse(Console.ReadLine());
            
            await trader.HandleTrade(oid, sz);
            await trader.PlaceToMarketAsync();
            await trader.Pulse();

            Console.WriteLine();
            Console.WriteLine("-------------------------");
            Console.Write("orderId: ");
            oid = Console.ReadLine();
            Console.Write("size: ");
            sz = decimal.Parse(Console.ReadLine());

            await trader.HandleTrade(oid, sz);
            await trader.PlaceToMarketAsync();
            await trader.Pulse();

            Console.WriteLine();
            Console.WriteLine("-------------------------");
            Console.Write("orderId: ");
            oid = Console.ReadLine();
            Console.Write("size: ");
            sz = decimal.Parse(Console.ReadLine());

            await trader.HandleTrade(oid, sz);
            await trader.PlaceToMarketAsync();
            await trader.Pulse();


        }
    }

    public class Mock: ITrader
    {
        private Dictionary<string, decimal> _orders = new Dictionary<string, decimal>();
        public async Task<string> PlaceOrderAsync(string symbol, decimal price, decimal size, MarketSide side)
        {
            var orderId = Guid.NewGuid().ToString();
            _orders.Add(orderId, size);
            return orderId;
        }

        public async Task<bool> CheckOrderIsOpenedAsync(string orderId, string symbol)
        {
            return _orders.ContainsKey(orderId);
        }

        public async Task CancelOrder(string orderId)
        {
            _orders.Remove(orderId);
        }

        public async Task CancelAllOrders(string assetPair)
        {
            _orders.Clear();
        }

        public async Task<decimal?> GetRemainingSizeOfActiveOrder(string orderId, string symbol)
        {
            if (_orders.TryGetValue(orderId, out var size))
            {
                return size;
            }

            return null;
        }

        public async Task<string> RePlaceOrderAsync(string orderId, string symbol, decimal price, decimal size, MarketSide side)
        {
            if (_orders.TryGetValue(orderId, out var oldSize))
            {
                await CancelOrder(orderId);
                return await PlaceOrderAsync(symbol, price, size, side);
            }

            return null;
        }
    }
}
