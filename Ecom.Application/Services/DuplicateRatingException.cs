namespace Ecom.Application.Services;

public sealed class DuplicateRatingException() : Exception("A rating already exists for this reviewer and product.");
