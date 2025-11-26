using Ecom.Application.DTOs.Common;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Carousel
{
    public class CarouselDto : BaseDto
    {
        public string Title { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Image { get; set; } = string.Empty;
        public string ProductUrl { get; set; } = string.Empty;
    }

    public class CarouselCreateDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Arabic description cannot exceed 1000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]
        public decimal Price { get; set; }

        [StringLength(500, ErrorMessage = "Product URL cannot exceed 500 characters")]
        public string ProductUrl { get; set; } = string.Empty;
    }

    public class CarouselUpdateDto
    {
        [Required(ErrorMessage = "ID is required")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Arabic description cannot exceed 1000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]
        public decimal Price { get; set; }
        
        public string Image { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Product URL cannot exceed 500 characters")]
        public string ProductUrl { get; set; } = string.Empty;
    }

    public class CarouselCreateWithFileDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Arabic description cannot exceed 1000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]
        public decimal Price { get; set; }
        
        [Required(ErrorMessage = "Image is required")]
        public IFormFile Image { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Product URL cannot exceed 500 characters")]
        public string ProductUrl { get; set; } = string.Empty;
    }

    public class CarouselUpdateWithFileDto
    {
        [Required(ErrorMessage = "ID is required")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Arabic title cannot exceed 200 characters")]
        public string TitleAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Arabic description cannot exceed 1000 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]
        public decimal Price { get; set; }
        
        public IFormFile? Image { get; set; }
        
        public string? ImageToDelete { get; set; }

        [StringLength(500, ErrorMessage = "Product URL cannot exceed 500 characters")]
        public string ProductUrl { get; set; } = string.Empty;
    }
}

