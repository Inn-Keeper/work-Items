# C# Educational Project — Scaffold Plan

**Goal:** Build a small, well-tested .NET Core Web API to sharpen C#/.NET fundamentals.

## Stack
- ASP.NET Core Web API (.NET 10)
- EF Core + SQLite (local) / Azure SQL (deployed)
- xUnit + WebApplicationFactory for testing
- Azure App Service for deployment
- Swagger/OpenAPI for exploration

## Domain
Simple **Task / Work-Item Tracker**:
- `WorkItem`: Id, Title, Description, Status (enum), DueDate, CreatedAt
- CRUD endpoints: `GET/POST /workitems`, `GET/PUT/DELETE /workitems/{id}`

## Concepts to Demonstrate
1. **C# fundamentals** — records, nullable reference types, LINQ, async/await
2. **ASP.NET Core** — minimal APIs or controllers, dependency injection, middleware, `ProblemDetails` error handling
3. **EF Core** — migrations, entity relationships, querying, common gotchas (tracking vs no-tracking, N+1 queries)
4. **DTOs vs entities** — don't leak the DB model through the API
5. **Validation** — `[Required]` attributes or FluentValidation
6. **Testing** — xUnit unit tests + integration tests via `WebApplicationFactory`
7. **Azure deployment** — App Service + Azure SQL

## Build Order
1. Scaffold project (`dotnet new webapi`)
2. Define `WorkItem` entity + DTOs
3. Add `DbContext` + initial migration
4. Implement endpoints incrementally, testing via Swagger
5. Add validation + error-handling middleware
6. Write unit + integration tests
7. Deploy to Azure App Service (+ Azure SQL)

## Deeper Topics
- Async/await patterns and common pitfalls (deadlocks, `ConfigureAwait`)
- DI lifetimes (Transient / Scoped / Singleton) and when each applies
- EF Core gotchas (change tracking, lazy loading, migrations in production)
- Testing strategy: walk through the xUnit / WebApplicationFactory setup
