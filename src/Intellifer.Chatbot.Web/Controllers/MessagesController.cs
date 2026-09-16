using Intellifer.Chatbot.Application.Dtos;
using Intellifer.Chatbot.Application.Interfaces;
using Intellifer.Chatbot.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intellifer.Chatbot.Web.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController : ControllerBase
{
    private readonly IChatMessageService _chatMessageService;

    public MessagesController(IChatMessageService chatMessageService)
    {
        _chatMessageService = chatMessageService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetHistory(
        [FromQuery] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var history = await _chatMessageService.GetHistoryAsync(userId, sessionId, cancellationToken);

        return Ok(history);
    }
}
