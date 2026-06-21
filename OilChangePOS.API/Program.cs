using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OilChangePOS.API.Middleware;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtAccessTokenFactory>();

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var signingKey = jwtSection.GetValue<string>("SigningKey") ?? string.Empty;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                          ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(sub, out var userId))
                {
                    context.Fail("Invalid token subject.");
                    return;
                }

                var dbFactory = context.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<OilChangePosDbContext>>();
                await using var db = await dbFactory.CreateDbContextAsync(context.HttpContext.RequestAborted);
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive)
                {
                    context.Fail("User is inactive or no longer exists.");
                    return;
                }

                var roleClaim = principal?.FindFirstValue(ClaimTypes.Role);
                if (!Enum.TryParse<UserRole>(roleClaim, out var tokenRole) || tokenRole != user.Role)
                {
                    context.Fail("User role changed.");
                    return;
                }

                int? tokenHomeBranchId = null;
                var homeBranchClaim = principal?.FindFirstValue("home_branch_id");
                if (!string.IsNullOrWhiteSpace(homeBranchClaim))
                {
                    if (!int.TryParse(homeBranchClaim, out var parsedHomeBranchId))
                    {
                        context.Fail("Invalid home branch claim.");
                        return;
                    }

                    tokenHomeBranchId = parsedHomeBranchId;
                }

                if (tokenHomeBranchId != user.HomeBranchWarehouseId)
                    context.Fail("User home branch changed.");
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=(localdb)\\MSSQLLocalDB;Database=OilChangePOSDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContextFactory<OilChangePosDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IBranchStockRequestService, BranchStockRequestService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<ICatalogAdminService, CatalogAdminService>();
builder.Services.AddScoped<IMainWarehouseAdminService, MainWarehouseAdminService>();
builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();

var app = builder.Build();

// Seed in the background so Kestrel can accept connections immediately while the React dev server or other clients start.
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
app.UseResponseCompression();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
