using Aspire.Hosting.LocalStack.Container;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var ports = new[] { 7173, 7174 };
var disableClient =
    builder.Configuration.GetValue("DisableClient", false)
    || builder.Configuration.GetValue("DisableFrontend", false);
var apiCount = 2;

var gateway = builder.AddProject<Projects.ProjectApp_ApiGateway>("projectapp-apigateway");

var localstack = builder.AddLocalStack("projectapp-localstack", configureContainer: container =>
{
    container.Lifetime = ContainerLifetime.Session;
    container.DebugLevel = 1;
    container.LogLevel = LocalStackLogLevel.Debug;
    container.Port = 4566;
    container.AdditionalEnvironmentVariables.Add("DEBUG", "1");
});

var awsResources = builder.AddAWSCloudFormationTemplate("resources", "CloudFormation/project-template.yaml", "projectapp");

var minio = builder.AddMinioContainer("projectapp-minio");

for (var i = 0; i < apiCount; i++)
{
    var api = builder.AddProject<Projects.ProjectApp_Api>($"projectapp-api-{i}", launchProfileName: null)
        .WithReference(redis)
        .WithReference(awsResources)
        .WithHttpEndpoint(port: ports[i])
        .WaitFor(redis)
        .WaitFor(awsResources);

    gateway.WithReference(api).WaitFor(api);
}

builder.AddProject<Projects.Service_FileStorage>("service-filestorage")
    .WithReference(awsResources)
    .WithReference(minio)
    .WithHttpEndpoint(port: 7180)
    .WithEnvironment("AWS__Resources__MinioBucketName", "projectapp-bucket")
    .WaitFor(awsResources)
    .WaitFor(minio);

if (!disableClient)
{
    builder.AddProject<Projects.Client_Wasm>("client")
        .WithReference(gateway)
        .WithHttpEndpoint(port: 5127, name: "client")
        .WaitFor(gateway);
}

builder.UseLocalStack(localstack);

builder.Build().Run();
