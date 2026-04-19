# OilChangePOS (Isolated WinForms Solution)

Production-oriented desktop POS and inventory system for oil/lubricant shop.

## Architecture

- `OilChangePOS.WinForms` (Presentation Layer)
- `OilChangePOS.Business` (Business Logic Layer)
- `OilChangePOS.Data` (Data Access Layer with EF Core + SQL Server)
- `OilChangePOS.Domain` (Entities and core types)
- `OilChangePOS.API` (HTTP API for WinForms and other clients)
- `web/` — **React (Vite + TypeScript)** UI in the same repo; not added to the `.sln`. See `web/README.md`.

## Key Rules Implemented

- Stock accuracy is based on `StockMovements` only.
- Every operation creates stock movement entries:
  - Purchase => `IN`
  - Sale => `OUT`
  - Oil service => `OUT`
  - Stock audit => `ADJUST`
- Sales and service operations are transaction-safe and reject insufficient stock.

## Default Credentials

- `admin` / `admin123`
- `cashier` / `admin123`

## Run Steps

1. Open terminal in `OilChangePOS`.
2. Create DB manually (optional if you prefer script-first):
   - Execute `Database/CreateDatabase.sql` in SQL Server Management Studio.
3. Update connection string in `OilChangePOS.WinForms/appsettings.json`.
4. Restore and run:
   - `dotnet restore`
   - `dotnet run --project OilChangePOS.WinForms/OilChangePOS.WinForms.csproj`

> On startup, the app uses `EnsureCreated()` and seeds default users/products if tables are empty.

## Sample Flow: Complete Sale + Stock Deduction

- UI POS tab provides quantity input and discount.
- `SalesService.CompleteSaleAsync(...)`:
  - Validates stock from `StockMovements`
  - Creates `Invoice` + `InvoiceItems`
  - Creates `StockMovements` with `OUT`
  - Commits one DB transaction

## Notes

- The current UI is a practical, extensible baseline for production hardening (invoice print templates, richer role checks, and advanced reporting can be layered on top).
