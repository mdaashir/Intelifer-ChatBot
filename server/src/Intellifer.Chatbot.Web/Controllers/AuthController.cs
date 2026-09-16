using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellifer.Chatbot.Web.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserManagementService _userManagement;

    public AuthController(IAuthService authService, IUserManagementService userManagement)
    {
        _authService = authService;
        _userManagement = userManagement;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(ApiError.From(result.Errors));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

        // A failed sign-in is a 400, not a 401: the Angular interceptor treats 401
        // as "your token expired, go and sign in again", which would loop here.
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(ApiError.From(result.Errors));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _userManagement.GetByIdAsync(userId, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _userManagement.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return result.Succeeded ? NoContent() : BadRequest(ApiError.From(result.Errors));
    }
}
