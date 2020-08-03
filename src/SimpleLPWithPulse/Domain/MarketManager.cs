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

        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1,1);

        public MarketManager(ITrader trader, string symbol)
        {
            _trader = trader;
            _symbol = symbol;
        }

        public async Task ResetMarke(decimal initPrice, decimal levelMinSize, decimal delta, int countLevelsPerSide, 
            decimal pricePulseTick, int pricePulseMaxTicks, 
            decimal sizePulseTick, int sizePulseMaxTick)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {

                _levels.Clear();
                _levelsByOrder.Clear();

                await _trader.CancelAllOrders(_symbol);

                var price = initPrice;
                for (int i = 0; i < countLevelsPerSide; i++)
                {
                    var level = new MarketLevel()
                    {
                        MinSize = levelMinSize,
                        ActualOrderSize = levelMinSize,
                        PriceBuy = price,
                        PriceSell = price + delta,
                        Side = MarketSide.Short,
                        Status = MarketLevelStatus.Empty,
                        CurrentOrderId = string.Empty,
                        Delta = delta,
                        PricePulseTick = pricePulseTick,
                        PricePulseMaxTicks = pricePulseMaxTicks,
                        SizePulseTick = sizePulseTick,
                        SizePulseMaxTick = sizePulseMaxTick,
                        CompensateSize = 0
                    };

                    level.ApplySizePulse();

                    _levels.Add(level);

                    price += delta;
                }

                price = initPrice - delta;
                for (int i = 0; i < countLevelsPerSide; i++)
                {
                    var level = new MarketLevel()
                    {
                        MinSize = levelMinSize,
                        ActualOrderSize = levelMinSize,
                        PriceBuy = price,
                        PriceSell = price + delta,
                        Side = MarketSide.Long,
                        Status = MarketLevelStatus.Empty,
                        CurrentOrderId = string.Empty,
                        Delta = delta,
                        PricePulseTick = pricePulseTick,
                        PricePulseMaxTicks = pricePulseMaxTicks,
                        SizePulseTick = sizePulseTick,
                        SizePulseMaxTick = sizePulseMaxTick,
                        CompensateSize = 0
                    };

                    level.ApplySizePulse();

                    _levels.Add(level);
                    
                    price -= delta;
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task PlaceToMarketAsync()
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                var level = _levels.FirstOrDefault(l => l.Status == MarketLevelStatus.Empty);

                while (level != null)
                {
                    var side = level.ActualOrderSide;

                    var price = level.ActualOrderPrice;

                    var scope = _levels.Where(l => l.Status == MarketLevelStatus.Placed && l.OrderSide != side).ToList();

                    var crossLevel =
                        side == MarketSide.Long
                            ? scope.Where(l => l.PriceSell <= level.OriginalOrderPrice).OrderBy(l => l.PriceSell).FirstOrDefault()
                            : scope.Where(l => l.PriceBuy >= level.OriginalOrderPrice).OrderByDescending(l => l.PriceBuy).FirstOrDefault();

                    if (crossLevel != null)
                    {
                        if (crossLevel.Status == MarketLevelStatus.Placed)
                        {
                            await CancelOrder(crossLevel.CurrentOrderId);
                        }

                        //crossLevel.RegisterTradeSize(crossLevel.ActualOrderSize * crossLevel.ActualOrderSideSign);
                        crossLevel.Revert();

                        Console.WriteLine(
                            $"flip level. Price: {crossLevel.PriceBuy}, Side: {crossLevel.Side}, Id: {level.CurrentOrderId}");


                        //level.RegisterTradeSize(crossLevel.ActualOrderSize * crossLevel.ActualOrderSideSign);
                        level.Revert();
                        Console.WriteLine($"flip level. Price: {level.PriceBuy}, Side: {level.Side}");
                    }
                    else
                    {
                        var orderId = await _trader.PlaceOrderAsync(
                            _symbol,
                            price,
                            level.ActualOrderSize,
                            side);

                        if (!string.IsNullOrEmpty(orderId))
                        {
                            level.CurrentOrderId = orderId;
                            level.Status = MarketLevelStatus.Placed;
                            level.OrderSide = side;
                            Console.WriteLine(
                                $"Placed. Price: {level.PriceBuy}, Side: {level.Side}, O-Price: {price}, O-Side: {side}, O-Id: {level.CurrentOrderId}");

                            _levelsByOrder[orderId] = level;
                        }
                    }

                    level = _levels.FirstOrDefault(l => l.Status == MarketLevelStatus.Empty);
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private async Task CancelOrder(string currentOrderId)
        {
            await _trader.CancelOrder(currentOrderId);
            _levelsByOrder.Remove(currentOrderId);
        }

        public async Task HandleTrade(string orderId, decimal tradeSize)
        {
            await _semaphoreSlim.WaitAsync();
            try
            {
                var level = GetLevelbyOrder(orderId);

                if (level == null)
                {
                    Console.WriteLine($"ERROR: Cannot found level by orderId: {orderId}");
                    return;
                }

                //level.CompensateSize += tradeSize;

                var isOrderOpen = await _trader.CheckOrderIsOpenedAsync(orderId, _symbol);

                if (isOrderOpen)
                    return;

                Console.WriteLine($"execute level. Price: {level.PriceBuy}, Side: {level.Side}, Id: {level.CurrentOrderId}");

                _levelsByOrder.Remove(orderId);

                level.Revert();
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task Pulse()
        {
            foreach (var level in _levels)
            {
                await _semaphoreSlim.WaitAsync();
                try
                {
                    bool shouldPulse;
                    string currentOrderId;

                    currentOrderId = level.CurrentOrderId;
                    shouldPulse = level.Status == MarketLevelStatus.Placed;

                    if (shouldPulse)
                    {
                        var size = await _trader.GetRemainingSizeOfActiveOrder(currentOrderId, _symbol);

                        if (size.HasValue)
                        {
                            var price = level.ActualOrderPrice;
                            var orderId = _trader.RePlaceOrderAsync(
                                currentOrderId,
                                _symbol,
                                price,
                                size.Value,
                                level.ActualOrderSide).GetAwaiter().GetResult();

                            Console.WriteLine($"Pulse {level.PriceBuy}, {level.Side}: Price: {price}, Side: {level.ActualOrderSide}, Size: {size.Value}");

                            if (!string.IsNullOrEmpty(orderId) && level.CurrentOrderId == currentOrderId)
                            {
                                level.CurrentOrderId = orderId;
                                _levelsByOrder[orderId] = level;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: cannot exec pulse for {level.PriceBuy} ({level.Side})");
                    Console.WriteLine(ex);
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }
        }

        private MarketLevel GetLevelbyOrder(string orderId)
        {
            return _levelsByOrder.TryGetValue(orderId, out var level) ? level : null;
        }
    }

    public interface ITrader
    {
        Task<string> PlaceOrderAsync(string symbol, decimal price, decimal size, MarketSide side);
        
        Task<bool> CheckOrderIsOpenedAsync(string orderId, string symbol);

        Task CancelOrder(string orderId);

        Task CancelAllOrders(string assetPair);

        Task<decimal?> GetRemainingSizeOfActiveOrder(string orderId, string symbol);

        Task<string> RePlaceOrderAsync(string orderId, string symbol, decimal price, decimal size, MarketSide side);
    }
}
