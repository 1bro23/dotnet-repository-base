namespace Mongo.Repository.Domain.Exceptions;

public class HttpStatusException : Exception
{
    public int StatusCode { get; set; }
    public HttpStatusException(int statusCode, string message, Exception? ex = null) : base(message, ex)
    {
        StatusCode = statusCode;
    }
}
