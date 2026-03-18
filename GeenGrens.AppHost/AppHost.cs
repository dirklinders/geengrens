
using System.Diagnostics;
using System.Runtime.InteropServices;

var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.GeenGrens_ApiService>("apiservice")
    .WithUrl("/swagger")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.GeenGrens_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.AddProject<Projects.Blog_Web>("blog-web");


var blogUrl = "https://localhost:7033";   // Aspire dashboard
var swaggerUrl = "https://localhost:7424/swagger"; // Swagger UI of your API
var frontendUrl = "https://localhost:7220";    // Your frontend

void OpenUrl(string url)
{
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
    }
    catch
    {
        // ignored — might not open in some environments
    }
}

// Open additional tabs on startup
//OpenUrl(dashboardUrl);

Task.Run(async () =>
{
    await Task.Delay(3000); // Wait 1 second asynchronously
    OpenUrl(swaggerUrl);
    OpenUrl(frontendUrl);
    OpenUrl(blogUrl);
});



builder.Build().Run();

