using System.Net;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Toolbelt;
using UserSecretsExtension.Test.Internals;

namespace UserSecretsExtension.Test;

/// <summary>
/// Runs the SampleApp, which references this extension, with "dotnet run" inside a disposable
/// Linux container, and verifies over HTTP that the User Secrets of the project are merged into
/// the "appsettings.*.json" responses. This is an E2E test.
///
/// The NuGet packages in the "_dist" folder are used as they are. If you have changed the
/// extension, please run "dotnet pack" on it before you run this test.
///
/// Because the container has a pristine NuGet global package cache and a pristine User Secrets
/// store, there is no risk of picking up anything left over on the host.
/// </summary>
[Parallelizable(ParallelScope.All)]
public class SampleAppE2ETests
{
    private const int ContainerPort = 8080;

    private const string ServerLogPath = "/tmp/sampleapp.log";

    private const string SampleAppProjectPath = "/work/SampleApp/SampleApp.csproj";

    private static async Task<IContainer> StartContainerAsync()
    {
        var container = new ContainerBuilder("mcr.microsoft.com/dotnet/sdk:10.0.400")
            .WithBindMount(PathUtils.SolutionDir, "/work")
            .WithWorkingDirectory("/work/SampleApp")
            .WithEntrypoint("tail", "-f", "/dev/null")
            .WithEnvironment("ASPNETCORE_URLS", $"http://0.0.0.0:{ContainerPort}")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithPortBinding(ContainerPort, assignRandomHostPort: true)
            .Build();
        await container.StartAsync();
        return container;
    }

    private static async Task StartSampleAppInContainerAsync(IContainer container)
    {
        await ExecOrFailAsync(container, "sh", "-c", $"dotnet run --no-launch-profile > {ServerLogPath} 2>&1 &");
        var waitResult = await container.ExecAsync(["timeout", "300", "sh", "-c", $"until grep -q 'Now listening on' {ServerLogPath} 2>/dev/null; do sleep 1; done"]);
        var serverLog = (await container.ExecAsync(["cat", ServerLogPath])).Stdout;
        waitResult.ExitCode.Is(0L, message: $"The sample app did not start listening.\n{serverLog}");
    }

    [Test]
    public async Task UserSecrets_OverrideAppSettingsResponses_ViaDevServerInContainer()
    {
        // GIVEN: Copy the solution folder tree into a temporary working folder (excluding bin/obj/.vs
        // folders). The "_dist" folder comes along, so the packages in it become the NuGet feed that
        // the sample app restores from inside the container.
        using var workspace = WorkDirectory.CreateCopyFrom(PathUtils.SolutionDir, entry => entry.Name is not "bin" and not "obj" and not ".vs");

        // GIVEN: Start the sample app in the background and wait for it to start listening
        await using (var container = await StartContainerAsync())
        {
            // WHEN: Start the sample app in the background and wait for it to start listening
            await StartSampleAppInContainerAsync(container);

            // "AutomaticDecompression" makes the client ask for a compressed response. The gateway
            // serves a pre-compressed copy of a static asset when it is asked to, and the extension has
            // to turn that off for itself. Otherwise it would have no plain JSON text to merge into.
            using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
            using var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(ContainerPort)}")
            };

            // THEN: No User Secrets are set yet, so both configuration files are served as they are
            (await GetFooBarAsync(httpClient, "/appsettings.json")).Is("Production");
            (await GetFooBarAsync(httpClient, "/appsettings.Development.json")).Is("Developers Shared");
        }

        await using (var container = await StartContainerAsync())
        {
            // GIVEN: Set the "Foo:Bar" configuration entry as a User Secret of the sample app project
            var secretValue = $"My Own Local Value ({Guid.NewGuid()})";
            await ExecOrFailAsync(container, "dotnet", "user-secrets", "set", "Foo:Bar", secretValue, "--project", SampleAppProjectPath);

            // WHEN: Start the sample app in the background and wait for it to start listening
            await StartSampleAppInContainerAsync(container);

            // "AutomaticDecompression" makes the client ask for a compressed response. The gateway
            // serves a pre-compressed copy of a static asset when it is asked to, and the extension has
            // to turn that off for itself. Otherwise it would have no plain JSON text to merge into.
            using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
            using var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(ContainerPort)}")
            };

            // THEN: Both configuration files are now served with the User Secret merged into them, and
            // the sample app did not have to be restarted for that
            (await GetFooBarAsync(httpClient, "/appsettings.json")).Is(secretValue);
            (await GetFooBarAsync(httpClient, "/appsettings.Development.json")).Is(secretValue);
        }
    }

    private static async Task<string?> GetFooBarAsync(HttpClient httpClient, string path)
    {
        var response = await httpClient.GetAsync(path);
        response.StatusCode.Is(HttpStatusCode.OK);

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["Foo"]?["Bar"]?.GetValue<string>();
    }

    private static async Task<string> ExecOrFailAsync(IContainer container, params string[] command)
    {
        var result = await container.ExecAsync(command);
        result.ExitCode.Is(0L, message: $"\"{string.Join(' ', command)}\" failed in the container (exit code {result.ExitCode}).\n{result.Stdout}\n{result.Stderr}");
        return result.Stdout;
    }
}
