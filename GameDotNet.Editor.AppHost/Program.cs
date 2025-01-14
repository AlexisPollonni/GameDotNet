using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<GameDotNet_Editor>("frontend").WithOtlpExporter();

builder.Build().Run();