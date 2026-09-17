# Intellifer Chatbot

A private 1-1 chatbot: an ASP.NET Core 10 API with a SignalR hub, SQL Server via
EF Core, and an Angular 22 client. Clean Architecture across four projects, JWT
authentication, role-based access control, and an admin panel for managing accounts.

This is deliberately a **chatbot**, not a chat room. Each signed-in user talks to the
bot in a conversation nobody else can read, and that guarantee is enforced by the
server rather than trusted to the client.

---

## What it does

- Register or sign in and get a JWT.
- Hold a private conversation with a bot over SignalR, with replies arriving in real
  time. The bot is rule-based by default and uses a language model when one is
  configured; both sit behind the same interface.
- Messages are persisted in SQL Server and scoped to both the signed-in user and a
  client-generated session id, so a page refresh restores the conversation.
- Administrators get an account-management panel: list, create, edit, delete users,
  assign roles, and reset passwords.

---

## Architecture

```
        Domain            entities only, references nothing
          ^
          |
     Application          interfaces, DTOs, use-case services (no EF Core, no ASP.NET Core)
          ^
          |
   Infrastructure         EF Core, ASP.NET Core Identity, repository and service implementations
          ^
          |
         Web              controllers, SignalR hub, composition root
```

The boundaries are enforced by the project graph, not by convention: a layer cannot
reference something above it without the compiler rejecting it. `Domain` has **zero**
project and package references, and `Application` has zero package references too.

| Project | Contains | References |
|---|---|---|
| `Intellifer.Chatbot.Domain` | `ChatMessage`, `MessageSender` | nothing |
| `Intellifer.Chatbot.Application` | Interfaces, DTOs, `ChatMessageService`, `AuthService`, `RuleBasedBotResponder`, `AccountPolicy` | Domain |
| `Intellifer.Chatbot.Infrastructure` | `AppUser`, `ChatDbContext`, `ChatRepository`, `UserManagementService`, `TokenService`, `DbInitializer`, migrations | Domain, Application |
| `Intellifer.Chatbot.Web` | `ChatHub`, `ChatBroadcaster`, controllers, `Program.cs` | all of the above |

Two placement decisions are worth calling out, because they are where Clean
Architecture usually gets fudged:

- **`AppUser` and the Identity managers live in Infrastructure, not Domain.**
  `IdentityUser` is an ASP.NET Core Identity type; putting it in `Domain` would drag a
  framework dependency into the layer that is meant to be pure. `Domain` keeps only
  `ChatMessage`. `Application` never sees `AppUser` at all — it works with a plain
  `string userId`, and `UserManagementService` wraps `UserManager` / `RoleManager`.
- **`IChatBroadcaster` is declared in Application and implemented in Web.** Application
  decides *that* a reply happened; the Web implementation decides *how* the caller finds
  out. The direction of the dependency is inverted so Application does not need to know
  SignalR exists.

### Repository layout

```
client/                                Angular 22 app, Material, unit + component + e2e tests
server/
  Intellifer.Chatbot.slnx
  src/
    Intellifer.Chatbot.Domain/           Entities, Enums
    Intellifer.Chatbot.Application/      Common, Dtos, Interfaces, Policies, Services
    Intellifer.Chatbot.Infrastructure/   Persistence (+ Migrations), Security, Ai, Bot
    Intellifer.Chatbot.Web/              Controllers, Hubs, Common, Program.cs, Dockerfile
  tests/
    Intellifer.Chatbot.UnitTests/        84 tests, no Docker required
    Intellifer.Chatbot.IntegrationTests/ 26 tests, real SQL Server via Testcontainers
docker-compose.yml
README.md
```

---

## The session model

A `SessionId` is a GUID the **browser** generates and keeps in `localStorage`. It is
sent with every message and with the history request. It is not a SignalR
`ConnectionId` (which resets on every reconnect) and it is not a server-issued token.

`ChatMessage` carries `SessionId` **and** `UserId`, and every query filters on both:

```csharp
.Where(message => message.SessionId == sessionId && message.UserId == userId)
```

So a session id is not a secret and does not need to be. Knowing someone's `SessionId`
gets you nothing, because the rows are also keyed to the account that owns them. Before
authentication existed, `GET /api/messages?sessionId=<guid>` would have returned any
conversation to anyone holding the GUID; now the ownership check is a `WHERE` clause.

**What the session id actually buys you:** a refresh keeps the same conversation, and
"New conversation" mints a new GUID so one account can have several separate threads.
There is no `Conversations` table — a session is simply a partition key.

**One honest caveat.** `localStorage` is shared per-origin across tabs, so **two tabs in
the same browser share the same session id and therefore the same conversation.** That
is expected behaviour, not a bug. Isolation between visitors is what the requirements
care about, and that is enforced per account — demonstrated with a second browser or an
incognito window.

---

## Authentication and authorization

- **ASP.NET Core Identity** owns the user store, password hashing and validation.
- **JWT bearer tokens** authenticate both the REST API and the SignalR hub.
- Two roles exist: `Admin` and `User`. Registration always assigns `User`.
- Accounts are never confirmed by email — there is no mail sender in this build.

| Endpoint | Access |
|---|---|
| `POST /api/auth/register` | anonymous |
| `POST /api/auth/login` | anonymous |
| `GET /api/auth/me` | authenticated |
| `POST /api/auth/change-password` | authenticated (own account) |
| `GET /api/messages?sessionId=` | authenticated, own sessions only |
| `GET/POST/PUT/DELETE /api/admin/users` | `Admin` role |
| `POST /api/admin/users/{id}/reset-password` | `Admin` role |
| `/hubs/chat` | authenticated |

Three details here are deliberate and easy to get wrong:

**A failed sign-in returns 400, not 401.** The Angular interceptor treats 401 as "your
token is no longer valid, sign in again", which on the login screen would be a redirect
loop. Bad credentials are a validation failure, so they are a 400.

**Roles need the claim type mapped explicitly.** The token is written with the literal
`role` claim and inbound claim mapping is switched off, so `TokenValidationParameters`
has to be told `RoleClaimType = "role"`. Without it, `[Authorize(Roles = "Admin")]`
silently never matches and every admin request 403s — or worse, is not checked at all.

**Passwords are reset by an administrator.** With no email infrastructure, a
self-service reset link has nowhere to go. An admin sets a new password from the panel,
and a signed-in user can change their own. Identity's reset-token machinery is still used
underneath; the admin just receives the token instead of an inbox.

**Admins manage accounts, not content.** There is deliberately no way for an
administrator to read another user's conversations. That is a privacy boundary, and it
is the reason the admin surface only exposes account operations.

---

## The SignalR flow

The hub stays thin. Persistence, reply generation and delivery all live behind
interfaces:

```
Client  ──SendMessage(sessionId, content)──▶  ChatHub
                                                │
                                                ▼
                                  IChatMessageService.SaveAndRespondAsync
                                                │
                        ┌───────────────────────┼───────────────────────┐
                        ▼                       ▼                       ▼
              persist user message     IBotResponder.GetReplyAsync   persist bot reply
                        │                       │                       │
                        └───────────────────────┼───────────────────────┘
                                                ▼
                                   IChatBroadcaster.BroadcastAsync
                                                │
                                                ▼
                            Clients.Client(connectionId).ReceiveMessage ×2
```

**The server echoes both messages back to the caller**, and the client renders only what
the server sends. There is no optimistic insert: the user's own message is not shown
until it is persisted and handed back, which means every bubble on screen has a real
database id and a real timestamp. It costs a few milliseconds of perceived latency and
removes an entire class of reconciliation bugs.

**Replies go to the originating connection, not to everyone.** `BroadcastAsync` takes the
connection id from `Context.ConnectionId` and uses `Clients.Client(connectionId)`.
`Clients.Caller` is only resolvable inside a hub method instance, so a broadcaster that
sits behind an interface — outside the hub, where the hub calls into it — cannot rely on
it. Passing the connection id makes "only the sender receives the reply" explicit and
testable rather than dependent on ambient state. There is an integration test that opens
two connections as the same user and asserts the second receives nothing.

**The token reaches the hub through the query string.** A browser cannot set an
`Authorization` header on a WebSocket handshake, so the Angular client uses
`accessTokenFactory` and the server reads `access_token` from the query string in
`JwtBearerEvents.OnMessageReceived`. That hook is restricted to the `/hubs/chat` path so
the query string is never treated as a token source for the REST API.

**SignalR has its own serializer.** `AddJsonProtocol` is configured separately from
`AddControllers().AddJsonOptions`; configuring only the latter would make the hub emit
`sender: 0` while REST emitted `sender: "User"` for the same message.

---

## The bot: rule-based, a language model, or both

Both responders sit behind the same port, so nothing above them knows which one
answered:

```csharp
public interface IBotResponder
{
    Task<string> GetReplyAsync(BotConversation conversation, CancellationToken cancellationToken = default);
}
```

- **`RuleBasedBotResponder`** (Application) matches greetings, thanks, farewells and a
  few questions, and otherwise falls back. It is pure and stateless, so it is unit
  tested with no infrastructure at all.
- **`AiBotResponder`** (Infrastructure) sends the conversation to a language model. It
  maps prior bot turns to `assistant` and visitor turns to `user`, keeps only the most
  recent `HistoryMessages`, and merges consecutive same-role turns — a reply that
  failed to persist leaves two user turns in a row, and some providers reject a
  conversation that does not alternate.

`Bot:Mode` decides which one answers:

| Mode | Behaviour |
|---|---|
| `Rules` | Rules only. The AI is never called, which keeps runs deterministic. |
| `Ai` | AI only. A provider failure surfaces rather than being papered over. |
| `Auto` | **Default.** The AI when `Bot:Ai` has a `BaseUrl` and an `ApiKey`, otherwise the rules; and if a configured call fails, the rules answer instead. |

`Auto` is why this runs out of the box: with no key and no network it is simply a
rule-based bot, and a wrong key, a rate limit or an unreachable endpoint degrades into
a rule-based answer rather than an error in the chat window.

### Pointing it at a model

Because the adapter speaks the OpenAI chat completions format, the same settings cover
OpenAI, Azure OpenAI, OpenRouter, Groq, Together, vLLM, LM Studio and Ollama:

```jsonc
"Bot": {
  "Mode": "Auto",
  "Ai": {
    "Provider": "OpenAi",                     // or "Anthropic"
    "BaseUrl": "http://localhost:11434/v1",   // e.g. a local Ollama
    "ApiKey": "ollama",                       // required to be non-empty, unused locally
    "Model": "llama3.1"
  }
}
```

For Anthropic, set `Provider` to `Anthropic` and `BaseUrl` to `https://api.anthropic.com`.
That adapter is not a copy of the OpenAI one: the key travels in `x-api-key` rather than
a bearer token, a version header is required, and the system prompt is hoisted out of the
conversation into a top-level field.

Every transport, protocol and parsing failure is normalised into a single
`AiChatClientException`. That is what makes the fallback precise: it catches that type
and nothing else, so a genuine bug surfaces instead of hiding behind a canned reply.

---

## Running it

### Docker — one command

```bash
docker compose up --build
```

Then open <http://localhost:4200> and sign in with the seeded administrator:

| | |
|---|---|
| Email | `admin@intellifer.local` |
| Password | `Admin123!` |

This starts SQL Server, applies migrations, seeds the roles and the admin account, starts
the API, and serves the Angular app through nginx.

The nginx container proxies `/api` and `/hubs` to the API, so the browser only ever talks
to one origin — **which is why the API needs no CORS configuration at all.**

Override these before using this anywhere that matters:

| Variable | Default |
|---|---|
| `MSSQL_SA_PASSWORD` | `YourStrong!Passw0rd` |
| `JWT_SIGNING_KEY` | `compose-only-signing-key-change-me-before-real-use` |
| `SEED_ADMIN_EMAIL` | `admin@intellifer.local` |
| `SEED_ADMIN_PASSWORD` | `Admin123!` |

### Local development

1. **Database** — the compose SQL Server doubles as the development database:

   ```bash
   docker compose up -d sqlserver
   ```

2. **API** — migrates and seeds on start because `Database:ApplyMigrationsOnStartup` is
   `true` in `appsettings.Development.json`:

   ```bash
   dotnet run --project server/src/Intellifer.Chatbot.Web
   ```

   Listening on <http://localhost:5080>. In Development the OpenAPI document is served
   at `/openapi/v1.json`.

3. **Client** — `ng serve` proxies `/api` and `/hubs` to port 5080 using
   `proxy.conf.json` (with `ws: true`, which the hub needs):

   ```bash
   cd client
   npm install
   npm start
   ```

   Listening on <http://localhost:4200>.

### Configuration

| Setting | Purpose |
|---|---|
| `ConnectionStrings:Default` | SQL Server connection string |
| `Database:ApplyMigrationsOnStartup` | Apply migrations and seed on boot |
| `Jwt:Issuer`, `Jwt:Audience` | Token validation |
| `Jwt:SigningKey` | HMAC-SHA256 key, **at least 32 bytes**, validated at startup |
| `Jwt:ExpiryMinutes` | Access token lifetime (default 60) |
| `SeedAdmin:Email`, `SeedAdmin:Password` | Account created on first run |
| `Bot:Mode` | `Rules`, `Ai` or `Auto` (default) |
| `Bot:SystemPrompt` | System prompt sent to the model |
| `Bot:HistoryMessages` | How many prior turns to send; `0` disables context |
| `Bot:Ai:Provider` | `OpenAi` (default) or `Anthropic` |
| `Bot:Ai:BaseUrl`, `Bot:Ai:ApiKey`, `Bot:Ai:Model` | Endpoint, credential and model |
| `Bot:Ai:MaxTokens`, `Bot:Ai:TimeoutSeconds` | Reply cap and per-request timeout |

---

## Tests

```bash
# 84 tests, no Docker needed
dotnet test server/tests/Intellifer.Chatbot.UnitTests

# 26 tests, starts a throwaway SQL Server container
dotnet test server/tests/Intellifer.Chatbot.IntegrationTests

# 55 tests, no browser needed
cd client && npm test

# 17 tests in a real browser - run 'docker compose up -d sqlserver' first
cd client && npm run e2e
```

**Backend unit tests** cover `ChatMessageService` (ordering, session and user scoping,
trimming, validation, the history it hands the responder, and the case where the bot
throws mid-conversation), the bot's rules including that "hilarious" is not a greeting,
`AccountPolicy` — a pure function precisely so the self-delete and last-admin rules can
be tested without any Identity machinery — and both AI adapters against a stub handler,
which is what pins the wire format without touching a network.

**Backend integration tests** run against a real SQL Server and cover what fakes cannot:
that the schema round-trips, that ordering is by identity column, that deleting an
account cascades to its messages, that a message longer than `nvarchar(2000)` is rejected
by the database, that a non-admin receives 403 while an admin receives 200, that another
user reading a session id gets an empty list, which responder the composition root picks
for each mode, and that a full hub round-trip delivers exactly two messages to the sender
and nothing to a second connection.

**Client tests** are the services, guards and interceptor — session handling, token
handling, the 401 path — plus the components driven through their real templates against
mocked HTTP and a stubbed hub, so the assertions are about what actually renders.

**End-to-end tests** are the only ones that cross the whole system: a real browser, the
real API over a real WebSocket, a real database. They are what proves a reply arrives, a
refresh keeps the thread, and a second account in the same browser sees none of the first
account's messages.

The split is deliberate: the fast suites need neither Docker nor a browser, so they stay
in the tight feedback loop.

---

## Decisions and trade-offs

**Four projects, not folders.** The boundaries are the point of the exercise, so they are
compile-time errors rather than code-review conventions.

**A `UserId` column rather than trusting the session id.** This is the change that turned
"nobody sees anyone else's messages" from a client-side promise into a server-side
`WHERE` clause.

**Migrations applied on startup, behind a flag.** `docker compose up` has to be one
command, so the API migrates itself when `Database:ApplyMigrationsOnStartup` is set. That
is a demo convenience and is documented as such; a real deployment applies migrations as
its own step.

**`datetime2` values are re-stamped as UTC on read.** SQL Server's `datetime2` carries no
offset, so EF hands the value back with `DateTimeKind.Unspecified`, which serialises
without the trailing `Z` and would make every browser render every timestamp as local
time. A value converter in the entity configuration fixes it at the source.

**The identity column orders the conversation.** Both messages are saved sequentially so
the reply always has a higher `Id` than the message that prompted it, which means
identical timestamps cannot reorder them. The trade-off is that they are not written in a
single transaction — if the bot throws, the user's message is already durable, which is
arguably the behaviour you want, but the client shows an error rather than the message it
did save.

**A brand-new session returns `200 []`, not `404`.** A 404 for an unowned session would
also fire for a legitimately empty new conversation, and returning an empty list leaks
nothing.

**The admin user list avoids an N+1 query.** Roles are loaded in one batched join rather
than calling `GetRolesAsync` per user.

**No refresh tokens.** Short-lived access tokens, and the interceptor signs you out on
401. Refresh token rotation is the obvious next addition, not something to fake.

---

## Known limitations

- No refresh token, so a session simply ends when the token expires.
- No rate limiting on the hub or the login endpoint.
- The seeded administrator's password is a committed development default.
- The AI responder is dormant until `Bot:Ai` is configured, and once it is there is no
  token or cost control and no rate limiting on it.
- `localStorage` means two tabs of the same browser share one conversation.
- The client test suite deliberately covers only `ChatService`'s session handling; the
  rest of the client behaviour is exercised by building and running it.

### Troubleshooting

If `npm start` reports a missing `ng`, your shell may have `NODE_ENV=production` or
`npm_config_omit=dev` set, which makes every install skip dev dependencies. Install them
explicitly:

```bash
npm install --include=dev
```
