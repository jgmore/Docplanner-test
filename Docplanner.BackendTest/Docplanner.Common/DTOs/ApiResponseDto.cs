namespace Docplanner.Common.DTOs;

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IEnumerable<string>? Errors { get; set; }

    public ApiResponseDto() { }

    // Static methods to facilitate the creation of common responses
    public static ApiResponseDto<T> CreateSuccess(string facilityId, T data, string message = "Operation completed successfully")
    {
        return new ApiResponseDto<T>
        {
            Success = true,
            Message = message,
            FacilityId = facilityId,
            Data = data
        };
    }

    public static ApiResponseDto<T> CreateError(string message, IEnumerable<string>? errors = null)
    {
        return new ApiResponseDto<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}
