namespace OilChangePOS.Business;

internal static class SaleLineValidation
{
    internal static List<SaleItemRequest> NormalizeAndMergeLines(
        IEnumerable<SaleItemRequest>? items,
        string emptyMessage)
    {
        if (items is null)
            throw new InvalidOperationException(emptyMessage);

        var merged = new Dictionary<int, decimal>();
        foreach (var item in items)
        {
            if (item.ProductId <= 0)
                throw new InvalidOperationException("معرّف صنف غير صالح.");
            if (item.Quantity <= 0)
                throw new InvalidOperationException("كمية الصنف يجب أن تكون أكبر من صفر.");

            merged[item.ProductId] = merged.GetValueOrDefault(item.ProductId) + item.Quantity;
        }

        if (merged.Count == 0)
            throw new InvalidOperationException(emptyMessage);

        return merged
            .Select(x => new SaleItemRequest(x.Key, x.Value))
            .ToList();
    }

    internal static void EnsureValidDiscount(decimal discountAmount, decimal subtotal)
    {
        if (discountAmount < 0)
            throw new InvalidOperationException("قيمة الخصم لا يمكن أن تكون سالبة.");
        if (discountAmount > subtotal)
            throw new InvalidOperationException("قيمة الخصم لا يمكن أن تتجاوز إجمالي الفاتورة.");
    }
}
