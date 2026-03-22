namespace TaskFlow.Application.DTOs;

/// <summary>
/// Response payload for successful login: signed JWT (Bearer) and token lifetime hint for clients.
/// </summary>
/// <param name="AccessToken">Signed JWT.</param>
/// <param name="ExpiresIn">Access token lifetime in seconds (same value as JWT <c>exp</c> semantics for clients that do not decode the token).</param>
public sealed record LoginResponse(string AccessToken, int ExpiresIn);
