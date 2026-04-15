namespace OilChangePOS.WinForms.Remote;

public sealed class OilChangeApiOptions
{
    public const string SectionName = "OilChangeApi";

    /// <summary>Base URL of the OilChangePOS.API host (include trailing slash optional).</summary>
    public string BaseUrl { get; set; } = "https://localhost:7099/";

    /// <summary>If true, WinForms does not wait for api/health at startup.</summary>
    public bool SkipApiHealthCheck { get; set; }

    /// <summary>Max time to poll api/health before showing an error (seconds).</summary>
    public int StartupMaxWaitSeconds { get; set; } = 60;

    /// <summary>Delay between failed health checks (milliseconds).</summary>
    public int StartupRetryMilliseconds { get; set; } = 500;
}
