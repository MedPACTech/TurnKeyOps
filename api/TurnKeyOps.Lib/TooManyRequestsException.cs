namespace MedInsights.Lib
{
    public sealed class TooManyRequestsException : Exception
    {
        public TooManyRequestsException(string message) : base(message)
        {
        }
    }
}
