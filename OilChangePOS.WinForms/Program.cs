using System.Globalization;
using System.Text;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.WinForms;

internal static class Program
{
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

        var services = new ServiceCollection();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Server=(localdb)\\MSSQLLocalDB;Database=OilChangePOSDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContextFactory<OilChangePosDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IAuthService, AuthService>();

        using var provider = services.BuildServiceProvider();
        try
        {
            var dbFactory = provider.GetRequiredService<IDbContextFactory<OilChangePosDbContext>>();
            using (var db = dbFactory.CreateDbContext())
            {
                DatabaseInitializer.SeedAsync(db).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            var detail = new StringBuilder();
            detail.AppendLine(ex.Message);
            for (Exception? inner = ex.InnerException; inner != null; inner = inner.InnerException)
                detail.AppendLine(inner.Message);
            for (Exception? scan = ex; scan != null; scan = scan.InnerException)
            {
                if (scan is not SqlException sql)
                    continue;
                foreach (SqlError err in sql.Errors)
                    detail.AppendLine($"SQL خطأ {err.Number}، سطر {err.LineNumber}: {err.Message}");
                break;
            }

            MessageBox.Show(
                $"فشل الاتصال بقاعدة البيانات.\n\nسلسلة الاتصال:\n{connectionString}\n\nالخطأ:\n{detail}\n\n" +
                "أصلح إعدادات SQL Server في appsettings.json وأعد التشغيل.",
                "خطأ في بدء التشغيل",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

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
                sp.GetRequiredService<IDbContextFactory<OilChangePosDbContext>>(),
                sp.GetRequiredService<ISalesService>(),
                sp.GetRequiredService<IInventoryService>(),
                sp.GetRequiredService<IReportService>(),
                sp.GetRequiredService<IExpenseService>(),
                sp.GetRequiredService<ITransferService>(),
                sp.GetRequiredService<IWarehouseService>(),
                sp.GetRequiredService<ICustomerService>(),
                sp.GetRequiredService<IAuthService>(),
                sessionUser!);

            Application.Run(main);

            if (!main.LogoutRequested)
                break;
        }
    }
}