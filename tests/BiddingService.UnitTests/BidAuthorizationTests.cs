using System.Reflection;
using BiddingService.Controllers;
using BiddingService.Models;
using BiddingService.RequestHelpers;
using Microsoft.AspNetCore.Authorization;

namespace BiddingService.UnitTests;

public class BidAuthorizationTests
{
    [Fact]
    public void PlaceBid_RequiresAuthorization()
    {
        var method = typeof(BidsController).GetMethod(nameof(BidsController.PlaceBid))!;

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void GetBidsForAuction_StaysPublic()
    {
        var method = typeof(BidsController).GetMethod(nameof(BidsController.GetBidsForAuction))!;

        Assert.Null(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    /// <summary>
    /// The bidder identity must come from the access token, so the action must not
    /// accept a bidder/seller/username parameter that a client could supply.
    /// </summary>
    [Fact]
    public void PlaceBid_DoesNotAcceptAnIdentityFromTheRequest()
    {
        var parameters = typeof(BidsController).GetMethod(nameof(BidsController.PlaceBid))!
            .GetParameters()
            .Select(p => p.Name!)
            .ToArray();

        Assert.Equal(["auctionId", "amount"], parameters);
    }
}

public class BidRulesTests
{
    [Fact]
    public void IsOwnAuction_IsTrue_WhenBidderIsTheSeller()
    {
        Assert.True(BidRules.IsOwnAuction("bob", "bob"));
    }

    [Fact]
    public void IsOwnAuction_IsFalse_ForADifferentBidder()
    {
        Assert.False(BidRules.IsOwnAuction("bob", "alice"));
    }

    [Theory]
    [InlineData(null, "bob")]
    [InlineData("bob", null)]
    [InlineData("bob", "")]
    [InlineData("bob", "   ")]
    public void IsOwnAuction_IsFalse_WhenEitherIdentityIsMissing(string? seller, string? bidder)
    {
        Assert.False(BidRules.IsOwnAuction(seller, bidder));
    }

    [Fact]
    public void DetermineStatus_IsFinished_WhenTheAuctionHasEnded()
    {
        var status = BidRules.DetermineStatus(DateTime.UtcNow.AddMinutes(-1), 100, 5000, null);

        Assert.Equal(BidStatus.Finished, status);
    }

    [Fact]
    public void DetermineStatus_IsAccepted_ForAFirstBidOverTheReserve()
    {
        var status = BidRules.DetermineStatus(DateTime.UtcNow.AddDays(1), 1000, 1500, null);

        Assert.Equal(BidStatus.Accepted, status);
    }

    [Fact]
    public void DetermineStatus_IsAcceptedBelowReserve_ForAFirstBidUnderTheReserve()
    {
        var status = BidRules.DetermineStatus(DateTime.UtcNow.AddDays(1), 1000, 900, null);

        Assert.Equal(BidStatus.AcceptedBelowReserve, status);
    }

    [Fact]
    public void DetermineStatus_IsAccepted_WhenTheBidBeatsTheCurrentHighBid()
    {
        var status = BidRules.DetermineStatus(DateTime.UtcNow.AddDays(1), 1000, 2000, 1500);

        Assert.Equal(BidStatus.Accepted, status);
    }

    [Theory]
    [InlineData(1500)]
    [InlineData(1400)]
    public void DetermineStatus_IsTooLow_WhenTheBidDoesNotBeatTheCurrentHighBid(int amount)
    {
        var status = BidRules.DetermineStatus(DateTime.UtcNow.AddDays(1), 1000, amount, 1500);

        Assert.Equal(BidStatus.TooLow, status);
    }
}
