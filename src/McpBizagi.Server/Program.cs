using McpBizagi.Core;
using McpBizagi.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// stdout belongs exclusively to MCP; diagnostics are sent to stderr.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
var configuration = ServerOptions.FromEnvironment();
using var stateLease = new StateLease(configuration.State);
builder.Services.AddSingleton(configuration);
builder.Services.AddSingleton(new WorkspaceFiles(configuration.Workspace));
builder.Services.AddSingleton<WorkerClient>();
builder.Services.AddSingleton<Operations>();
builder.Services.AddHostedService(services => services.GetRequiredService<Operations>());
builder.Services.AddSingleton<NativeWorkflows>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<ModelTools>();
await builder.Build().RunAsync();
