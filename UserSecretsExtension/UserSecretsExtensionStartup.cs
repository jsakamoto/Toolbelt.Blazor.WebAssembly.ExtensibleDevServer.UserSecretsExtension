using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Toolbelt.Blazor.WebAssembly.DevServer.Extensions.UserSecrets;

[assembly: HostingStartup(typeof(UserSecretsExtensionStartup))]

namespace Toolbelt.Blazor.WebAssembly.DevServer.Extensions.UserSecrets;

public class UserSecretsExtensionStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, UserSecretsExtensionStartupFilter>();
        });
    }
}
