namespace OilChangePOS.Business;

internal static class ProductDisplayNames
{
    /// <summary>POS/catalog style: <c>شركة — اسم الصنف</c> (e.g. Mobil — 5w30). Omits company segment when missing.</summary>
    internal static string CatalogLine(string? companyName, string productName)
    {
        var pn = (productName ?? string.Empty).Trim();
        if (pn.Length == 0) return "—";
        var cn = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        return cn is null or "" ? pn : $"{cn} — {pn}";
    }

    /// <summary>Shelf line: <c>شركة — صنف — تعبئة</c> when <paramref name="packageSize"/> is set (e.g. Mobil — 5W30 — 4L).</summary>
    internal static string CatalogDisplayName(string? companyName, string? productName, string? packageSize = null)
    {
        var baseLine = CatalogLine(companyName, productName ?? string.Empty);
        var pack = string.IsNullOrWhiteSpace(packageSize) ? null : packageSize.Trim();
        if (pack is null) return baseLine;
        if (baseLine == "—") return pack;
        return $"{baseLine} — {pack}";
    }
}
