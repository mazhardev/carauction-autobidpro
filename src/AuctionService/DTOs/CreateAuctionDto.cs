using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuctionService.DTOs;

public class CreateAuctionDto : IValidatableObject
{
    [Required(AllowEmptyStrings = false)]
    public string Make { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false)]
    public string Model { get; set; } = string.Empty;

    [Required]
    [Range(1886, int.MaxValue, ErrorMessage = "Year must be 1886 or later")]
    public int? Year { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Color { get; set; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Mileage cannot be negative")]
    public int? Mileage { get; set; }

    [Required(AllowEmptyStrings = false)]
    [Url(ErrorMessage = "Image URL must be a valid URL")]
    public string ImageUrl { get; set; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Reserve price cannot be negative")]
    public int? ReservePrice { get; set; }

    [Required]
    public DateTime? AuctionEnd { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AuctionEnd.HasValue && AuctionEnd.Value.ToUniversalTime() <= DateTime.UtcNow)
        {
            yield return new ValidationResult(
                "Auction end must be in the future",
                [nameof(AuctionEnd)]);
        }

        var maxVehicleYear = DateTime.UtcNow.Year + 1;
        if (Year.HasValue && Year.Value > maxVehicleYear)
        {
            yield return new ValidationResult(
                $"Year cannot be later than {maxVehicleYear}",
                [nameof(Year)]);
        }
    }
}
