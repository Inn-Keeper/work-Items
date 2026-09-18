namespace WorkItems.Api;

/// <summary>ETag / If-Match handling for optimistic concurrency. The ETag is the item's Version.</summary>
internal static class Preconditions
{
    public static string ETag(int version) => $"\"{version}\"";

    // Only a single strong tag like "3" is a version. Weak tags (W/"3") can't be used with If-Match
    // (RFC 9110), and "*" or a list can't identify the version the client edited.
    public static int? IfMatchVersion(HttpRequest request)
    {
        var value = request.Headers.IfMatch.ToString();
        return value.Length > 2 && value[0] == '"' && value[^1] == '"' && int.TryParse(value[1..^1], out var version)
            ? version : null;
    }

    public static IResult IfMatchError(HttpRequest request) => request.Headers.IfMatch.Count == 0
        ? Results.Problem(statusCode: StatusCodes.Status428PreconditionRequired,
            title: "If-Match header required", detail: "Send If-Match with the item's current ETag (its version).")
        : Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid If-Match header", detail: "Send a single strong ETag, e.g. If-Match: \"3\".");

    public static IResult VersionConflict() => Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed,
        title: "Item was changed elsewhere", detail: "Reload the item, or retry with its current version to overwrite.");
}
