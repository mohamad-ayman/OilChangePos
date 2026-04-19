namespace OilChangePOS.WinForms.Remote;

/// <summary>Holds the current API Bearer token for the WinForms HTTP pipeline (set after login, cleared on logout).</summary>
internal static class ApiAuthSession
{
    private static string? _accessToken;

    public static string? AccessToken
    {
        get => _accessToken;
        set => _accessToken = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static void Clear() => _accessToken = null;
}
