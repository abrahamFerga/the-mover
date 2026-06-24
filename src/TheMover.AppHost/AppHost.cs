// TheMover.AppHost — ARCH.md: Containers / TheMover.AppHost (dev-time OTel dashboard only)
// Launch with: dotnet run --project src/TheMover.AppHost
// The dashboard opens at the URL printed to stdout.
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TheMover_App>("the-mover");

builder.Build().Run();
