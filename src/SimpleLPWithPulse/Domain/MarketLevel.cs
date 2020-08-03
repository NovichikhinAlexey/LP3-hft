using System;

namespace SimpleLP.Domain
{
    public class MarketLevel
    {
        public decimal PriceBuy { get; set; }

        public decimal PriceSell { get; set; }

        public decimal MinSize { get; set; }

        public MarketLevelStatus Status { get; set; }

        public string CurrentOrderId { get; set; }

        public MarketSide Side { get; set; }

        public decimal Delta { get; set; }

        public MarketSide OrderSide { get; set; }

        public decimal SizePulseTick { get; set; }
        public int SizePulseMaxTick { get; set; }

        public decimal PricePulseTick { get; set; }
        public int PricePulseMaxTicks { get; set; }

        public decimal CompensateSize { get; set; }

        public decimal ActualOrderSize { get; set; }

        public MarketSide ActualOrderSide
        {
            get
            {
                return Side;
            }
        }

        public int ActualOrderSideSign
        {
            get { return ActualOrderSide == MarketSide.Long ? 1 : -1; }
        }

        public decimal ActualOrderPrice
        {
            get
            {
                var price = OriginalOrderPrice + (_rnd.Next(PricePulseMaxTicks * 2) - PricePulseMaxTicks) * PricePulseTick;

                return price;
            }
        }

        public decimal OriginalOrderPrice
        {
            get
            {
                var price = (Side == MarketSide.Long ? PriceBuy : PriceSell);

                return price;
            }
        }

        public bool RegisterTradeSize(decimal tradeSize)
        {
            //CompensateSize += tradeSize;
            //ActualOrderSize -= tradeSize;

            return false;
        }

        static Random _rnd = new Random();

        public void Revert()
        {
            Side = Side == MarketSide.Long ? MarketSide.Short : MarketSide.Long;
            Status = MarketLevelStatus.Empty;
            CurrentOrderId = string.Empty;

            if (ActualOrderSide == MarketSide.Short)
            {
                //var volume = -MinSize + CompensateSize - _rnd.Next(SizePulseMaxTick) * SizePulseTick;

                //if (volume > -MinSize)
                //{
                //    Revert();
                //}

                ActualOrderSize = Math.Abs(MinSize);
            }
            else
            {
                //var volume = +MinSize + CompensateSize + _rnd.Next(SizePulseMaxTick) * SizePulseTick;

                //if (volume < MinSize)
                // {
                //    Revert();
                //}

                ActualOrderSize = Math.Abs(MinSize);
            }
        }

        public void ApplySizePulse()
        {
            ActualOrderSize += _rnd.Next(SizePulseMaxTick) * SizePulseTick;
        }
    }
}
