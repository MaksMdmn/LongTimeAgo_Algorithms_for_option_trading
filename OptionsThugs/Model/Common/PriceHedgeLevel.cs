namespace OptionsThugs.Model.Common
{
    public class PriceHedgeLevel
    {
        public PriceHedgeLevel(PriceDirection direction, decimal price)
        {
            Direction = direction;
            Price = price;
        }

        public PriceDirection Direction { get; }
        public decimal Price { get; }

    }
}
