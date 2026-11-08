namespace OrderAgent.Domain.Menu;

/// <summary>
/// Platillo o bebida del menú. El precio que se cobra siempre sale de aquí, nunca del LLM.
/// </summary>
public sealed class MenuItem
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; }

    private MenuItem() { } // EF Core

    public MenuItem(int id, string name, string description, string category, decimal price, bool isAvailable = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price), "El precio debe ser mayor a cero.");

        Id = id;
        Name = name.Trim();
        Description = description.Trim();
        Category = category.Trim();
        Price = price;
        IsAvailable = isAvailable;
    }

    public void MarkUnavailable() => IsAvailable = false;

    public void MarkAvailable() => IsAvailable = true;
}
