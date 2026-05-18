using Amazon.SQS;
using LocalStack.Client.Extensions;
using ProjectApp.ServiceDefaults;
using Service.FileStorage.Messaging;
using Service.FileStorage.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonSQS>();
builder.Services.AddHostedService<SqsConsumerService>();

builder.AddMinioClient("projectapp-minio");
builder.Services.AddSingleton<UploadTracker>();
builder.Services.AddScoped<IFileStorageService, MinioFileStorageService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
    await storage.EnsureBucketExists();
}

app.MapDefaultEndpoints();
app.MapControllers();
app.Run();
