# C# / ASP.NET Vocabulary

Terms used in this Work-Item API, in plain language.

| Term | Meaning in this project |
| --- | --- |
| **Entity** | `WorkItem` is the object EF Core stores in SQLite. |
| **DTO** (data transfer object) | `WorkItemInput` describes client-supplied fields; `WorkItemResponse` describes what the API returns. Both live in `WorkItems.Contracts`, shared by the API and the desktop client. |
| **Record** | A concise C# type for data. Both DTOs are records. |
| **Enum** | A fixed set of named values. `WorkItemStatus` has `Todo`, `InProgress`, and `Done`. |
| **Nullable** | A value may be absent. `Description` and `DueDate` use `?`. |
| **Minimal API** | Routes are mapped with `MapGet`, `MapPost`, `MapPut`, and `MapDelete` in `WorkItemEndpoints.cs`; `Program.cs` only wires up services and middleware. |
| **Endpoint** | One HTTP method and path, such as `GET /workitems/{id}`. |
| **CRUD** | Create (`POST`), read (`GET`), update (`PUT`), and delete (`DELETE`). |
| **Dependency injection (DI)** | ASP.NET Core creates `WorkItemsDb` and passes it to endpoints that need it. |
| **DbContext** | EF Core's database session. `WorkItemsDb` exposes the work items and saves changes. |
| **DbSet** | An EF Core collection representing a database table: `WorkItemsDb.WorkItems`. |
| **LINQ** | C# query operators such as `Where`, `OrderBy`, and `Select`. EF Core translates supported queries into SQL. |
| **Change tracking** | EF Core notices edits to loaded entities so `SaveChangesAsync` can write them. `AsNoTracking` skips this for read-only queries. |
| **Async/await** | Lets a request wait for database work without blocking a server thread. |
| **Validation** | Checks input before saving. `AddValidation()` enforces the attributes on `WorkItemInput` (e.g. `[MaxLength]`) plus `IValidatableObject` rules; failures return HTTP 400 with field errors. |
| **ProblemDetails** | A standard JSON shape for API errors. Every error here uses it: validation, 404, 412/428, and unhandled exceptions. |
| **Status code** | The HTTP result: 201 created, 200 success, 204 deleted, 400 invalid input, 404 missing item, 412 stale version, 428 missing `If-Match`. |
| **Swagger / OpenAPI** | Documentation and an interactive page at `/swagger` for trying endpoints. |
| **SQLite** | The local file-based database used by this API. |
| **Migration** | A versioned database schema change. The API applies pending migrations at startup; new ones are generated with the repo-local `dotnet ef` tool. |
| **Avalonia** | The .NET UI framework used by `WorkItems.Desktop` to run a separate desktop client. |
| **Client** | An app that makes HTTP requests to the API. The Avalonia app and browser UI are both clients. |
| **Optimistic concurrency** | Each item has a `Version`. Updates send it in `If-Match`; if someone else saved first, the database update matches no row and the API returns 412. |
| **ETag / If-Match** | HTTP headers carrying the item's version: the API sends `ETag`, clients echo it back in `If-Match`. |
| **N+1 query** | Loading a list, then running one more query per item (e.g. for its tags). Avoided here by projecting tags inside the page query. |
| **Many-to-many** | Items and tags: an item has many tags, a tag has many items, linked by a join table EF creates. |
