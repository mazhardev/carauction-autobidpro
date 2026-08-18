using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace GatewayService.IntegrationTests;

/// <summary>
/// Boots the real gateway pipeline (routing → CORS → authentication → authorization →
/// YARP) and swaps only the token signature validation, so tests never need a running
/// IdentityService. Signature validation itself stays on: tokens must be signed with
/// <see cref="SigningKey"/> or they are rejected.
/// </summary>
public class GatewayApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Test-only signing key. Not a credential used by any environment.</summary>
    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("gateway-integration-tests-signing-key-not-a-real-secret"));

    /// <summary>Nothing listens on port 1, so every forwarded request fails at the proxy hop.</summary>
    private const string DeadDestination = "http://127.0.0.1:1/";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The Development configuration is the one that defines the YARP clusters.
        builder.UseEnvironment("Development");

        // Point every cluster at a destination that is guaranteed to be down, so an
        // authorized request deterministically fails with a proxy error instead of
        // depending on whichever service happens to be running on the dev machine.
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:auctions:Destinations:auctionApi:Address"] = DeadDestination,
                ["ReverseProxy:Clusters:search:Destinations:searchApi:Address"] = DeadDestination,
                ["ReverseProxy:Clusters:bids:Destinations:bidApi:Address"] = DeadDestination,
                ["ReverseProxy:Clusters:notifications:Destinations:notificationsHub:Address"] = DeadDestination
            }));

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.ConfigurationManager = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = SigningKey,
                    NameClaimType = "username",
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    public static string CreateToken(string username) =>
        Write(username, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), SigningKey);

    public static string CreateExpiredToken(string username) =>
        Write(username, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5), SigningKey);

    /// <summary>A syntactically valid JWT signed with a key the gateway does not trust.</summary>
    public static string CreateTokenWithUntrustedKey(string username)
    {
        var otherKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("a-completely-different-key-that-the-gateway-rejects"));

        return Write(username, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), otherKey);
    }

    private static string Write(string username, DateTime notBefore, DateTime expires,
        SymmetricSecurityKey key)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim("username", username)]),
            IssuedAt = notBefore,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });

        return handler.WriteToken(token);
    }
}
