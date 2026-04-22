namespace MedInsights.Lib
{
    public sealed class ForbiddenAccessException : Exception
    {
        public ForbiddenAccessException(string message) : base(message)
        {
        }
    }
}
