using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lykke.HftApi.ApiContract;
using TradingApi.Client;

namespace Example.FollowPrices
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                string[] filter = ReadFilterFromConsole();

                var client = new TradingApiClient("https://hft-apiv2-grpc.lykke.com", "");

                await GetPriceSnapshot(filter, client);

                await FollowPriceUpdate(filter, client);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static string[] ReadFilterFromConsole()
        {
            Console.Write("Filter asset pairs (space separator): ");
            var pairsFilter = Console.ReadLine();
            var filter = !string.IsNullOrEmpty(pairsFilter)
                ? pairsFilter.Split(' ')
                : new string[] { };
            return filter;
        }

        private static async Task FollowPriceUpdate(string[] filter, TradingApiClient client)
        {
            var priceUpdateRequest = new PriceUpdatesRequest();
            priceUpdateRequest.AssetPairIds.AddRange(filter);

            Console.WriteLine("Subscribe to prices.");
            var priceStream = client.PublicApi.GetPriceUpdates(priceUpdateRequest);

            var token = new CancellationToken();
            while (await priceStream.ResponseStream.MoveNext(token))
            {
                var update = priceStream.ResponseStream.Current;

                Console.WriteLine($"{update.AssetPairId}  Bid: {update.Bid}  Ask: {update.Ask}  {update.Timestamp}");
            }

            Console.WriteLine("Price stream are closed.");
        }

        private static async Task GetPriceSnapshot(string[] filter, TradingApiClient client)
        {
            var priceRequest = new PricesRequest();
            priceRequest.AssetPairIds.AddRange(filter);

            var prices = await client.PublicApi.GetPricesAsync(priceRequest);

            ValidateResult(prices.Error);
            Console.WriteLine($"Count prices: {prices.Payload.Count}.");

            foreach (var price in prices.Payload)
            {
                Console.WriteLine($"{price.AssetPairId}: Ask={price.Ask}; Bid={price.Bid}; Time={price.Timestamp}");
            }
        }

        private static void GetPrices(TradingApiClient client)
        {
            PricesResponse prices;
            var sw = new Stopwatch();
            sw.Start();

            prices = client.PublicApi.GetPrices(new PricesRequest());
            ValidateResult(prices.Error);

            sw.Stop();

            Console.WriteLine($"Count prices: {prices.Payload.Count}. ExecTime: {sw.ElapsedMilliseconds} ms");
        }


        static void ValidateResult(Error error)
        {
            if (error != null && error.Code != ErrorCode.Success)
            {
                Console.WriteLine($"ERROR: {error.Code}: {error.Message}");
                if (error.Fields != null)
                {
                    foreach (var field in error.Fields)
                    {
                        Console.WriteLine($"   {field.Key}: {field.Value}");
                    }
                }

                throw new Exception();
            }
        }
    }
}





/*
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IndhbGxldCIsImF1ZCI6ImhmdC1hcGl2Mi5seWtrZS5jb20iLCJrZXktaWQiOiIyZDkyYjMyYi1mZGYzLTRhZjgtYTAzZi1iNzNmYzY2MjJmZDQiLCJjbGllbnQtaWQiOiI3OGVmMzU2Ni03NTgzLTQzMzctYmRkNi0zYTQyYmUyOWVmNTEiLCJ3YWxsZXQtaWQiOiJlMmZiNzNjMS1jZGFhLTQ1YWEtYmNhYi1mMDY1NmE4MGVmZGIiLCJuYmYiOjE1ODk5MjA3NzQsImV4cCI6MTkwNTQ1MzU3NCwiaWF0IjoxNTg5OTIwNzc0fQ.rNa2xghIbDOBB55ERMxrWd4nRuv79nOVA0D8KG8uN2I
 */
