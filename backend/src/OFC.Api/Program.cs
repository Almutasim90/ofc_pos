using OFC.Api;
using OFC.Infrastructure;
using OFC.Api.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapSprintOneEndpoints();
app.MapSprintTwoEndpoints();
app.MapSprintThreeEndpoints();
app.MapSprintFourEndpoints();
app.MapSprintFiveEndpoints();
app.MapSprintSixEndpoints();
app.MapSprintSevenEndpoints();
app.MapSprintEightEndpoints();
app.MapSprintNineEndpoints();
app.MapSprintTenEndpoints();
app.MapSprintElevenEndpoints();
app.MapSprintTwelveEndpoints();
app.MapSprintThirteenEndpoints();
app.MapSprintFourteenEndpoints();
app.MapSprintFifteenEndpoints();
app.MapSprintSixteenEndpoints();

app.Run();

public partial class Program;
