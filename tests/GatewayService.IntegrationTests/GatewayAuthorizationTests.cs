using System.Net;
using System.Net.Http.Headers;

namespace GatewayService.IntegrationTests;

/// <summary>
/// Exercises the gateway authorization pipeline end to end. The downstream services are
/// not running, so an allowed request fails at the proxy hop (502/503/504) rather than
/// returning 200 — the assertions therefore check that a request was *not* rejected with
/// 401/403 and that it matched a route (not 404).
/// </summary>
public class GatewayAuthorizationTests(GatewayApplicationFactory factory)
    : IClassFixture<GatewayApplicationFactory>
{
    private static readonly string AuctionId = Guid.NewGuid().ToString();

    private HttpClient Client => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    // ---------------------------------------------------------------------
    // Anonymous access is rejected on write routes
    // ---------------------------------------------------------------------

    public static TheoryData<string, string> ProtectedRoutes() => new()
    {
        { "POST", "/auctions" },
        { "PUT", "/auctions/{id}" },
        { "DELETE", "/auctions/{id}" },
        { "POST", "/bids?auctionId={id}&amount=100" }
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_RejectsAnonymousRequest(string method, string path)
    {
        var response = await Client.SendAsync(BuildRequest(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_RejectsTokenSignedWithAnUntrustedKey(string method, string path)
    {
        var request = BuildRequest(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            GatewayApplicationFactory.CreateTokenWithUntrustedKey("attacker"));

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_RejectsAnExpiredToken(string method, string path)
    {
        var request = BuildRequest(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            GatewayApplicationFactory.CreateExpiredToken("bob"));

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_RejectsAGarbageBearerToken()
    {
        var request = BuildRequest("POST", "/auctions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // Authenticated requests are forwarded to the downstream service
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_ForwardsAuthenticatedRequest(string method, string path)
    {
        var request = BuildRequest(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            GatewayApplicationFactory.CreateToken("bob"));

        var response = await Client.SendAsync(request);

        AssertReachedTheProxyHop(response.StatusCode);
    }

    // ---------------------------------------------------------------------
    // Public browsing stays anonymous
    // ---------------------------------------------------------------------

    public static TheoryData<string> PublicRoutes() =>
    [
        "/search?searchTerm=ford",
        "/auctions",
        "/auctions/{id}",
        "/bids/{id}"
    ];

    [Theory]
    [MemberData(nameof(PublicRoutes))]
    public async Task PublicRoute_AllowsAnonymousRequest(string path)
    {
        var response = await Client.SendAsync(BuildRequest("GET", path));

        AssertReachedTheProxyHop(response.StatusCode);
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// The route matched and authorization let the request through; the only thing left
    /// is the (deliberately unreachable) downstream service.
    /// </summary>
    private static void AssertReachedTheProxyHop(HttpStatusCode status) =>
        Assert.Contains(status, new[]
        {
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout
        });

    private static HttpRequestMessage BuildRequest(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", AuctionId));

        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        return request;
    }
}

/// <summary>
/// The gateway called <c>UseCors()</c> without a default policy being configured, which
/// made the CORS middleware a no-op for every route except "notifications".
/// </summary>
public class GatewayCorsTests(GatewayApplicationFactory factory)
    : IClassFixture<GatewayApplicationFactory>
{
    private const string ClientOrigin = "http://localhost:3000";

    [Fact]
    public async Task CrossOriginRequestFromTheClientApp_GetsCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auctions");
        request.Headers.Add("Origin", ClientOrigin);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(ClientOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task CrossOriginRequestFromAnUnknownOrigin_GetsNoCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auctions");
        request.Headers.Add("Origin", "http://evil.example.com");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
