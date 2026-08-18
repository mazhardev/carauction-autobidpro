using System;
using AutoMapper;
using BiddingService.DTOs;
using BiddingService.Models;
using BiddingService.RequestHelpers;
using BiddingService.Services;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Entities;

namespace BiddingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BidsController(IMapper mapper, IPublishEndpoint publishEndpoint, 
    GrpcAuctionClient grpcClient) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<BidDto>> PlaceBid(string auctionId, int amount)
    {
        // The bidder identity always comes from the validated token, never from the request.
        var bidder = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(bidder)) return Unauthorized();

        var auction = await DB.Find<Auction>().OneAsync(auctionId);

        if (auction == null)
        {
            auction = grpcClient.GetAuction(auctionId);

            if (auction == null)
            {
                return BadRequest("Cannot accept bids for this auction");
            }
        }

        if (BidRules.IsOwnAuction(auction.Seller, bidder))
        {
            return BadRequest("You cannot bid on your own item");
        }

        int? highestBidAmount = null;

        if (auction.AuctionEnd >= DateTime.UtcNow)
        {
            var highBid = await DB.Find<Bid>()
                .Match(a => a.AuctionId == auctionId)
                .Sort(b => b.Descending(x => x.Amount))
                .ExecuteFirstAsync();

            highestBidAmount = highBid?.Amount;
        }

        var bid = new Bid()
        {
            Amount = amount,
            AuctionId = auctionId,
            Bidder = bidder,
            BidStatus = BidRules.DetermineStatus(auction.AuctionEnd, auction.ReservePrice, amount,
                highestBidAmount)
        };

        await DB.SaveAsync(bid);

        await publishEndpoint.Publish(mapper.Map<BidPlaced>(bid));

        return Ok(mapper.Map<BidDto>(bid));
    }

    [HttpGet("{auctionId}")]
    public async Task<ActionResult<List<BidDto>>> GetBidsForAuction(string auctionId)
    {
        var bids = await DB.Find<Bid>()
            .Match(a => a.AuctionId == auctionId)
            .Sort(b => b.Descending(a => a.BidTime))
            .ExecuteAsync();

        return bids.Select(mapper.Map<BidDto>).ToList();
    }
}
