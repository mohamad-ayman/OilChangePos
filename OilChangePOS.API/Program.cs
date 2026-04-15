using Microsoft.EntityFrameworkCore;
using OilChangePOS.API.Middleware;
using OilChangePOS.Business;
using OilChangePOS.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true);
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=(localdb)\\MSSQLLocalDB;Database=OilChangePOSDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContextFactory<OilChangePosDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<ICatalogAdminService, CatalogAdminService>();
builder.Services.AddScoped<IMainWarehouseAdminService, MainWarehouseAdminService>();

var app = builder.Build();

// Seed in the background so Kestrel can accept connections immediately (WinForms / other clients can start in parallel).
_ = Task.Run(async () =>
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OilChangePosDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        await DatabaseInitializer.SeedAsync(db);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogError(ex, "Database seed failed; API is running. Check connection string.");
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();
