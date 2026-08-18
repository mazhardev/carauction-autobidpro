using System.Reflection;
using System.Text.Json;
using AuctionService.Controllers;
using AuctionService.Data;
using AuctionService.DTOs;
using AuctionService.Entities;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AuctionService.UnitTests;

public class AuctionsControllerAuthorizationTests
{
    private const string Owner = "owner";
    private const string Attacker = "attacker";

    // ---------------------------------------------------------------------
    // Endpoint protection
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(AuctionsController.CreateAuction))]
    [InlineData(nameof(AuctionsController.UpdateAuction))]
    [InlineData(nameof(AuctionsController.DeleteAuction))]
    public void WriteEndpoints_RequireAuthorization(string action)
    {
        var method = typeof(AuctionsController).GetMethod(action)!;

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData(nameof(AuctionsController.GetAuctions))]
    [InlineData(nameof(AuctionsController.GetAuction))]
    public void ReadEndpoints_StayPublic(string action)
    {
        var method = typeof(AuctionsController).GetMethod(action)!;

        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    // ---------------------------------------------------------------------
    // The server owns the seller identity
    // ---------------------------------------------------------------------

    [Fact]
    public void CreateAuctionDto_HasNoSellerOrWinnerField()
    {
        var boundFromRequest = typeof(CreateAuctionDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("Seller", boundFromRequest, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Winner", boundFromRequest, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAuction_SetsSellerFromToken_AndIgnoresSellerInRequestBody()
    {
        await using var context = AuctionTestHelpers.CreateContext();
        var controller = new AuctionsController(context, AuctionTestHelpers.CreateMapper(),
                Mock.Of<IPublishEndpoint>())
            .WithUser(AuctionTestHelpers.AuthenticatedAs(Owner));

        // A malicious client tries to claim the auction for somebody else.
        var body = JsonSerializer.Deserialize<CreateAuctionDto>(
            MaliciousCreateAuctionJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var result = await controller.CreateAuction(body);

        Assert.IsType<CreatedAtActionResult>(result.Result);

        var saved = await context.Auctions.SingleAsync();
        Assert.Equal(Owner, saved.Seller);
        Assert.NotEqual(Attacker, saved.Seller);
    }

    [Fact]
    public async Task CreateAuction_ReturnsUnauthorized_WhenPrincipalHasNoUsername()
    {
        await using var context = AuctionTestHelpers.CreateContext();
        var controller = new AuctionsController(context, AuctionTestHelpers.CreateMapper(),
                Mock.Of<IPublishEndpoint>())
            .WithUser(AuctionTestHelpers.Anonymous());

        var result = await controller.CreateAuction(ValidCreateDto());

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Empty(await context.Auctions.ToListAsync());
    }

    // ---------------------------------------------------------------------
    // Ownership checks
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateAuction_ReturnsForbid_ForNonOwner()
    {
        await using var context = AuctionTestHelpers.CreateContext();
        var auction = await SeedAuctionAsync(context, Owner);
        var controller = new AuctionsController(context, AuctionTestHelpers.CreateMapper(),
                Mock.Of<IPublishEndpoint>())
            .WithUser(AuctionTestHelpers.AuthenticatedAs(Attacker));

        var result = await controller.UpdateAuction(auction.Id, new UpdateAuctionDto { Make = "Hacked" });

        Assert.IsType<ForbidResult>(result.Result);

        var reloaded = await context.Auctions.Include(x => x.Item).SingleAsync();
        Assert.Equal("Ford", reloaded.Item.Make);
    }

    [Fact]
    public async Task UpdateAuction_Succeeds_ForOwner()
    {
        await using var context = AuctionTestHelpers.CreateContext();
        var auction = await SeedAuctionAsync(context, Owner);
        var controller = new AuctionsController(context, AuctionTestHelpers.CreateMapper(),
                Mock.Of<IPublishEndpoint>())
            .WithUser(AuctionTestHelpers.AuthenticatedAs(Owner));

        var result = await controller.UpdateAuction(auction.Id, new UpdateAuctionDto { Make = "Ferrari" });

        Assert.IsType<OkResult>(result.Result);

        var reloaded = await context.Auctions.Include(x => x.Item).SingleAsync();
        Assert.Equal("Ferrari", reloaded.Item.Make);
    }

    [Fact]
    public async Task DeleteAuction_ReturnsForbid_ForNonOwner()
    {
        await using var context = AuctionTestHelpers.CreateContext();
        var auction = await SeedAuctionAsync(context, Owner);
        var controller = new AuctionsController(context, AuctionTestHelpers.CreateMapper(),
                Mock.Of<IPublishEndpoint>())
            .WithUser(AuctionTestHelpers.AuthenticatedAs(Attacker));

        var result = await controller.DeleteAuction(auction.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.NotEmpty(await context.Auctions.ToListAsync());
    }

    [Fact]
    public async Task DeleteAuction_Succeeds_ForOwner()
    {
        await using var context = AuctionTestHelpers.CreateContext();
        var auction = await SeedAuctionAsync(context, Owner);
        var controller = new AuctionsController(context, AuctionTestHelpers.CreateMapper(),
                Mock.Of<IPublishEndpoint>())
            .WithUser(AuctionTestHelpers.AuthenticatedAs(Owner));

        var result = await controller.DeleteAuction(auction.Id);

        Assert.IsType<OkResult>(result);
        Assert.Empty(await context.Auctions.ToListAsync());
    }

    // ---------------------------------------------------------------------

    private const string MaliciousCreateAuctionJson = """
        {
          "seller": "attacker",
          "make": "Ford",
          "model": "GT",
          "year": 2020,
          "color": "Blue",
          "mileage": 1000,
          "imageUrl": "https://example.com/car.jpg",
          "reservePrice": 20000,
          "auctionEnd": "2999-01-01T00:00:00Z"
        }
        """;

    private static CreateAuctionDto ValidCreateDto() => new()
    {
        Make = "Ford",
        Model = "GT",
        Year = 2020,
        Color = "Blue",
        Mileage = 1000,
        ImageUrl = "https://example.com/car.jpg",
        ReservePrice = 20000,
        AuctionEnd = DateTime.UtcNow.AddDays(10)
    };

    private static async Task<Auction> SeedAuctionAsync(AuctionDbContext context, string seller)
    {
        var auction = new Auction
        {
            Id = Guid.NewGuid(),
            Seller = seller,
            ReservePrice = 10000,
            AuctionEnd = DateTime.UtcNow.AddDays(7),
            Item = new Item
            {
                Make = "Ford",
                Model = "GT",
                Year = 2020,
                Color = "Blue",
                Mileage = 1000,
                ImageUrl = "https://example.com/car.jpg"
            }
        };

        context.Auctions.Add(auction);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return auction;
    }
}
