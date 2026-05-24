namespace OilChangePOS.Business;

internal static class SaleLineInputNormalizer
{
    internal static List<SaleItemRequest> NormalizeRequired(List<SaleItemRequest>? items, string emptyMessage)
    {
        if (items is null || items.Count == 0)
            throw new InvalidOperationException(emptyMessage);

        var merged = new Dictionary<int, decimal>();
        foreach (var item in items)
        {
            if (item.ProductId <= 0)
                throw new InvalidOperationException("معرّف الصنف غير صالح.");
            if (item.Quantity <= 0)
                throw new InvalidOperationException("كمية كل صنف يجب أن تكون أكبر من صفر.");

            merged[item.ProductId] = merged.TryGetValue(item.ProductId, out var current)
                ? current + item.Quantity
                : item.Quantity;
        }

        return merged
            .Select(x => new SaleItemRequest(x.Key, x.Value))
            .ToList();
    }
}
