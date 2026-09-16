using System.Security.Claims;
using Intellifer.Chatbot.Infrastructure.Security;

namespace Intellifer.Chatbot.Web.Common;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtClaimNames.Subject);
}
