using McpBizagi.Core;
using McpBizagi.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using System.Text.Json;
using System.Text.Json.Serialization;

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
// Use the official SDK's parameter marshaller, but never silently ignore unknown
// members of a typed request (including nested patches and mutation arrays).
var toolJson = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<ModelTools>(toolJson);
await builder.Build().RunAsync();
