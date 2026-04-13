namespace OilChangePOS.Domain;

/// <summary>Stable codes stored on <see cref="StockAuditLine.ReasonCode"/>.</summary>
public static class StockAuditReasonCodes
{
    public const string PhysicalCount = "PhysicalCount";
    public const string Damage = "Damage";
    public const string Theft = "Theft";
    public const string DataEntry = "DataEntry";
    public const string Sampling = "Sampling";
    public const string Other = "Other";
    /// <summary>Single-SKU correction from Inventory screen (not a full wall count).</summary>
    public const string InventoryScreen = "InventoryScreen";

    public static IReadOnlyList<(string Code, string Display)> Options { get; } =
    [
        (PhysicalCount, "جرد فعلي"),
        (Damage, "تلف / فساد"),
        (Theft, "سرقة / فقدان"),
        (DataEntry, "خطأ إدخال بيانات"),
        (Sampling, "عينات / استهلاك"),
        (Other, "أخرى"),
        (InventoryScreen, "تعديل من شاشة المخزون")
    ];

    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return PhysicalCount;
        return Options.Any(o => o.Code == code) ? code : PhysicalCount;
    }

    public static string GetDisplay(string? code)
    {
        var normalized = Normalize(code);
        return Options.First(o => o.Code == normalized).Display;
    }
}

public enum StockAuditStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2
}
