using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Toolbelt.Blazor.WebAssembly.DevServer.Extensions.UserSecrets;

public class UserSecretsExtensionStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        // Get the user secrets ID from environment variable
        var userSecretsId = Environment.GetEnvironmentVariable("DOTNET_USER_SECRETS_ID");
        if (string.IsNullOrEmpty(userSecretsId)) return app => next(app);

        return app =>
        {
            // Load user secrets to get the path to the secrets JSON file
            var secretJsonPath = GetSecretJsonPath(userSecretsId);

            // Add middleware to merge secrets into appsettings responses
            app.Use(async (context, nextMiddleware) => await MergeSecretsIntoAppSettingsResponse(context, nextMiddleware, secretJsonPath));

            // Call the next middleware in the pipeline
            next(app);
        };
    }

    private static string GetSecretJsonPath(string userSecretsId)
    {
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddUserSecrets(userSecretsId, reloadOnChange: true);
        var configuration = configurationBuilder.Build();
        var jsonProvider = configuration.Providers.OfType<JsonConfigurationProvider>().First();
        var fileInfo = jsonProvider.Source.FileProvider?.GetFileInfo(jsonProvider.Source.Path ?? "");
        return fileInfo?.PhysicalPath ?? throw new InvalidOperationException("Could not find the path to the secrets JSON file.");
    }

    private static async Task MergeSecretsIntoAppSettingsResponse(HttpContext context, Func<Task> nextMiddleware, string secretJsonPath)
    {
        // Filter requests to appsettings.*.json
        var requestPath = context.Request.Path.Value;
        if (!(requestPath?.StartsWith("/appsettings.") ?? false) || !requestPath.EndsWith(".json") || !File.Exists(secretJsonPath))
        {
            await nextMiddleware();
            return;
        }

        // Disable caching and compression by removing relevant headers
        RemoveRequestHeaders(context, ["Accept-Encoding", "Cache-Control", "If-Modified-Since", "If-None-Match"]);

        // Capture the original response body
        var originalBody = context.Response.Body;
        var memStream = new MemoryStream();
        context.Response.Body = memStream;

        await nextMiddleware();

        context.Response.Body = originalBody;

        var originalResponseBytes = memStream.ToArray();
        var responseBody = Encoding.UTF8.GetString(originalResponseBytes);

        var secretJsonText = await File.ReadAllTextAsync(secretJsonPath);

        // Merge JSON in responseBody with secrets JSON
        var mergedJson = MergeJsonStrings(responseBody, secretJsonText);

        var mergedBytes = Encoding.UTF8.GetBytes(mergedJson);
        context.Response.ContentLength = mergedBytes.Length;
        await originalBody.WriteAsync(mergedBytes, 0, mergedBytes.Length);
    }

    private static void RemoveRequestHeaders(HttpContext context, IEnumerable<string> headers)
    {
        foreach (var header in headers) context.Request.Headers.Remove(header);
    }

    internal static string MergeJsonStrings(string baseJson, string overrideJson)
    {
        var baseNode = JsonNode.Parse(baseJson);
        var overrideNode = JsonNode.Parse(overrideJson);

        if (baseNode is JsonObject baseObj) ExpandColonDelimitedKeys(baseObj);
        if (overrideNode is JsonObject overrideObj) ExpandColonDelimitedKeys(overrideObj);

        var mergedNode = MergeJson(baseNode, overrideNode);
        return mergedNode is null
            ? "null"
            : mergedNode.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });
    }

    private static void ExpandColonDelimitedKeys(JsonObject obj)
    {
        var colonKeys = obj
            .Where(kvp => kvp.Key.Contains(':'))
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToArray();

        foreach (var (key, value) in colonKeys)
        {
            obj.Remove(key);

            var segments = key.Split(':');
            var current = obj;

            for (var i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i];
                if (current[segment] is JsonObject existing)
                {
                    current = existing;
                }
                else
                {
                    var newObj = new JsonObject();
                    current[segment] = newObj;
                    current = newObj;
                }
            }

            current[segments[^1]] = value?.DeepClone();
        }

        foreach (var (_, value) in obj)
        {
            if (value is JsonObject child)
            {
                ExpandColonDelimitedKeys(child);
            }
        }
    }

    private static JsonNode? MergeJson(JsonNode? baseNode, JsonNode? overrideNode)
    {
        if (overrideNode is null) return null;

        if (baseNode is null) return overrideNode.DeepClone();

        if (baseNode is JsonObject baseObj && overrideNode is JsonObject overrideObj)
        {
            var merged = baseObj.DeepClone() as JsonObject ?? new JsonObject();

            foreach (var (key, value) in overrideObj)
            {
                if (merged[key] is JsonObject existingObj && value is JsonObject overrideChild)
                {
                    merged[key] = MergeJson(existingObj, overrideChild);
                }
                else
                {
                    merged[key] = value?.DeepClone();
                }
            }

            return merged;
        }

        return overrideNode.DeepClone();
    }
}
