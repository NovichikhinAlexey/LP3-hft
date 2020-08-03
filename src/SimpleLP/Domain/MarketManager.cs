using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleLP.Domain
{
    public class MarketManager
    {
        private readonly ITrader _trader;
        private readonly string _symbol;
        List<MarketLevel> _levels = new List<MarketLevel>();
        Dictionary<string, MarketLevel> _levelsByOrder = new Dictionary<string, MarketLevel>();

        public MarketManager(ITrader trader, string symbol)
        {
            _trader = trader;
            _symbol = symbol;
        }

        public void ResetMarke(decimal initPrice, decimal levelSize, decimal delta, int countLevelsPerSide)
        {
            _levels.Clear();
            _levelsByOrder.Clear();

            _trader.CancelAllOrders(_symbol);

            var price = initPrice;
            for (int i = 0; i < countLevelsPerSide; i++)
            {
                price += delta;

                var level = new MarketLevel()
                {
                    MinSize = levelSize,
                    Price = price,
                    PriceCompensate = price - delta,
                    Side = MarketSide.Short,
                    Status = MarketLevelStatus.Empty,
                    CurrentOrderId = string.Empty,
                    Delta = delta,
                    Type = MarketLevelType.Direct
                };

                _levels.Add(level);
            }

            price = initPrice;
            for (int i = 0; i < countLevelsPerSide; i++)
            {
                price -= delta;

                var level = new MarketLevel()
                {
                    MinSize = levelSize,
                    Price = price,
                    PriceCompensate = price + delta,
                    Side = MarketSide.Long,
                    Status = MarketLevelStatus.Empty,
                    CurrentOrderId = string.Empty,
                    Delta = delta,
                    Type = MarketLevelType.Direct
                };

                _levels.Add(level);
            }
        }

        public async Task PlaceToMarketAsync()
        {
            var level = _levels.FirstOrDefault(l => l.Status == MarketLevelStatus.Empty);

            while(level != null)
            {
                var side =
                    (level.Side == MarketSide.Long && level.Type == MarketLevelType.Direct)
                    || (level.Side == MarketSide.Short && level.Type == MarketLevelType.Compensate)
                        ? MarketSide.Long
                        : MarketSide.Short;

                var price = level.Type == MarketLevelType.Direct ? level.Price : level.PriceCompensate;



                var scope = _levels.Where(l => l.Status == MarketLevelStatus.Placed && l.OrderSide != side);

                var crossLevel = 
                    side == MarketSide.Long
                        ? scope.Where(l => l.Price <= price).OrderBy(l => l.Price).FirstOrDefault() : 
                        scope.Where(l => l.Price >= price).OrderByDescending(l => l.Price).FirstOrDefault();

                if (crossLevel != null)
                {
                    crossLevel.CurrentOrderId = string.Empty;
                    crossLevel.Status = MarketLevelStatus.Empty;
                    crossLevel.Type = crossLevel.Type == MarketLevelType.Direct ? MarketLevelType.Compensate : MarketLevelType.Direct;

                    if (crossLevel.Status == MarketLevelStatus.Placed)
                    {
                        await _trader.CancelOrder(crossLevel.CurrentOrderId);
                        _levelsByOrder.Remove(crossLevel.CurrentOrderId);
                    }

                    Console.WriteLine($"flip level. Price: {level.Price}, Side: {level.Side}, Type: {level.Type}, Id: {level.CurrentOrderId}");


                    level.CurrentOrderId = string.Empty;
                    level.Status = MarketLevelStatus.Empty;
                    level.Type = level.Type == MarketLevelType.Direct ? MarketLevelType.Compensate : MarketLevelType.Direct;
                    Console.WriteLine($"flip level. Price: {level.Price}, Side: {level.Side}, Type: {level.Type}");
                }
                else
                {
                    var orderId = await _trader.PlaceOrderAsync(
                        _symbol,
                        price,
                        level.MinSize,
                        side);

                    if (!string.IsNullOrEmpty(orderId))
                    {
                        level.CurrentOrderId = orderId;
                        level.Status = MarketLevelStatus.Placed;
                        level.OrderSide = side;
                        Console.WriteLine(
                            $"Placed. Price: {level.Price}, Type: {level.Side}/{level.Type}, O-Price: {price}, O-Side: {side}, O-Id: {level.CurrentOrderId}");

                        _levelsByOrder[orderId] = level;
                    }
                }

                level = _levels.FirstOrDefault(l => l.Status == MarketLevelStatus.Empty);
            }
        }

        public async Task HandleTrade(string orderId)
        {
            var isOrderOpen = await _trader.CheckOrderIsOpenedAsync(orderId, _symbol);

            if (isOrderOpen)
                return;

            if (!_levelsByOrder.TryGetValue(orderId, out var level))
            {
                Console.WriteLine($"ERROR: Cannot found level by orderId: {orderId}");
                return;
            }

            Console.WriteLine($"execute level. Side: {level.Side}, Type: {level.Type}, Id: {level.CurrentOrderId}");

            _levelsByOrder.Remove(orderId);

            level.CurrentOrderId = string.Empty;
            level.Status = MarketLevelStatus.Empty;
            level.Type = level.Type == MarketLevelType.Direct ? MarketLevelType.Compensate : MarketLevelType.Direct;
        }

    }

    public interface ITrader
    {
        Task<string> PlaceOrderAsync(string symbol, decimal price, decimal size, MarketSide side);
        
        Task<bool> CheckOrderIsOpenedAsync(string orderId, string symbol);

        Task CancelOrder(string orderId);

        Task CancelAllOrders(string assetPair);
    }
}
