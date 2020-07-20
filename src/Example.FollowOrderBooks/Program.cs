using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lykke.HftApi.ApiContract;
using TradingApi.Client;

namespace Example.FollowOrderBooks
{
    class Program
    {
        private static Dictionary<decimal, string> _ask = new Dictionary<decimal, string>();
        private static Dictionary<decimal, string> _bid = new Dictionary<decimal, string>();

        static async Task Main(string[] args)
        {
            try
            {
                string filter = ReadFilterFromConsole();
                
                var client = new TradingApiClient("https://hft-apiv2-grpc.lykke.com", "");


                UpdateThread(client, filter);

                await PrintThread();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static async Task PrintThread()
        {
            while (true)
            {
                Console.Clear();
                var data = _ask.ToArray().OrderBy(i => i.Key).ToArray().Take(10).OrderByDescending(i => i.Key);
                foreach (var item in data)
                {
                    Console.WriteLine($"{item.Value}\t\t{item.Key}");
                }

                Console.WriteLine();
                foreach (var item in _bid.ToArray().OrderByDescending(i => i.Key).ToArray().Take(10))
                {
                    Console.WriteLine($"\t\t{item.Key}\t\t{item.Value}");
                }

                await Task.Delay(2000);
            }
        }

        static async Task UpdateThread(TradingApiClient client, string filter)
        {
            var reqiest = new OrderbookUpdatesRequest();
            reqiest.AssetPairId = filter;
            var stream = client.PublicApi.GetOrderbookUpdates(reqiest);

            var token = new CancellationToken();
            while (await stream.ResponseStream.MoveNext(token))
            {
                var update = stream.ResponseStream.Current;

                foreach (Orderbook.Types.PriceVolume ask in update.Asks)
                {
                    var price = decimal.Parse(ask.P);
                    if (ask.V == "0")
                        _ask.Remove(price);
                    else
                        _ask[price] = ask.V;
                }

                foreach (Orderbook.Types.PriceVolume bid in update.Bids)
                {
                    var price = decimal.Parse(bid.P);
                    if (bid.V == "0")
                        _bid.Remove(price);
                    else
                        _bid[price] = bid.V;
                }
            }
        }

        private static string ReadFilterFromConsole()
        {
            Console.Write("Asset pair: ");
            var pair = Console.ReadLine();
            return pair;
        }
    }
}
