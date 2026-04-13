IF DB_ID('OilChangePOSDb') IS NULL
BEGIN
    CREATE DATABASE OilChangePOSDb;
END
GO

USE OilChangePOSDb;
GO

CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    Role INT NOT NULL CHECK (Role IN (1,2)),
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Warehouses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Type INT NOT NULL CHECK (Type IN (1, 2)),
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Products (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    ProductCategory NVARCHAR(50) NOT NULL,
    PackageSize NVARCHAR(20) NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Products_NameCategoryPack UNIQUE (Name, ProductCategory, PackageSize)
);

CREATE TABLE Customers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(200) NOT NULL,
    PhoneNumber NVARCHAR(20) NOT NULL
);
CREATE INDEX IX_Customers_PhoneNumber ON Customers(PhoneNumber);

CREATE TABLE Cars (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId INT NOT NULL,
    PlateNumber NVARCHAR(20) NOT NULL UNIQUE,
    Make NVARCHAR(100) NOT NULL,
    Model NVARCHAR(100) NOT NULL,
    [Year] INT NULL,
    CONSTRAINT FK_Cars_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
);

CREATE TABLE Invoices (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceNumber NVARCHAR(30) NOT NULL UNIQUE,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CustomerId INT NULL,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    Subtotal DECIMAL(18,2) NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(30) NOT NULL DEFAULT N'Cash',
    CreatedByUserId INT NOT NULL,
    CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
    CONSTRAINT FK_Invoices_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
);
CREATE INDEX IX_Invoices_CreatedAtUtc ON Invoices(CreatedAtUtc);

CREATE TABLE InvoiceItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity DECIMAL(18,3) NOT NULL CHECK (Quantity > 0),
    UnitPrice DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
    LineTotal DECIMAL(18,2) NOT NULL CHECK (LineTotal >= 0),
    CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE,
    CONSTRAINT FK_InvoiceItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
CREATE INDEX IX_InvoiceItems_InvoiceId ON InvoiceItems(InvoiceId);
CREATE INDEX IX_InvoiceItems_ProductId ON InvoiceItems(ProductId);

CREATE TABLE Services (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ServiceNumber NVARCHAR(30) NOT NULL UNIQUE,
    ServiceDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CustomerId INT NOT NULL,
    CarId INT NOT NULL,
    OdometerKm INT NOT NULL CHECK (OdometerKm >= 0),
    Subtotal DECIMAL(18,2) NOT NULL CHECK (Subtotal >= 0),
    Total DECIMAL(18,2) NOT NULL CHECK (Total >= 0),
    CreatedByUserId INT NOT NULL,
    CONSTRAINT FK_Services_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
    CONSTRAINT FK_Services_Cars FOREIGN KEY (CarId) REFERENCES Cars(Id),
    CONSTRAINT FK_Services_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
);
CREATE INDEX IX_Services_ServiceDateUtc ON Services(ServiceDateUtc);

CREATE TABLE ServiceDetails (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ServiceId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity DECIMAL(18,3) NOT NULL CHECK (Quantity > 0),
    UnitPrice DECIMAL(18,2) NOT NULL CHECK (UnitPrice >= 0),
    LineTotal DECIMAL(18,2) NOT NULL CHECK (LineTotal >= 0),
    CONSTRAINT FK_ServiceDetails_Services FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ServiceDetails_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
);
CREATE INDEX IX_ServiceDetails_ServiceId ON ServiceDetails(ServiceId);

CREATE TABLE StockAudits (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AuditDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Notes NVARCHAR(500) NULL,
    CreatedByUserId INT NOT NULL,
    WarehouseId INT NULL,
    [Status] INT NOT NULL DEFAULT(1),
    CONSTRAINT FK_StockAudits_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    CONSTRAINT FK_StockAudits_Warehouses FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id)
);

CREATE INDEX IX_StockAudits_WarehouseId_AuditDateUtc ON StockAudits(WarehouseId, AuditDateUtc);

CREATE TABLE StockAuditLines (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    StockAuditId INT NOT NULL,
    ProductId INT NOT NULL,
    SystemQuantity DECIMAL(18,3) NOT NULL,
    ActualQuantity DECIMAL(18,3) NOT NULL,
    ReasonCode NVARCHAR(50) NOT NULL DEFAULT(N'PhysicalCount'),
    CONSTRAINT FK_StockAuditLines_Audit FOREIGN KEY (StockAuditId) REFERENCES StockAudits(Id) ON DELETE CASCADE,
    CONSTRAINT FK_StockAuditLines_Product FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

CREATE TABLE StockMovements (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductId INT NOT NULL,
    MovementType INT NOT NULL CHECK (MovementType IN (1, 2, 3, 4)),
    Quantity DECIMAL(18,3) NOT NULL CHECK (Quantity > 0),
    FromWarehouseId INT NULL,
    ToWarehouseId INT NULL,
    [Date] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ReferenceId INT NULL,
    Notes NVARCHAR(500) NULL,
    CONSTRAINT FK_StockMovements_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
    CONSTRAINT FK_StockMovements_FromWarehouse FOREIGN KEY (FromWarehouseId) REFERENCES Warehouses(Id),
    CONSTRAINT FK_StockMovements_ToWarehouse FOREIGN KEY (ToWarehouseId) REFERENCES Warehouses(Id)
);
CREATE INDEX IX_StockMovements_ProductDate ON StockMovements(ProductId, [Date]);
CREATE INDEX IX_StockMovements_WarehouseDate ON StockMovements(FromWarehouseId, ToWarehouseId, [Date]);

CREATE TABLE Purchases (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ProductId INT NOT NULL,
    Quantity DECIMAL(18,3) NOT NULL CHECK (Quantity > 0),
    PurchasePrice DECIMAL(18,2) NOT NULL CHECK (PurchasePrice >= 0),
    ProductionDate DATE NOT NULL,
    PurchaseDate DATE NOT NULL,
    WarehouseId INT NOT NULL,
    CreatedByUserId INT NOT NULL,
    Notes NVARCHAR(500) NULL,
    CONSTRAINT FK_Purchases_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
    CONSTRAINT FK_Purchases_Warehouses FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id),
    CONSTRAINT FK_Purchases_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
);

INSERT INTO Warehouses (Name, Type, IsActive)
VALUES (N'Main Warehouse', 1, 1),
       (N'Branch 1', 2, 1);

INSERT INTO Users (Username, PasswordHash, Role, IsActive)
VALUES
('admin', '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9', 1, 1),
('cashier', '240BE518FABD2724DDB6F04EEB1DA5967448D7E831C08C8FA822809F74C720A9', 2, 1);
GO
