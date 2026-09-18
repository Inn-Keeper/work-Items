# C# / ASP.NET Vocabulary

Terms used in this Work-Item API, in plain language.

| Term | Meaning in this project |
| --- | --- |
| **Entity** | `WorkItem` is the object EF Core stores in SQLite. |
| **DTO** (data transfer object) | `WorkItemInput` describes client-supplied fields; `WorkItemResponse` describes what the API returns. |
| **Record** | A concise C# type for data. Both DTOs are records. |
| **Enum** | A fixed set of named values. `WorkItemStatus` has `Todo`, `InProgress`, and `Done`. |
| **Nullable** | A value may be absent. `Description` and `DueDate` use `?`. |
| **Minimal API** | Routes are defined directly in `Program.cs` with `MapGet`, `MapPost`, `MapPut`, and `MapDelete`. |
| **Endpoint** | One HTTP method and path, such as `GET /workitems/{id}`. |
| **CRUD** | Create (`POST`), read (`GET`), update (`PUT`), and delete (`DELETE`). |
| **Dependency injection (DI)** | ASP.NET Core creates `WorkItemsDb` and passes it to endpoints that need it. |
| **DbContext** | EF Core's database session. `WorkItemsDb` exposes the work items and saves changes. |
| **DbSet** | An EF Core collection representing a database table: `WorkItemsDb.WorkItems`. |
| **LINQ** | C# query operators such as `Where`, `OrderBy`, and `Select`. EF Core translates supported queries into SQL. |
| **Change tracking** | EF Core notices edits to loaded entities so `SaveChangesAsync` can write them. `AsNoTracking` skips this for read-only queries. |
| **Async/await** | Lets a request wait for database work without blocking a server thread. |
| **Validation** | Checks input before saving; an invalid title or status returns HTTP 400 with field errors. |
| **ProblemDetails** | A standard JSON shape for API errors, used here for validation errors. |
| **Status code** | The HTTP result: 201 created, 200 success, 204 deleted, 400 invalid input, or 404 missing item. |
| **Swagger / OpenAPI** | Documentation and an interactive page at `/swagger` for trying endpoints. |
| **SQLite** | The local file-based database used by this API. |
| **Migration** | A versioned database schema change. The API applies its initial migration at startup. |
| **Avalonia** | The .NET UI framework used by `WorkItems.Desktop` to run a separate desktop client. |
| **Client** | An app that makes HTTP requests to the API. The Avalonia app and browser UI are both clients. |
