namespace AdPulse.Api.Errors;

/// <summary>Signals invalid request data without exposing the source message to callers.</summary>
public sealed class ApiValidationException : Exception
{
    public ApiValidationException() : base("The request is invalid.")
    {
    }
}

/// <summary>Signals that the authenticated caller is not allowed to perform an operation.</summary>
public sealed class ApiAuthorizationException : Exception
{
    public ApiAuthorizationException() : base("The operation is not authorized.")
    {
    }
}

/// <summary>Signals that the requested resource does not exist in the caller's scope.</summary>
public sealed class ApiNotFoundException : Exception
{
    public ApiNotFoundException() : base("The requested resource was not found.")
    {
    }
}

/// <summary>Signals that a write lost an optimistic-concurrency race.</summary>
public sealed class ApiConcurrencyException : Exception
{
    public ApiConcurrencyException() : base("The resource was changed by another request.")
    {
    }
}
