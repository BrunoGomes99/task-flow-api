using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.Extensions;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.UseCases.User.GetCurrentUserProfile;
using TaskFlow.Application.UseCases.User.LoginUser;
using TaskFlow.Application.UseCases.User.RegisterUser;

namespace TaskFlow.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterUserResponse>> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RegisterUserCommand(request.Name, request.Email, request.Password),
            cancellationToken);

        return CreatedAtAction(nameof(GetProfile), routeValues: null, value: new RegisterUserResponse(result.Id));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await mediator.Send(
            new LoginUserCommand(request.Email, request.Password),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var profile = await mediator.Send(new GetCurrentUserProfileQuery(userId), cancellationToken);

        if (profile is null)
            return NotFound();

        return Ok(profile);
    }
}
