namespace SharpFunctional.MsSql.Example.Models;

/// <summary>
/// Represents a customer who can place orders.
/// </summary>
public sealed class Customer
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Order> Orders { get; set; } = [];

    public override string ToString() =>
        $"[{Id}] {FirstName} {LastName} ({Email})";
}
