using Ecom.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Product
{
    public class RatingDto : BaseDto
    {
        public string Content { get; set; } = string.Empty;
        public double RatingNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string RatingName { get; set; } = string.Empty;
    }

    public class RatingCreateDto
    {
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Range(1.0, 5.0)]
        public double RatingNumber { get; set; }

        [Required]
        public int ProductId { get; set; }
    }

    public class RatingUpdateDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Range(1.0, 5.0)]
        public double RatingNumber { get; set; }
    }
}
