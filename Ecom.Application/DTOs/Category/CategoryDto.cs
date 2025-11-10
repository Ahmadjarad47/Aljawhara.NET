using Ecom.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Ecom.Application.DTOs.Category
{
    public class CategoryDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public List<SubCategoryDto> SubCategories { get; set; } = new();
        public int ProductCount { get; set; }
    }

    public class CategoryCreateDto
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic name cannot exceed 100 characters")]
        public string NameAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "Arabic description cannot exceed 500 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
    }

    public class CategoryUpdateDto
    {
        [Required(ErrorMessage = "ID is required")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic name cannot exceed 100 characters")]
        public string NameAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "Arabic description cannot exceed 500 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
    }

    public class SubCategoryDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionAr { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryNameAr { get; set; } = string.Empty;
        public int ProductCount { get; set; }
    }

    public class SubCategoryCreateDto
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic name cannot exceed 100 characters")]
        public string NameAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "Arabic description cannot exceed 500 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Category ID is required")]
        public int CategoryId { get; set; }
    }

    public class SubCategoryUpdateDto
    {
        [Required(ErrorMessage = "ID is required")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(100, ErrorMessage = "Arabic name cannot exceed 100 characters")]
        public string NameAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "Arabic description cannot exceed 500 characters")]
        public string DescriptionAr { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Category ID is required")]
        public int CategoryId { get; set; }
    }
}





