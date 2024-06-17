var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.GameDotNet_Editor>("editor-frontend").WithOtlpExporter();

builder.Build().Run();