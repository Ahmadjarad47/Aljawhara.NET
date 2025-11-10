using Ecom.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers
{
    [ApiController]
    [Route("api/visitors")]
    public class VisitorsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public VisitorsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public class TrackVisitorDto
        {
            public string? Path { get; set; }
        }

        [HttpPost("track")]
        public async Task<IActionResult> Track([FromBody] TrackVisitorDto dto)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();

            var visitor = new Ecom.Domain.Entity.Visitor
            {
                IpAddress = ip,
                UserAgent = ua,
                Path = dto.Path ?? string.Empty,
                VisitedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.Visitors.AddAsync(visitor);
            await _unitOfWork.SaveChangesAsync();
            return Ok();
        }
    }
}


