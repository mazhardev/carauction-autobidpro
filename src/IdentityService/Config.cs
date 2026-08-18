using Duende.IdentityServer.Models;

namespace IdentityService;

public static class Config
{
    // Development-only fallbacks. Override these in every non-development
    // environment via configuration/environment variables, e.g.
    //   Clients__NextApp__Secret, Clients__NextApp__RedirectUri
    private const string DevelopmentNextAppSecret = "secret";
    private const string DevelopmentPostmanSecret = "NotASecret";
    private const string DevelopmentNextAppRedirectUri = "http://localhost:3000/api/auth/callback/id-server";

    public static IEnumerable<IdentityResource> IdentityResources =>
        [
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
        ];

    public static IEnumerable<ApiScope> ApiScopes =>
        [
            new ApiScope("auctionApp", "Auction App Full Access")
        ];

    public static IEnumerable<Client> Clients(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var postmanSecret = configuration["Clients:Postman:Secret"] ?? DevelopmentPostmanSecret;
        var nextAppSecret = configuration["Clients:NextApp:Secret"] ?? DevelopmentNextAppSecret;
        var nextAppRedirectUri = configuration["Clients:NextApp:RedirectUri"] ?? DevelopmentNextAppRedirectUri;

        return
        [
            new Client
            {
                ClientId = "postman",
                ClientName = "Postman",
                AllowedScopes = { "openid", "profile", "auctionApp" },
                RedirectUris = { "https://www.getpostman.com/oauth2/callback" },
                ClientSecrets = [new Secret(postmanSecret.Sha256())],
                AllowedGrantTypes = { GrantType.ResourceOwnerPassword }
            },
            new Client
            {
                ClientId = "nextApp",
                ClientName = "nextApp",
                ClientSecrets = { new Secret(nextAppSecret.Sha256()) },
                AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                RequirePkce = false,
                RedirectUris = { nextAppRedirectUri },
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "auctionApp" },
                AccessTokenLifetime = 3600*24*30,
                AlwaysIncludeUserClaimsInIdToken = true
            }
        ];
    }
}
