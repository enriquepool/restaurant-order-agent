using System.Text;
using OrderAgent.Domain.Menu;

namespace OrderAgent.Agent;

internal static class MenuFormatter
{
    public static string Format(IReadOnlyList<MenuItem> items, OrderAgentOptions options)
    {
        if (items.Count == 0)
            return "No hay platillos disponibles en este momento.";

        var sb = new StringBuilder();
        foreach (var category in items.GroupBy(i => i.Category))
        {
            sb.Append(category.Key).Append(":\n");
            foreach (var item in category)
            {
                sb.Append($"- [id {item.Id}] {item.Name}: {options.FormatMoney(item.Price)}");
                if (!string.IsNullOrWhiteSpace(item.Description)) sb.Append($" ({item.Description})");
                sb.Append('\n');
            }
        }

        return sb.ToString().TrimEnd();
    }
}
