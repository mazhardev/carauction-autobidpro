using System.Security.Claims;
using AuctionService.Data;
using AuctionService.RequestHelpers;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.UnitTests;

internal static class AuctionTestHelpers
{
    /// <summary>
    /// The claim type AuctionService configures as
    /// <c>TokenValidationParameters.NameClaimType</c>, and the claim IdentityService
    /// issues from <c>CustomProfiileService</c>.
    /// </summary>
    public const string UsernameClaimType = "username";

    public static AuctionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase($"auctions-{Guid.NewGuid()}")
            .Options;

        return new AuctionDbContext(options);
    }

    public static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<MappingProfiles>()).CreateMapper();

    /// <summary>
    /// Builds the principal exactly the way the JWT bearer handler does once
    /// <c>NameClaimType = "username"</c> is applied, so User.Identity.Name is the
    /// marketplace username.
    /// </summary>
    public static ClaimsPrincipal AuthenticatedAs(string username) =>
        new(new ClaimsIdentity(
            [new Claim(UsernameClaimType, username)],
            authenticationType: "Bearer",
            nameType: UsernameClaimType,
            roleType: ClaimTypes.Role));

    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    public static T WithUser<T>(this T controller, ClaimsPrincipal user) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }
}
