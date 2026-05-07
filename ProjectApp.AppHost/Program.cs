var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var ports = new[] { 7173, 7174, 7175 };

var gateway = builder.AddProject<Projects.ProjectApp_ApiGateway>("projectapp-apigateway")
    .WithEnvironment("ASPNETCORE_URLS", "https://localhost:7139;http://localhost:5139")
    .WithEndpoint("https", endpoint =>
    {
        endpoint.IsProxied = false;
        endpoint.Port = 7139;
        endpoint.TargetPort = 7139;
    })
    .WithEndpoint("http", endpoint =>
    {
        endpoint.IsProxied = false;
        endpoint.Port = 5139;
        endpoint.TargetPort = 5139;
    });

for (var i = 0; i < 3; i++)
{
    var api = builder.AddProject<Projects.ProjectApp_Api>($"projectapp-api-{i}")
        .WithReference(redis)
        .WithEnvironment("ASPNETCORE_URLS", $"http://localhost:{ports[i].ToString()}")
        .WithEndpoint("http", endpoint =>
        {
            endpoint.IsProxied = false;
            endpoint.Port = ports[i];
            endpoint.TargetPort = ports[i];
        })
        .WaitFor(redis);

    gateway.WithReference(api).WaitFor(api);
}

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WithHttpEndpoint(port: 5127, name: "client")
    .WaitFor(gateway);

builder.Build().Run();
