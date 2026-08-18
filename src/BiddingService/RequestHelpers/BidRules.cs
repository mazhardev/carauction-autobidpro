using BiddingService.Models;

namespace BiddingService.RequestHelpers;

/// <summary>
/// Bidding rules that depend only on the values already loaded from storage.
/// Kept free of infrastructure so the marketplace rules can be unit tested.
/// </summary>
public static class BidRules
{
    /// <summary>
    /// True when the authenticated bidder is the seller of the auction.
    /// The bidder must always come from the validated access token, never from the request.
    /// </summary>
    public static bool IsOwnAuction(string? seller, string? bidder)
    {
        if (string.IsNullOrWhiteSpace(seller) || string.IsNullOrWhiteSpace(bidder)) return false;

        return string.Equals(seller, bidder, StringComparison.Ordinal);
    }

    /// <summary>
    /// Decides the status of an otherwise acceptable bid.
    /// </summary>
    public static BidStatus DetermineStatus(DateTime auctionEnd, int reservePrice, int amount,
        int? highestBidAmount)
    {
        if (auctionEnd < DateTime.UtcNow) return BidStatus.Finished;

        var status = BidStatus.Accepted;

        if (highestBidAmount is null || amount > highestBidAmount)
        {
            status = amount > reservePrice
                ? BidStatus.Accepted
                : BidStatus.AcceptedBelowReserve;
        }

        if (highestBidAmount is not null && amount <= highestBidAmount)
        {
            status = BidStatus.TooLow;
        }

        return status;
    }
}
