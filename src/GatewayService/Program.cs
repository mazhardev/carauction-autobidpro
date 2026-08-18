using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityServiceUrl"];
        // Metadata is fetched over plain HTTP inside the compose network; set
        // IdentityServiceRequireHttpsMetadata=true wherever the authority is HTTPS.
        options.RequireHttpsMetadata = builder.Configuration
            .GetValue("IdentityServiceRequireHttpsMetadata", false);
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.NameClaimType = "username";
    });

// "authenticated" is referenced by name from the protected ReverseProxy routes.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Authenticated, policy => policy.RequireAuthenticatedUser());
});

var clientApp = builder.Configuration["ClientApp"]
    ?? throw new InvalidOperationException("ClientApp configuration value is required for the CORS policy.");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(ConfigurePolicy);
    // Named policy referenced by the "notifications" ReverseProxy route.
    options.AddPolicy("customPolicy", ConfigurePolicy);

    void ConfigurePolicy(Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder b) => b
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithOrigins(clientApp);
});

var app = builder.Build();

// Order matters: routing selects the proxy endpoint (and its authorization
// metadata), then CORS/authentication/authorization run before the proxy
// forwards anything downstream.
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();

internal static class AuthorizationPolicies
{
    public const string Authenticated = "authenticated";
}

// Exposed so the integration test host can boot the real pipeline.
public partial class Program;
