namespace SharpFunctional.MsSql.Example.Models;

/// <summary>
/// Represents a single line item within an order.
/// </summary>
public sealed class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Order? Order { get; set; }
    public Product? Product { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public override string ToString() =>
        $"  {Quantity}x {Product?.Name ?? $"Product#{ProductId}"} @ €{UnitPrice:F2} = €{LineTotal:F2}";
}
