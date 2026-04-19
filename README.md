# OilChangePOS (API + React)

POS and inventory backend for oil/lubricant retail, with a **browser UI** in `web/`.

## Architecture

- `OilChangePOS.API` — ASP.NET Core HTTP API (Kestrel)
- `OilChangePOS.Business` — business logic
- `OilChangePOS.Data` — EF Core + SQL Server
- `OilChangePOS.Domain` — entities and core types
- `web/` — React (Vite + TypeScript); run separately with npm. See `web/README.md`.

## Key rules

- Stock is derived from `StockMovements`.
- Operations create movements: purchase, sale, transfer, audit adjustment.
- Sales reject insufficient stock inside a single transaction.

## Default credentials (seeded dev DB)

- `admin` / `admin123`
- `cashier` / `admin123`

## Run (development)

1. Set the connection string in `OilChangePOS.API/appsettings.json` (or user secrets).
2. Apply EF migrations, e.g.  
   `dotnet ef database update --project OilChangePOS.Data --startup-project OilChangePOS.Data`  
   (or `--startup-project OilChangePOS.API` if you prefer the API host).
3. Start the API:  
   `dotnet run --project OilChangePOS.API/OilChangePOS.API.csproj`  
   **Visual Studio:** Right-click **OilChangePOS.API** → **Set as Startup Project**, then F5. If debugging still says the startup project cannot be launched, close VS, delete the **`.vs`** folder beside `OilChangePOS.sln`, reopen the solution, and set **OilChangePOS.API** as the startup project again (clears a stale reference after removing another host project).
4. Start the web app from `web/`: `npm install` then `npm run dev` (see `web/README.md`).

> On first API startup, the host may seed default users/products when the database is empty (see API startup / initializer).

## Sample flow: sale + stock

`SalesService.CompleteSaleAsync` validates stock, writes `Invoice` / `InvoiceItems`, and `StockMovements` (sale OUT) in one transaction.
