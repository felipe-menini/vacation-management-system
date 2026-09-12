using Licenses.Infrastructure;
using Licenses.Infrastructure.Notifications;
using Licenses.Worker;
using Licenses.Application.LeaveManagement;
using Licenses.Application.Notifications;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<LeaveRequestCompletionService>();
builder.Services.AddScoped<ILeaveRequestCompletionProcessor, LeaveRequestCompletionProcessor>();
builder.Services.Configure<OutboxProcessingOptions>(builder.Configuration.GetSection("OutboxProcessing"));
builder.Services.AddOptions<LeaveCompletionOptions>()
    .Bind(builder.Configuration.GetSection(LeaveCompletionOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<LeaveCompletionOptions>, LeaveCompletionOptionsValidator>();
builder.Services.AddScoped<INotificationDeliveryPipeline, OutboxNotificationDeliveryPipeline>();
builder.Services.AddScoped<OutboxMessageProcessor>();
builder.Services.AddScoped<INotificationSender, DevelopmentLoggingNotificationSender>();
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<INotificationSender, UnconfiguredNotificationSender>();
}
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<LeaveRequestCompletionWorker>();

var host = builder.Build();
host.Run();
