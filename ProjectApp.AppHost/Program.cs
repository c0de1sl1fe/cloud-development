var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var api = builder.AddProject<Projects.ProjectApp_Api>("projectapp-api")
    .WithReference(redis)
    .WaitFor(redis);

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
