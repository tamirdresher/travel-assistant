using System.Collections.Concurrent;

namespace TravelAssistant.Api.Checkout;

public interface IOrderStore
{
    void Save(Order order);
    Order? Get(Guid id);
    IReadOnlyList<Order> ByEmail(string email);
}

internal sealed class InMemoryOrderStore : IOrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Save(Order order) => _orders[order.Id] = order;

    public Order? Get(Guid id) => _orders.TryGetValue(id, out var o) ? o : null;

    public IReadOnlyList<Order> ByEmail(string email)
        => _orders.Values.Where(o =>
                string.Equals(o.CustomerEmail, email, StringComparison.OrdinalIgnoreCase))
            .ToList();
}
