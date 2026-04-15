using System.Globalization;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OilChangePOS.Business;
using OilChangePOS.Domain;
using OilChangePOS.WinForms.Remote;

namespace OilChangePOS.WinForms;

internal static class Program
{
    private const string HttpClientName = "OilChangePOS";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var ar = new CultureInfo("ar-SA");
        Thread.CurrentThread.CurrentUICulture = ar;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var apiOptions = configuration.GetSection(OilChangeApiOptions.SectionName).Get<OilChangeApiOptions>() ?? new OilChangeApiOptions();
        var baseUrl = apiOptions.BaseUrl?.Trim();
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = "http://localhost:5099/";
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        if (!apiOptions.SkipApiHealthCheck)
        {
            var healthUri = new Uri(new Uri(baseUrl, UriKind.Absolute), "api/health");
            var maxWait = TimeSpan.FromSeconds(Math.Clamp(apiOptions.StartupMaxWaitSeconds, 5, 600));
            var retryDelay = TimeSpan.FromMilliseconds(Math.Clamp(apiOptions.StartupRetryMilliseconds, 100, 10_000));
            var deadline = DateTime.UtcNow + maxWait;
            var ready = false;
            Exception? lastError = null;
            using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var response = probe.GetAsync(healthUri).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        ready = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                Thread.Sleep(retryDelay);
            }

            if (!ready)
            {
                var detail = lastError?.Message ?? "لم يتم استلام رد ناجح من api/health.";
                MessageBox.Show(
                    $"تعذر الاتصال بالخادم (API) خلال {maxWait.TotalSeconds} ثانية.\n\nالعنوان:\n{baseUrl}\n\n{detail}\n\nشغّل OilChangePOS.API أو انتظر حتى يكتمل الإقلاع ثم أعد المحاولة.",
                    "خطأ في بدء التشغيل",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                return;
            }
        }

        var services = new ServiceCollection();
        services.Configure<OilChangeApiOptions>(configuration.GetSection(OilChangeApiOptions.SectionName));
        services.AddHttpClient(HttpClientName, (sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<OilChangeApiOptions>>().Value;
            var url = string.IsNullOrWhiteSpace(opt.BaseUrl) ? baseUrl : opt.BaseUrl.Trim();
            if (!url.EndsWith('/')) url += "/";
            client.BaseAddress = new Uri(url, UriKind.Absolute);
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddScoped<IInventoryService>(sp => new HttpInventoryService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<IReportService>(sp => new HttpReportService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<IExpenseService>(sp => new HttpExpenseService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<ITransferService>(sp => new HttpTransferService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<IWarehouseService>(sp => new HttpWarehouseService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<ICustomerService>(sp => new HttpCustomerService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<IAuthService>(sp => new HttpAuthService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<ISalesService>(sp => new HttpSalesService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<ICatalogAdminService>(sp => new HttpCatalogAdminService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<IMainWarehouseAdminService>(sp => new HttpMainWarehouseAdminService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));
        services.AddScoped<IProductCatalogService>(sp => new HttpProductCatalogService(sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName)));

        using var provider = services.BuildServiceProvider();

        while (true)
        {
            AppUser? sessionUser = null;
            using (var login = new LoginForm(provider.GetRequiredService<IAuthService>()))
            {
                if (login.ShowDialog() != DialogResult.OK || login.AuthenticatedUser is null)
                    return;
                sessionUser = login.AuthenticatedUser;
            }

            using var appScope = provider.CreateScope();
            var sp = appScope.ServiceProvider;
            using var main = new MainForm(
                sp.GetRequiredService<ISalesService>(),
                sp.GetRequiredService<IInventoryService>(),
                sp.GetRequiredService<IReportService>(),
                sp.GetRequiredService<IExpenseService>(),
                sp.GetRequiredService<ITransferService>(),
                sp.GetRequiredService<IWarehouseService>(),
                sp.GetRequiredService<ICustomerService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<ICatalogAdminService>(),
                sp.GetRequiredService<IMainWarehouseAdminService>(),
                sp.GetRequiredService<IProductCatalogService>(),
                sessionUser!);

            Application.Run(main);

            if (!main.LogoutRequested)
                break;
        }
    }
}
