using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using SimpleLP.Domain;

namespace SimpleLP
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var key = Environment.GetEnvironmentVariable("HFT_API_KEY");
            var api = new ApiTrader("https://hft-apiv2-grpc.lykke.com", key);

            var trader = new MarketManager(api, "BTCUSD");

            trader.ResetMarke(10950, 0.0001m, 10, 15);
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
                            Console.WriteLine($"TRADE: orderId: {trade.OrderId}, size: {trade.BaseVolume}, role: {trade.Role}");
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
    }
}
