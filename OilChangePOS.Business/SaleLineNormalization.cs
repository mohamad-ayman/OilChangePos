using OilChangePOS.Domain;

namespace OilChangePOS.Business;

internal static class SaleLineNormalization
{
    internal static List<SaleItemRequest> Normalize(IReadOnlyList<SaleItemRequest> items, string emptyMessage)
    {
        if (items is null || items.Count == 0)
            throw new InvalidOperationException(emptyMessage);

        foreach (var item in items)
        {
            if (item.ProductId <= 0)
                throw new InvalidOperationException("معرّف صنف غير صالح.");
            if (item.Quantity <= 0)
                throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر.");
        }

        var map = new Dictionary<int, decimal>();
        foreach (var item in items)
            map[item.ProductId] = map.TryGetValue(item.ProductId, out var qty) ? qty + item.Quantity : item.Quantity;

        return map.Select(kv => new SaleItemRequest(kv.Key, kv.Value)).ToList();
    }
}
