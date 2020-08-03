namespace SimpleLP.Domain
{
    public class MarketLevel
    {
        public decimal Price { get; set; }

        public decimal PriceCompensate { get; set; }

        public decimal MinSize { get; set; }

        public MarketLevelStatus Status { get; set; }

        public string CurrentOrderId { get; set; }

        public MarketSide Side { get; set; }

        public decimal Delta { get; set; }

        public MarketLevelType Type { get; set; }

        public MarketSide OrderSide { get; set; }
    }

    public enum MarketLevelStatus
    {
        Empty,
        Placed
    }

    public enum MarketSide
    {
        Long,
        Short
    }

    public enum MarketLevelType
    {
        Direct,
        Compensate
    }
}
