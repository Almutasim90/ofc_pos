using OFC.PrintAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOptions<PrintAgentOptions>()
    .Bind(builder.Configuration.GetSection(PrintAgentOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiBaseUrl), "PrintAgent:ApiBaseUrl is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.SessionToken), "PrintAgent:SessionToken is required. Sign in once via the web app and copy the session token.")
    .Validate(o => o.BranchId != Guid.Empty, "PrintAgent:BranchId is required.")
    .ValidateOnStart();
builder.Services.AddHttpClient<PrintApiClient>();
builder.Services.AddHostedService<PrintAgentWorker>();

var host = builder.Build();
host.Run();
