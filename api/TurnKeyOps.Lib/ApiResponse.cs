namespace MedInsights.Lib
{
   public class ApiResponse<T>
   {
       public bool Success { get; set; }
       public T? Data { get; set; }
       public List<ApiError>? Errors { get; set; }
       public string? TraceId { get; set; }
   }
}