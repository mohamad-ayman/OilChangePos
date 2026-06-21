namespace OilChangePOS.Business;

internal static class SaleLineNormalizer
{
    public static List<SaleItemRequest> Normalize(IReadOnlyCollection<SaleItemRequest>? lines)
    {
        if (lines is null || lines.Count == 0)
            throw new InvalidOperationException("يجب أن تحتوي العملية على صنف واحد على الأقل.");

        var quantitiesByProduct = new Dictionary<int, decimal>();
        foreach (var line in lines)
        {
            if (line.ProductId <= 0)
                throw new InvalidOperationException("معرّف الصنف غير صالح.");
            if (line.Quantity <= 0)
                throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر.");

            quantitiesByProduct.TryGetValue(line.ProductId, out var current);
            quantitiesByProduct[line.ProductId] = current + line.Quantity;
        }

        return quantitiesByProduct
            .Select(x => new SaleItemRequest(x.Key, x.Value))
            .ToList();
    }
}
