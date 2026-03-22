namespace SharpFunctional.MsSql.Example.Models;

/// <summary>
/// Sample product entity demonstrating a typical domain model.
/// </summary>
public sealed class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }

    public List<OrderLine> OrderLines { get; set; } = [];

    public override string ToString() =>
        $"[{Id}] {Name} ({Category}) - €{Price:F2} | Stock: {Stock}";
}
