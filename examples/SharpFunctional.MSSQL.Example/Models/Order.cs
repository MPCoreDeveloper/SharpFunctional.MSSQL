namespace SharpFunctional.MsSql.Example.Models;

/// <summary>
/// Represents a customer order containing one or more order lines.
/// </summary>
public sealed class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public required string Status { get; set; }

    public Customer? Customer { get; set; }
    public List<OrderLine> Lines { get; set; } = [];

    public override string ToString() =>
        $"[Order {Id}] Customer={CustomerId} Status={Status} Date={OrderDate:yyyy-MM-dd} Lines={Lines.Count}";
}
