using Microsoft.AspNetCore.Identity;

namespace Intellifer.Chatbot.Infrastructure.Persistence;

public class AppUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; }
}
