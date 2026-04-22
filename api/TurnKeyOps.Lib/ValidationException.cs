
namespace MedInsights.Lib
{
    public class ValidationException : Exception
    {
        public List<ApiError> Errors { get; }

        public ValidationException(List<ApiError> errors)
        {
            Errors = errors;
        }
    }
}
