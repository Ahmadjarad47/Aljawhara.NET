using AutoMapper;
using Ecom.Application.DTOs.Carousel;
using Ecom.Application.Services.Interfaces;
using Ecom.Domain.Interfaces;
using Ecom.Domain.Entity;
using Ecom.Infrastructure.UnitOfWork;

namespace Ecom.Application.Services
{
    public class CarouselService : ICarouselService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IFileService _fileService;

        public CarouselService(IUnitOfWork unitOfWork, IMapper mapper, IFileService fileService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _fileService = fileService;
        }

        public async Task<CarouselDto?> GetCarouselByIdAsync(int id)
        {
            var carousel = await _unitOfWork.Carousels.GetActiveByIdAsync(id);
            return carousel != null ? _mapper.Map<CarouselDto>(carousel) : null;
        }

        public async Task<IEnumerable<CarouselDto>> GetAllCarouselsAsync()
        {
            var carousels = await _unitOfWork.Carousels.GetAllAsync();
            return _mapper.Map<IEnumerable<CarouselDto>>(carousels);
        }

        public async Task<IEnumerable<CarouselDto>> GetActiveCarouselsAsync()
        {
            var carousels = await _unitOfWork.Carousels.GetAllActiveAsync();
            return _mapper.Map<IEnumerable<CarouselDto>>(carousels);
        }

        public async Task<CarouselDto> CreateCarouselAsync(CarouselCreateDto carouselDto)
        {
            var carousel = _mapper.Map<Carousel>(carouselDto);
            
            await _unitOfWork.Carousels.AddAsync(carousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(carousel);
        }

        public async Task<CarouselDto> CreateCarouselWithFileAsync(CarouselCreateWithFileDto carouselDto)
        {
            var carousel = _mapper.Map<Carousel>(carouselDto);

            // Handle file upload if image is provided
            if (carouselDto.Image != null)
            {
                try
                {
                    var directory = $"carousels/{DateTime.UtcNow:yyyy/MM/dd}";
                    var imageUrl = await _fileService.SaveFileAsync(carouselDto.Image, directory);
                    carousel.Image = imageUrl;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading image: {ex.Message}", ex);
                }
            }

            await _unitOfWork.Carousels.AddAsync(carousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(carousel);
        }

        public async Task<CarouselDto> UpdateCarouselAsync(CarouselUpdateDto carouselDto)
        {
            var existingCarousel = await _unitOfWork.Carousels.GetByIdAsync(carouselDto.Id);
            if (existingCarousel == null)
                throw new ArgumentException($"Carousel with ID {carouselDto.Id} not found.");

            _mapper.Map(carouselDto, existingCarousel);
            _unitOfWork.Carousels.Update(existingCarousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(existingCarousel);
        }

        public async Task<CarouselDto> UpdateCarouselWithFileAsync(CarouselUpdateWithFileDto carouselDto)
        {
            var existingCarousel = await _unitOfWork.Carousels.GetByIdAsync(carouselDto.Id);
            if (existingCarousel == null)
                throw new ArgumentException($"Carousel with ID {carouselDto.Id} not found.");

            // Store old image URL for deletion
            var oldImageUrl = existingCarousel.Image;

            // Map basic properties
            existingCarousel.Title = carouselDto.Title;
            existingCarousel.Description = carouselDto.Description;
            existingCarousel.Price = carouselDto.Price;

            // Handle image deletion if specified
            if (!string.IsNullOrEmpty(carouselDto.ImageToDelete) && carouselDto.ImageToDelete == oldImageUrl)
            {
                try
                {
                    await _fileService.DeleteFileAsync(carouselDto.ImageToDelete);
                    existingCarousel.Image = string.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting image {carouselDto.ImageToDelete}: {ex.Message}");
                }
            }

            // Handle new file upload if image is provided
            if (carouselDto.Image != null)
            {
                try
                {
                    // Delete old image if it exists
                    if (!string.IsNullOrEmpty(oldImageUrl) && oldImageUrl != carouselDto.ImageToDelete)
                    {
                        try
                        {
                            await _fileService.DeleteFileAsync(oldImageUrl);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deleting old image {oldImageUrl}: {ex.Message}");
                        }
                    }

                    var directory = $"carousels/{DateTime.UtcNow:yyyy/MM/dd}";
                    var imageUrl = await _fileService.SaveFileAsync(carouselDto.Image, directory);
                    existingCarousel.Image = imageUrl;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error uploading image: {ex.Message}", ex);
                }
            }

            _unitOfWork.Carousels.Update(existingCarousel);
            await _unitOfWork.SaveChangesAsync();
            
            return _mapper.Map<CarouselDto>(existingCarousel);
        }

        public async Task<bool> DeleteCarouselAsync(int id)
        {
            var carousel = await _unitOfWork.Carousels.GetByIdAsync(id);
            if (carousel == null)
                return false;

            // Delete the image file if it exists
            if (!string.IsNullOrEmpty(carousel.Image))
            {
                try
                {
                    await _fileService.DeleteFileAsync(carousel.Image);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting image {carousel.Image}: {ex.Message}");
                }
            }

            _unitOfWork.Carousels.SoftDelete(carousel);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateCarouselAsync(int carouselId)
        {
            var carousel = await _unitOfWork.Carousels.GetByIdAsync(carouselId);
            if (carousel == null)
                return false;

            _unitOfWork.Carousels.Activate(carousel);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateCarouselAsync(int carouselId)
        {
            var carousel = await _unitOfWork.Carousels.GetByIdAsync(carouselId);
            if (carousel == null)
                return false;

            _unitOfWork.Carousels.Deactivate(carousel);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}

