using Intellifer.Chatbot.Application.Common;
using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellifer.Chatbot.Web.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserManagementService _userManagement;

    public AdminUsersController(IUserManagementService userManagement)
    {
        _userManagement = userManagement;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userManagement.GetAllAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var user = await _userManagement.GetByIdAsync(id, cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _userManagement.CreateAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(ApiError.From(result.Errors));
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> Update(
        string id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var requestingUserId = User.GetUserId();

        if (requestingUserId is null)
        {
            return Unauthorized();
        }

        var result = await _userManagement.UpdateAsync(id, requestingUserId, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Value);
        }

        return result.Errors.Contains("User not found.")
            ? NotFound(ApiError.From(result.Errors))
            : BadRequest(ApiError.From(result.Errors));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var requestingUserId = User.GetUserId();

        if (requestingUserId is null)
        {
            return Unauthorized();
        }

        var result = await _userManagement.DeleteAsync(id, requestingUserId, cancellationToken);

        if (result.Succeeded)
        {
            return NoContent();
        }

        return result.Errors.Contains("User not found.")
            ? NotFound(ApiError.From(result.Errors))
            : BadRequest(ApiError.From(result.Errors));
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        string id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userManagement.ResetPasswordAsync(id, request.NewPassword, cancellationToken);

        if (result.Succeeded)
        {
            return NoContent();
        }

        return result.Errors.Contains("User not found.")
            ? NotFound(ApiError.From(result.Errors))
            : BadRequest(ApiError.From(result.Errors));
    }
}
