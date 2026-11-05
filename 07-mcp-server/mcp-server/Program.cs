using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResumeMcpServer;

try
{
    //------------------------------------------------------------------------//
    //                              PREPARATION                               //
    //------------------------------------------------------------------------//

    // 1. Load the fictional records and check them before accepting tool calls.
    List<ResumeProfile> resumeProfiles = await ProfileLoader.LoadAsync(Path.Combine(
        AppContext.BaseDirectory,
        "SampleData",
        "profiles.json"));
    ProfileValidator.Validate(resumeProfiles);
    ServerSettings serverSettings = ServerSettings.Read();

    //------------------------------------------------------------------------//
    //                            MCP SERVER                                  //
    //------------------------------------------------------------------------//

    // 1. STDOUT belongs to MCP. Even the host's startup messages must go to STDERR.
    HostApplicationBuilder hostApplicationBuilder = Host.CreateApplicationBuilder();
    hostApplicationBuilder.Logging.ClearProviders();
    hostApplicationBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
    hostApplicationBuilder.Logging.AddConsole(consoleLoggerOptions => consoleLoggerOptions.LogToStandardErrorThreshold =
        LogLevel.Trace);
    hostApplicationBuilder.Services.AddSingleton(resumeProfiles);
    hostApplicationBuilder.Services.AddSingleton(serverSettings);
    hostApplicationBuilder.Services.AddSingleton<ResumeWriter>();
    hostApplicationBuilder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

    // 2. The client owns this process and closes it when the MCP connection ends.
    using IHost iHost = hostApplicationBuilder.Build();
    OutputPrinter.PrintIntroduction(serverSettings.AllowWriteBool);
    await iHost.RunAsync();
    return 0;
}
catch (Exception errorException)
{
    Console.Error.WriteLine(errorException.Message);
    return 1;
}
