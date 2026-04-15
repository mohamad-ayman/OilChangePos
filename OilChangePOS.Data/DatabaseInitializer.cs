using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OilChangePOS.Domain;

namespace OilChangePOS.Data;

public static class DatabaseInitializer
{
    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Older builds used a fixed demo hash (same for admin/cashier) or SHA256("password").
    /// Rewrite those rows so documented logins work: admin/admin, branch/branch.
    /// </summary>
    private static void NormalizeDemoPasswords(OilChangePosDbContext db)
    {
        var legacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9",
            "5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8"
        };
        foreach (var u in db.Users.ToList())
        {
            if (!legacy.Contains(u.PasswordHash)) continue;
            if (u.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
                u.PasswordHash = HashPassword("admin");
            else if (u.Username.Equals("branch", StringComparison.OrdinalIgnoreCase)
                     || u.Username.Equals("cashier", StringComparison.OrdinalIgnoreCase))
                u.PasswordHash = HashPassword("branch");
        }
    }

    public static async Task SeedAsync(OilChangePosDbContext dbContext)
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        else if (await HasEfMigrationsHistoryAsync(dbContext.Database))
        {
            await dbContext.Database.MigrateAsync();
        }

        // Keeps local DB in sync if Migrate did not apply (e.g. older deployed build) — matches migration AddAppUserHomeBranchWarehouse.
        await EnsureUserHomeBranchWarehouseSchemaAsync(dbContext);

        // Products.CompanyId must exist before any EF query on Product and before warehouse scripts touch Products.
        await EnsureCatalogCompaniesAsync(dbContext);
        await EnsureWarehouseSchemaAsync(dbContext);
        await EnsureExpensesTableAsync(dbContext);
        await EnsureBranchProductPricesTableAsync(dbContext);

        await ThrowIfProductsMissingCompanyIdAsync(dbContext.Database);

        if (!dbContext.Users.Any())
        {
            dbContext.Users.AddRange(
                new AppUser { Username = "admin", PasswordHash = HashPassword("admin"), Role = UserRole.Admin },
                new AppUser { Username = "branch", PasswordHash = HashPassword("branch"), Role = UserRole.Branch });
        }
        else if (!dbContext.Users.Any(u => u.Username == "branch"))
        {
            dbContext.Users.Add(new AppUser { Username = "branch", PasswordHash = HashPassword("branch"), Role = UserRole.Branch });
        }

        // GetMainAsync() requires an active row with Type == Main. Empty DB, deleted mains, or only branches → add/fix.
        if (!dbContext.Warehouses.Any(w => w.Type == WarehouseType.Main && w.IsActive))
        {
            var dormantMain = dbContext.Warehouses.FirstOrDefault(w => w.Type == WarehouseType.Main);
            if (dormantMain is not null)
                dormantMain.IsActive = true;
            else
                dbContext.Warehouses.Add(new Warehouse { Name = "Main Warehouse", Type = WarehouseType.Main, IsActive = true });
        }

        if (!dbContext.Warehouses.Any(w => w.Type == WarehouseType.Branch && w.IsActive))
        {
            var dormantBranch = dbContext.Warehouses.FirstOrDefault(w => w.Type == WarehouseType.Branch);
            if (dormantBranch is not null)
                dormantBranch.IsActive = true;
            else
                dbContext.Warehouses.Add(new Warehouse { Name = "Branch 1", Type = WarehouseType.Branch, IsActive = true });
        }

        if (!dbContext.Customers.Any())
        {
            dbContext.Customers.AddRange(
                new Customer { FullName = "Fleet / B2B Account", PhoneNumber = "0500000001" },
                new Customer { FullName = "Retail Customer", PhoneNumber = "0500000002" });
        }

        NormalizeDemoPasswords(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<bool> HasEfMigrationsHistoryAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        await database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U')";
            var id = await cmd.ExecuteScalarAsync(cancellationToken);
            return id is not DBNull and not null;
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> DboProductsColumnExistsAsync(DatabaseFacade database, string columnName, CancellationToken cancellationToken = default)
    {
        if (columnName is not ("CompanyName" or "CompanyId"))
            throw new ArgumentOutOfRangeException(nameof(columnName));

        await database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = database.GetDbConnection().CreateCommand();
            cmd.CommandText =
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON t.object_id = c.object_id
                    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = N'dbo' AND t.name = N'Products' AND c.name = @col
                ) THEN 1 ELSE 0 END
                """;
            var p = cmd.CreateParameter();
            p.ParameterName = "@col";
            p.Value = columnName;
            cmd.Parameters.Add(p);
            var n = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(n) == 1;
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }

    private static async Task ThrowIfProductsMissingCompanyIdAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        if (!await DboProductsTableExistsAsync(database, cancellationToken))
            return;
        if (await DboProductsColumnExistsAsync(database, "CompanyId", cancellationToken))
            return;

        throw new InvalidOperationException(
            "Database schema is missing Products.CompanyId. Automatic repair did not apply. Back up the database, then run: " +
            "dotnet ef database update --project OilChangePOS.Data --startup-project OilChangePOS.WinForms");
    }

    private static async Task<bool> DboProductsTableExistsAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        await database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT OBJECT_ID(N'[dbo].[Products]', N'U')";
            var id = await cmd.ExecuteScalarAsync(cancellationToken);
            return id is not DBNull and not null;
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureWarehouseSchemaAsync(OilChangePosDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Warehouses](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(100) NOT NULL UNIQUE,
                    [Type] INT NOT NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_Warehouses_IsActive] DEFAULT(1)
                );
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.Products', 'ProductCategory') IS NULL
                ALTER TABLE [dbo].[Products] ADD [ProductCategory] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Products_ProductCategory] DEFAULT(N'Oil');
            IF COL_LENGTH('dbo.Products', 'PackageSize') IS NULL
                ALTER TABLE [dbo].[Products] ADD [PackageSize] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Products_PackageSize] DEFAULT(N'Unit');

            IF COL_LENGTH('dbo.Products', 'ReorderLevel') IS NOT NULL
            BEGIN
                DECLARE @rlUq SYSNAME;
                DECLARE @rlCmd NVARCHAR(512);
                WHILE EXISTS (
                    SELECT 1
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
                    INNER JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
                    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.Products')
                      AND kc.type = N'UQ'
                      AND col.name = N'ReorderLevel')
                BEGIN
                    SELECT TOP (1) @rlUq = kc.name
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
                    INNER JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
                    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.Products')
                      AND kc.type = N'UQ'
                      AND col.name = N'ReorderLevel';
                    SET @rlCmd = N'ALTER TABLE [dbo].[Products] DROP CONSTRAINT ' + QUOTENAME(@rlUq) + N';';
                    EXEC (@rlCmd);
                END

                DECLARE @rlIdx NVARCHAR(MAX) = N'';
                SELECT @rlIdx += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON [dbo].[Products];'
                FROM sys.indexes i
                WHERE i.object_id = OBJECT_ID(N'dbo.Products')
                  AND i.index_id > 0
                  AND i.is_primary_key = 0
                  AND i.is_hypothetical = 0
                  AND i.is_unique_constraint = 0
                  AND EXISTS (
                      SELECT 1
                      FROM sys.index_columns ic
                      INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = N'ReorderLevel');
                IF LEN(@rlIdx) > 0
                    EXEC sp_executesql @rlIdx;

                DECLARE @rlDrop NVARCHAR(128);
                SELECT @rlDrop = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Products')
                  AND c.name = N'ReorderLevel';
                IF @rlDrop IS NOT NULL
                BEGIN
                    SET @rlCmd = N'ALTER TABLE [dbo].[Products] DROP CONSTRAINT ' + QUOTENAME(@rlDrop) + N';';
                    EXEC (@rlCmd);
                END
                ALTER TABLE [dbo].[Products] DROP COLUMN [ReorderLevel];
            END

            IF COL_LENGTH('dbo.Products', 'ProductType') IS NOT NULL
            BEGIN
                DECLARE @ptUq SYSNAME;
                DECLARE @ptCmd NVARCHAR(512);
                WHILE EXISTS (
                    SELECT 1
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
                    INNER JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
                    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.Products')
                      AND kc.type = N'UQ'
                      AND col.name = N'ProductType')
                BEGIN
                    SELECT TOP (1) @ptUq = kc.name
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
                    INNER JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
                    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.Products')
                      AND kc.type = N'UQ'
                      AND col.name = N'ProductType';
                    SET @ptCmd = N'ALTER TABLE [dbo].[Products] DROP CONSTRAINT ' + QUOTENAME(@ptUq) + N';';
                    EXEC (@ptCmd);
                END

                DECLARE @ptIdx NVARCHAR(MAX) = N'';
                SELECT @ptIdx += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON [dbo].[Products];'
                FROM sys.indexes i
                WHERE i.object_id = OBJECT_ID(N'dbo.Products')
                  AND i.index_id > 0
                  AND i.is_primary_key = 0
                  AND i.is_hypothetical = 0
                  AND i.is_unique_constraint = 0
                  AND EXISTS (
                      SELECT 1
                      FROM sys.index_columns ic
                      INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = N'ProductType');
                IF LEN(@ptIdx) > 0
                    EXEC sp_executesql @ptIdx;

                DECLARE @ptDrop NVARCHAR(128);
                SELECT @ptDrop = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Products')
                  AND c.name = N'ProductType';
                IF @ptDrop IS NOT NULL
                BEGIN
                    SET @ptCmd = N'ALTER TABLE [dbo].[Products] DROP CONSTRAINT ' + QUOTENAME(@ptDrop) + N';';
                    EXEC (@ptCmd);
                END
                ALTER TABLE [dbo].[Products] DROP COLUMN [ProductType];
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.StockMovements', 'MovementType') IS NULL
                    ALTER TABLE [dbo].[StockMovements] ADD [MovementType] INT NULL;

                IF COL_LENGTH('dbo.StockMovements', 'FromWarehouseId') IS NULL
                    ALTER TABLE [dbo].[StockMovements] ADD [FromWarehouseId] INT NULL;

                IF COL_LENGTH('dbo.StockMovements', 'ToWarehouseId') IS NULL
                    ALTER TABLE [dbo].[StockMovements] ADD [ToWarehouseId] INT NULL;
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockMovements', 'Type') IS NOT NULL
               AND COL_LENGTH('dbo.StockMovements', 'MovementType') IS NOT NULL
            BEGIN
                EXEC(N'
                    UPDATE dbo.StockMovements
                    SET MovementType = CASE
                        WHEN UPPER(LTRIM(RTRIM(CAST([Type] AS NVARCHAR(50))))) = ''IN'' THEN 1
                        WHEN UPPER(LTRIM(RTRIM(CAST([Type] AS NVARCHAR(50))))) = ''OUT'' THEN 2
                        ELSE 4
                    END
                    WHERE MovementType IS NULL;
                ');
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockMovements', 'MovementType') IS NOT NULL
            BEGIN
                DECLARE @mtType NVARCHAR(128);
                SELECT @mtType = t.name
                FROM sys.columns c
                INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = OBJECT_ID(N'dbo.StockMovements')
                  AND c.name = 'MovementType';

                IF @mtType IN ('nvarchar', 'varchar', 'nchar', 'char')
                BEGIN
                    IF COL_LENGTH('dbo.StockMovements', 'MovementTypeInt') IS NULL
                        ALTER TABLE [dbo].[StockMovements] ADD [MovementTypeInt] INT NULL;

                    EXEC(N'
                        UPDATE dbo.StockMovements
                        SET MovementTypeInt = CASE UPPER(LTRIM(RTRIM(CAST(MovementType AS NVARCHAR(50)))))
                            WHEN ''PURCHASE'' THEN 1
                            WHEN ''SALE'' THEN 2
                            WHEN ''TRANSFER'' THEN 3
                            WHEN ''ADJUST'' THEN 4
                            WHEN ''IN'' THEN 1
                            WHEN ''OUT'' THEN 2
                            ELSE 4
                        END;
                    ');

                    DECLARE @dfName NVARCHAR(128);
                    SELECT @dfName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.StockMovements')
                      AND c.name = 'MovementType';
                    IF @dfName IS NOT NULL
                        EXEC('ALTER TABLE [dbo].[StockMovements] DROP CONSTRAINT [' + @dfName + ']');

                    ALTER TABLE [dbo].[StockMovements] DROP COLUMN [MovementType];
                    EXEC sp_rename 'dbo.StockMovements.MovementTypeInt', 'MovementType', 'COLUMN';
                END
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockMovements', 'MovementType') IS NOT NULL
            BEGIN
                EXEC(N'
                    UPDATE dbo.StockMovements
                    SET MovementType = 4
                    WHERE MovementType IS NULL;
                ');

                DECLARE @dfName2 NVARCHAR(128);
                SELECT @dfName2 = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.StockMovements')
                  AND c.name = 'MovementType';
                IF @dfName2 IS NULL
                    ALTER TABLE [dbo].[StockMovements] ADD DEFAULT(4) FOR [MovementType];
            END

            IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockMovements', 'Type') IS NOT NULL
            BEGIN
                DECLARE @dropTypeIndexes NVARCHAR(MAX) = N'';
                SELECT @dropTypeIndexes = @dropTypeIndexes +
                    N'DROP INDEX [' + i.name + N'] ON [dbo].[StockMovements];'
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID(N'dbo.StockMovements')
                  AND i.is_primary_key = 0
                  AND i.is_unique_constraint = 0
                  AND c.name = 'Type';
                IF LEN(@dropTypeIndexes) > 0
                    EXEC sp_executesql @dropTypeIndexes;

                DECLARE @dropTypeChecks NVARCHAR(MAX) = N'';
                SELECT @dropTypeChecks = @dropTypeChecks +
                    N'ALTER TABLE [dbo].[StockMovements] DROP CONSTRAINT [' + cc.name + N'];'
                FROM sys.check_constraints cc
                WHERE cc.parent_object_id = OBJECT_ID(N'dbo.StockMovements')
                  AND cc.definition LIKE N'%[[]Type[]]%';
                IF LEN(@dropTypeChecks) > 0
                    EXEC sp_executesql @dropTypeChecks;

                DECLARE @dropTypeDefault NVARCHAR(128);
                SELECT @dropTypeDefault = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.StockMovements')
                  AND c.name = 'Type';
                IF @dropTypeDefault IS NOT NULL
                    EXEC(N'ALTER TABLE [dbo].[StockMovements] DROP CONSTRAINT [' + @dropTypeDefault + N']');

                ALTER TABLE [dbo].[StockMovements] DROP COLUMN [Type];
            END

            IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockMovements', 'MovementType') IS NOT NULL
            BEGIN
                DECLARE @dropMovementTypeChecks NVARCHAR(MAX) = N'';
                SELECT @dropMovementTypeChecks = @dropMovementTypeChecks +
                    N'ALTER TABLE [dbo].[StockMovements] DROP CONSTRAINT [' + cc.name + N'];'
                FROM sys.check_constraints cc
                WHERE cc.parent_object_id = OBJECT_ID(N'dbo.StockMovements')
                  AND cc.definition LIKE N'%[[]MovementType[]]%';
                IF LEN(@dropMovementTypeChecks) > 0
                    EXEC sp_executesql @dropMovementTypeChecks;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE parent_object_id = OBJECT_ID(N'dbo.StockMovements')
                      AND name = N'CK_StockMovements_MovementType_Int'
                )
                BEGIN
                    ALTER TABLE [dbo].[StockMovements]
                        WITH NOCHECK ADD CONSTRAINT [CK_StockMovements_MovementType_Int]
                        CHECK ([MovementType] IN (1,2,3,4));
                END
            END

            IF OBJECT_ID(N'[dbo].[FK_StockMovements_FromWarehouse]', N'F') IS NULL
                ALTER TABLE [dbo].[StockMovements] WITH CHECK ADD CONSTRAINT [FK_StockMovements_FromWarehouse] FOREIGN KEY([FromWarehouseId]) REFERENCES [dbo].[Warehouses]([Id]);
            IF OBJECT_ID(N'[dbo].[FK_StockMovements_ToWarehouse]', N'F') IS NULL
                ALTER TABLE [dbo].[StockMovements] WITH CHECK ADD CONSTRAINT [FK_StockMovements_ToWarehouse] FOREIGN KEY([ToWarehouseId]) REFERENCES [dbo].[Warehouses]([Id]);
            """);

        // Invoices.WarehouseId: separate round-trips. A single batch is compiled against one catalog snapshot;
        // referencing WarehouseId in the same batch as ALTER ADD can raise Msg 207 even under IF.
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.Invoices', 'WarehouseId') IS NULL
                ALTER TABLE [dbo].[Invoices] ADD [WarehouseId] INT NULL;
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[FK_Invoices_Warehouses_WarehouseId]', N'F') IS NULL
               AND OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.Invoices', 'WarehouseId') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL
            ALTER TABLE [dbo].[Invoices] WITH CHECK ADD CONSTRAINT [FK_Invoices_Warehouses_WarehouseId]
                FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]);
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.Invoices', 'WarehouseId') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.indexes i
                   WHERE i.object_id = OBJECT_ID(N'dbo.Invoices') AND i.name = N'IX_Invoices_WarehouseId')
                CREATE NONCLUSTERED INDEX [IX_Invoices_WarehouseId] ON [dbo].[Invoices]([WarehouseId]);
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[Purchases]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Purchases](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [ProductId] INT NOT NULL,
                    [Quantity] DECIMAL(18,3) NOT NULL,
                    [PurchasePrice] DECIMAL(18,2) NOT NULL,
                    [ProductionDate] DATETIME2 NOT NULL,
                    [PurchaseDate] DATETIME2 NOT NULL,
                    [WarehouseId] INT NOT NULL,
                    [CreatedByUserId] INT NOT NULL,
                    [Notes] NVARCHAR(500) NOT NULL CONSTRAINT [DF_Purchases_Notes] DEFAULT(N''),
                    CONSTRAINT [FK_Purchases_Products] FOREIGN KEY([ProductId]) REFERENCES [dbo].[Products]([Id]),
                    CONSTRAINT [FK_Purchases_Warehouses] FOREIGN KEY([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]),
                    CONSTRAINT [FK_Purchases_Users] FOREIGN KEY([CreatedByUserId]) REFERENCES [dbo].[Users]([Id])
                );
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockAudits]', N'U') IS NULL
               AND OBJECT_ID(N'[dbo].[Users]', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [dbo].[StockAudits](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [AuditDateUtc] DATETIME2 NOT NULL CONSTRAINT [DF_StockAudits_AuditDateUtc] DEFAULT (SYSUTCDATETIME()),
                    [Notes] NVARCHAR(500) NOT NULL CONSTRAINT [DF_StockAudits_Notes] DEFAULT (N''),
                    [CreatedByUserId] INT NOT NULL,
                    [WarehouseId] INT NULL,
                    [Status] INT NOT NULL CONSTRAINT [DF_StockAudits_Status] DEFAULT (1),
                    CONSTRAINT [FK_StockAudits_Users] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([Id])
                );
                IF OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[StockAudits] WITH CHECK ADD CONSTRAINT [FK_StockAudits_Warehouses_WarehouseId]
                        FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]);
                END
                CREATE NONCLUSTERED INDEX [IX_StockAudits_WarehouseId_AuditDateUtc] ON [dbo].[StockAudits]([WarehouseId], [AuditDateUtc]);
            END

            IF OBJECT_ID(N'[dbo].[StockAuditLines]', N'U') IS NULL
               AND OBJECT_ID(N'[dbo].[StockAudits]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Products]', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [dbo].[StockAuditLines](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [StockAuditId] INT NOT NULL,
                    [ProductId] INT NOT NULL,
                    [SystemQuantity] DECIMAL(18,3) NOT NULL,
                    [ActualQuantity] DECIMAL(18,3) NOT NULL,
                    [ReasonCode] NVARCHAR(50) NOT NULL CONSTRAINT [DF_StockAuditLines_ReasonCode] DEFAULT (N'PhysicalCount'),
                    CONSTRAINT [FK_StockAuditLines_StockAudits] FOREIGN KEY ([StockAuditId]) REFERENCES [dbo].[StockAudits]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_StockAuditLines_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id])
                );
            END
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockAudits]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockAudits', 'WarehouseId') IS NULL
                ALTER TABLE [dbo].[StockAudits] ADD [WarehouseId] INT NULL;

            IF OBJECT_ID(N'[dbo].[StockAudits]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockAudits', 'Status') IS NULL
                ALTER TABLE [dbo].[StockAudits] ADD [Status] INT NOT NULL CONSTRAINT [DF_StockAudits_Status_Scratch] DEFAULT(1);
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockAudits]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockAudits', 'WarehouseId') IS NOT NULL
               AND EXISTS (SELECT 1 FROM [dbo].[StockAudits] WHERE [WarehouseId] IS NULL)
               AND EXISTS (SELECT 1 FROM [dbo].[Warehouses])
                UPDATE sa
                SET [WarehouseId] = w.[Id]
                FROM [dbo].[StockAudits] sa
                CROSS JOIN (SELECT TOP 1 [Id] FROM [dbo].[Warehouses] ORDER BY [Id]) w
                WHERE sa.[WarehouseId] IS NULL;
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[FK_StockAudits_Warehouses_WarehouseId]', N'F') IS NULL
               AND OBJECT_ID(N'[dbo].[StockAudits]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockAudits', 'WarehouseId') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL
            ALTER TABLE [dbo].[StockAudits] WITH CHECK ADD CONSTRAINT [FK_StockAudits_Warehouses_WarehouseId]
                FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]);
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockAudits]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockAudits', 'WarehouseId') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'dbo.StockAudits') AND name = N'IX_StockAudits_WarehouseId_AuditDateUtc')
                CREATE NONCLUSTERED INDEX [IX_StockAudits_WarehouseId_AuditDateUtc]
                    ON [dbo].[StockAudits]([WarehouseId], [AuditDateUtc]);
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockAuditLines]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockAuditLines', 'ReasonCode') IS NULL
                ALTER TABLE [dbo].[StockAuditLines] ADD [ReasonCode] NVARCHAR(50) NOT NULL
                    CONSTRAINT [DF_StockAuditLines_ReasonCode_Scratch] DEFAULT(N'PhysicalCount');
            """);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[StockMovements]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.StockMovements', 'SourcePurchaseId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[StockMovements] ADD [SourcePurchaseId] INT NULL;
                CREATE NONCLUSTERED INDEX [IX_StockMovements_SourcePurchaseId] ON [dbo].[StockMovements]([SourcePurchaseId]);
                IF OBJECT_ID(N'[dbo].[Purchases]', N'U') IS NOT NULL
                    ALTER TABLE [dbo].[StockMovements] WITH CHECK ADD CONSTRAINT [FK_StockMovements_Purchases_SourcePurchaseId]
                        FOREIGN KEY ([SourcePurchaseId]) REFERENCES [dbo].[Purchases]([Id]);
            END
            """);
    }

    private static async Task EnsureExpensesTableAsync(OilChangePosDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[Expenses]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Expenses](
                    [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Expenses] PRIMARY KEY,
                    [Amount] DECIMAL(18,2) NOT NULL,
                    [Category] NVARCHAR(80) NOT NULL,
                    [Description] NVARCHAR(500) NOT NULL,
                    [ExpenseDateUtc] DATETIME2 NOT NULL,
                    [WarehouseId] INT NULL,
                    [CreatedByUserId] INT NOT NULL
                );
                CREATE NONCLUSTERED INDEX [IX_Expenses_ExpenseDateUtc] ON [dbo].[Expenses]([ExpenseDateUtc]);
                CREATE NONCLUSTERED INDEX [IX_Expenses_WarehouseId] ON [dbo].[Expenses]([WarehouseId]);
                IF OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL
                    ALTER TABLE [dbo].[Expenses] ADD CONSTRAINT [FK_Expenses_Warehouses_WarehouseId]
                        FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]);
                IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NOT NULL
                    ALTER TABLE [dbo].[Expenses] ADD CONSTRAINT [FK_Expenses_Users_CreatedByUserId]
                        FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([Id]);
            END
            """);
    }

    private static async Task EnsureUserHomeBranchWarehouseSchemaAsync(OilChangePosDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Users', N'HomeBranchWarehouseId') IS NULL
                    ALTER TABLE [dbo].[Users] ADD [HomeBranchWarehouseId] INT NULL;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'IX_Users_HomeBranchWarehouseId')
                BEGIN
                    CREATE INDEX [IX_Users_HomeBranchWarehouseId] ON [dbo].[Users]([HomeBranchWarehouseId]);
                END

                IF OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
                         AND name = N'FK_Users_Warehouses_HomeBranchWarehouseId')
                BEGIN
                    ALTER TABLE [dbo].[Users] ADD CONSTRAINT [FK_Users_Warehouses_HomeBranchWarehouseId]
                        FOREIGN KEY ([HomeBranchWarehouseId]) REFERENCES [dbo].[Warehouses]([Id]) ON DELETE SET NULL;
                END
            END
            """);
    }

    private static async Task EnsureBranchProductPricesTableAsync(OilChangePosDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[BranchProductPrices]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[BranchProductPrices](
                    [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BranchProductPrices] PRIMARY KEY,
                    [WarehouseId] INT NOT NULL,
                    [ProductId] INT NOT NULL,
                    [SalePrice] DECIMAL(18,2) NOT NULL
                );
                CREATE UNIQUE NONCLUSTERED INDEX [IX_BranchProductPrices_WarehouseId_ProductId]
                    ON [dbo].[BranchProductPrices]([WarehouseId], [ProductId]);
                IF OBJECT_ID(N'[dbo].[Warehouses]', N'U') IS NOT NULL
                    ALTER TABLE [dbo].[BranchProductPrices] ADD CONSTRAINT [FK_BranchProductPrices_Warehouses_WarehouseId]
                        FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[Warehouses]([Id]);
                IF OBJECT_ID(N'[dbo].[Products]', N'U') IS NOT NULL
                    ALTER TABLE [dbo].[BranchProductPrices] ADD CONSTRAINT [FK_BranchProductPrices_Products_ProductId]
                        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]);
            END
            """);
    }

    /// <summary>Migrate <c>Products.CompanyName</c> to <c>Companies</c> + <c>Products.CompanyId</c> for EnsureCreated databases.</summary>
    /// <remarks>
    /// Each upgrade path uses its own SQL batch. SQL Server compiles a batch before running it; two mutually
    /// exclusive <c>IF</c> branches that mention <c>CompanyId</c> in one batch yield error 207 when the column
    /// does not exist yet, even when the branch that references it should be skipped.
    /// </remarks>
    private static async Task EnsureCatalogCompaniesAsync(OilChangePosDbContext dbContext)
    {
        var db = dbContext.Database;

        await db.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[Companies]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Companies](
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(120) NOT NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_Companies_IsActive_Scratch] DEFAULT(1)
                );
                CREATE UNIQUE NONCLUSTERED INDEX [IX_Companies_Name] ON [dbo].[Companies]([Name]);
            END
            """);

        if (!await DboProductsTableExistsAsync(db))
            return;

        var hasCompanyName = await DboProductsColumnExistsAsync(db, "CompanyName");
        var hasCompanyId = await DboProductsColumnExistsAsync(db, "CompanyId");

        if (hasCompanyId && hasCompanyName)
        {
            await db.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH(N'dbo.Products', N'CompanyName') IS NOT NULL
                BEGIN
                    DECLARE @cnDf SYSNAME;
                    DECLARE @dropCnSql NVARCHAR(512);
                    SELECT @cnDf = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Products') AND c.name = N'CompanyName';
                    IF @cnDf IS NOT NULL
                    BEGIN
                        SET @dropCnSql = N'ALTER TABLE [dbo].[Products] DROP CONSTRAINT ' + QUOTENAME(@cnDf) + N';';
                        EXEC (@dropCnSql);
                    END
                    ALTER TABLE [dbo].[Products] DROP COLUMN [CompanyName];
                END
                """);
            return;
        }

        if (hasCompanyId)
            return;

        if (hasCompanyName)
        {
            // Separate batches: SQL Server can reject references to CompanyId in the same batch as ALTER ADD (207) during compilation.
            await db.ExecuteSqlRawAsync(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_CompanyName_Name_ProductCategory_PackageSize')
                    DROP INDEX [IX_Products_CompanyName_Name_ProductCategory_PackageSize] ON [dbo].[Products];

                IF COL_LENGTH(N'dbo.Products', N'CompanyId') IS NULL
                    ALTER TABLE [dbo].[Products] ADD [CompanyId] INT NULL;
                """);

            await db.ExecuteSqlRawAsync(
                """
                INSERT INTO [dbo].[Companies] ([Name], [IsActive])
                SELECT DISTINCT LTRIM(RTRIM(p0.[CompanyName])), 1
                FROM [dbo].[Products] p0
                WHERE LTRIM(RTRIM(ISNULL(p0.[CompanyName], N''))) <> N''
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[Companies] c0
                      WHERE c0.[Name] = LTRIM(RTRIM(p0.[CompanyName])));

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Companies] WHERE [Name] = N'عام')
                    INSERT INTO [dbo].[Companies] ([Name], [IsActive]) VALUES (N'عام', 1);

                DECLARE @GeneralCompanyId INT = (SELECT TOP 1 [Id] FROM [dbo].[Companies] WHERE [Name] = N'عام');

                UPDATE p
                SET [CompanyId] = c.[Id]
                FROM [dbo].[Products] p
                INNER JOIN [dbo].[Companies] c ON c.[Name] = LTRIM(RTRIM(p.[CompanyName]))
                WHERE p.[CompanyId] IS NULL
                  AND LTRIM(RTRIM(ISNULL(p.[CompanyName], N''))) <> N'';

                UPDATE [dbo].[Products] SET [CompanyId] = @GeneralCompanyId WHERE [CompanyId] IS NULL;

                DECLARE @cnDfMigrate SYSNAME;
                DECLARE @dropCnMigrateSql NVARCHAR(512);
                SELECT @cnDfMigrate = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Products') AND c.name = N'CompanyName';
                IF @cnDfMigrate IS NOT NULL
                BEGIN
                    SET @dropCnMigrateSql = N'ALTER TABLE [dbo].[Products] DROP CONSTRAINT ' + QUOTENAME(@cnDfMigrate) + N';';
                    EXEC (@dropCnMigrateSql);
                END
                ALTER TABLE [dbo].[Products] DROP COLUMN [CompanyName];

                ALTER TABLE [dbo].[Products] ALTER COLUMN [CompanyId] INT NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_CompanyId_Name_ProductCategory_PackageSize')
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_Products_CompanyId_Name_ProductCategory_PackageSize]
                        ON [dbo].[Products]([CompanyId], [Name], [ProductCategory], [PackageSize]);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Companies_CompanyId')
                    ALTER TABLE [dbo].[Products] WITH CHECK ADD CONSTRAINT [FK_Products_Companies_CompanyId]
                        FOREIGN KEY ([CompanyId]) REFERENCES [dbo].[Companies]([Id]);
                """);
            return;
        }

        await db.ExecuteSqlRawAsync(
            """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_CompanyName_Name_ProductCategory_PackageSize')
                DROP INDEX [IX_Products_CompanyName_Name_ProductCategory_PackageSize] ON [dbo].[Products];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_Name_ProductCategory_PackageSize')
                DROP INDEX [IX_Products_Name_ProductCategory_PackageSize] ON [dbo].[Products];

            IF COL_LENGTH(N'dbo.Products', N'CompanyId') IS NULL
                ALTER TABLE [dbo].[Products] ADD [CompanyId] INT NULL;
            """);

        await db.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM [dbo].[Companies] WHERE [Name] = N'عام')
                INSERT INTO [dbo].[Companies] ([Name], [IsActive]) VALUES (N'عام', 1);

            DECLARE @GeneralOnlyId INT = (SELECT TOP 1 [Id] FROM [dbo].[Companies] WHERE [Name] = N'عام');
            UPDATE [dbo].[Products] SET [CompanyId] = @GeneralOnlyId WHERE [CompanyId] IS NULL;

            ALTER TABLE [dbo].[Products] ALTER COLUMN [CompanyId] INT NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_CompanyId_Name_ProductCategory_PackageSize')
                CREATE UNIQUE NONCLUSTERED INDEX [IX_Products_CompanyId_Name_ProductCategory_PackageSize]
                    ON [dbo].[Products]([CompanyId], [Name], [ProductCategory], [PackageSize]);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Products_Companies_CompanyId')
                ALTER TABLE [dbo].[Products] WITH CHECK ADD CONSTRAINT [FK_Products_Companies_CompanyId]
                    FOREIGN KEY ([CompanyId]) REFERENCES [dbo].[Companies]([Id]);
            """);
    }
}
