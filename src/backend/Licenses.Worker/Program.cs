using Licenses.Infrastructure;
using Licenses.Infrastructure.Notifications;
using Licenses.Worker;
using Licenses.Application.Notifications;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<OutboxProcessingOptions>(builder.Configuration.GetSection("OutboxProcessing"));
builder.Services.AddScoped<INotificationDeliveryPipeline, OutboxNotificationDeliveryPipeline>();
builder.Services.AddScoped<OutboxMessageProcessor>();
builder.Services.AddScoped<INotificationSender, DevelopmentLoggingNotificationSender>();
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<INotificationSender, UnconfiguredNotificationSender>();
}
builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();
host.Run();
