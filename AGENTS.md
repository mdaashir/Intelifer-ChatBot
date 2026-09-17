# Intellifer Chatbot — ASP.NET Core + SignalR + Angular

.NET 10 solution, Clean Architecture. Take-home interview assignment for a
Full Stack Developer role — needs to be textbook-correct because I have to
explain every decision in the interview, not just demo it.

This is a **chatbot**, not a chat room: each signed-in user has a private,
1-1 conversation with the bot. Nobody sees anyone else's messages, and the
server enforces that rather than trusting the client.

## Role

You are a senior ASP.NET Core / .NET developer. Always apply ASP.NET Core-first
patterns and Clean Architecture decisions — SOLID, dependency injection,
async/await — not generic script-style or Node/Express-style shortcuts.

## Code standards

- Never instantiate services directly (no `new ChatDbContext()`, no
  `new SomeService()`) — always use constructor injection
- Every infrastructure integration gets its own interface + implementation:
  `Application/Interfaces/IChatRepository.cs` + `Infrastructure/Persistence/ChatRepository.cs`
- Register every service once in `Program.cs` with the correct DI lifetime
  (`Scoped` for `ChatDbContext`, `Singleton` for the stateless ones) — don't
  re-resolve manually elsewhere
- Respect the layer boundaries, nothing crosses the wrong direction:
  `Domain` (entities only, zero dependencies) → `Application` (interfaces,
  DTOs, use-case services, no EF Core/ASP.NET Core references) →
  `Infrastructure` (EF Core, Identity, repo implementations) → `Web`
  (controllers, `ChatHub`, composition root — the only layer that sees all)
- The boundaries are compiler-enforced, so keep them that way: `Domain` has no
  project or package references, and `Application` has no package references
- Framework types stay in the layer that owns the framework. `AppUser` is an
  `IdentityUser`, so it lives in `Infrastructure`, never in `Domain`.
  `Application` passes a plain `string userId` around and never sees `AppUser`
- Shared cross-cutting code (filters, middleware, hub client contracts) goes in
  `Web/Common/` or `Web/Hubs/`
- Use the `dotnet` CLI to scaffold: `dotnet new webapi`,
  `dotnet ef migrations add <Name>`, `dotnet ef database update`
- DTOs at every API/hub boundary — never expose an EF entity directly
- Async all the way through — no `.Result` / `.Wait()`
- Comments only where the reason is not obvious from the code

## Tech stack

- .NET 10 — ASP.NET Core Web API + SignalR
- EF Core 10, Code-First migrations, SQL Server (container for both dev and
  docker-compose; LocalDB is not installed on this machine)
- ASP.NET Core Identity + JWT bearer for auth and roles
- Angular 22 (standalone components, **zoneless**, signals + RxJS) with
  Angular Material, `@microsoft/signalr`
- Docker + docker-compose (sqlserver, api, client behind nginx)
- xUnit for backend tests; Testcontainers for integration tests

## Authentication and RBAC

- Identity owns users, password hashing and validation. `RequireConfirmedAccount`
  and `RequireConfirmedEmail` are both off — there is no mail sender
- JWT bearer authenticates the API **and** the hub
- Roles are `Admin` and `User`; registration always assigns `User`
- Tokens carry the literal `role` claim with inbound claim mapping disabled, so
  `TokenValidationParameters.RoleClaimType` must be set to `role`. Without it
  `[Authorize(Roles = "Admin")]` silently never matches
- A failed sign-in returns **400, not 401** — the Angular interceptor treats 401
  as "token expired, sign in again", which would loop on the login screen
- Password reset is admin-driven, and a signed-in user can change their own.
  Identity's reset-token machinery is used, but an admin receives the token
  rather than an inbox
- `AccountPolicy` in `Application/Policies` holds the self-delete, self-demote
  and last-admin rules as pure functions so they are unit-testable
- Admins manage accounts, **not content**. There is deliberately no way for an
  admin to read another user's conversations

## Session model

- No rooms, no `Conversations` table, no SignalR Groups — those are
  multi-user-room mechanisms and this app has no shared rooms
- Each browser generates a `SessionId` (a GUID) once and keeps it in
  `localStorage`. It is what survives a page refresh (SignalR's own
  `ConnectionId` does not; it resets on reconnect)
- `ChatMessage` carries **both** `SessionId` and `UserId`, and every query
  filters on both. A session id is therefore not a secret: knowing one gets you
  nothing without the owning account
- `localStorage` is shared per-origin across tabs, so two tabs in the same
  browser share one conversation. That is expected — isolation is per account,
  so demo it with a second browser or an incognito window, and never claim that
  two tabs are isolated

## SignalR hub

- `ChatHub` stays thin — persistence and bot-reply logic live in `Application`
  services, never inline in the hub
- Flow on `SendMessage(sessionId, content)`: hub → `IChatMessageService
  .SaveAndRespondAsync` → persist user message → `IBotResponder.GetReplyAsync`
  → persist bot reply → hand both DTOs to `IChatBroadcaster.BroadcastAsync`
- `IChatBroadcaster` is defined in `Application`, implemented in `Web` over
  `IHubContext<ChatHub, IChatHubClient>`. This is a deliberate SOLID/DIP call:
  Application decides *that* a reply happened, the Web layer decides *how* the
  caller finds out
- The broadcaster addresses the caller by **connection id**
  (`Clients.Client(connectionId)`), not `Clients.All` and not `Clients.Caller`.
  `Caller` is only resolvable inside a hub method instance, so a broadcaster
  sitting behind an interface cannot rely on it; the hub passes
  `Context.ConnectionId` in. A reply belongs only to the connection that sent
  the message, and there is a test that proves it
- The server echoes **both** the persisted user message and the bot reply. The
  client never invents a message, so every bubble has a real id and timestamp
- The browser cannot set an `Authorization` header on a WebSocket handshake, so
  the token travels as the `access_token` query string parameter and is read in
  `JwtBearerEvents.OnMessageReceived`, restricted to the `/hubs/chat` path
- SignalR uses its **own** serializer: `AddJsonProtocol` must be configured
  separately from `AddControllers().AddJsonOptions`, or the hub emits `sender: 0`
  while REST emits `sender: "User"` for the same message
- `GET /api/messages?sessionId=...` hydrates that session's history on load;
  SignalR only carries live updates, not the initial load

## The bot

- `IBotResponder` takes a `BotConversation` — the new prompt plus the turns that came
  before it — not a bare string. `ChatMessageService` reads that history *before* it
  persists the new message, and bounds it with `BotConversation.MaxHistoryTurns`
- Two implementations coexist behind that port and are chosen by `Bot:Mode`:
  `RuleBasedBotResponder` in `Application` (pure, stateless, testable with no
  infrastructure) and `AiBotResponder` in `Infrastructure` over `IAiChatClient`
- `Auto` is the default and the reason the project runs with no key at all: the AI is
  used when `Bot:Ai:BaseUrl` and `:ApiKey` are set, otherwise the rules, and the rules
  answer if a configured AI call fails. `Rules` and `Ai` force one or the other
- `FallbackBotResponder` catches `AiChatClientException` and nothing else. Every
  provider failure is normalised into that one type by `AiChatClientBase` precisely so
  the catch can stay narrow — a genuine bug must surface rather than hide behind a
  canned reply
- Both adapters live in `Infrastructure/Ai`. OpenAI speaks `chat/completions`, which is
  the more useful of the two because that format also covers Azure OpenAI, OpenRouter,
  Groq, vLLM, LM Studio and Ollama. Anthropic speaks `v1/messages` with `x-api-key`, a
  version header and no system role, so the system prompt is hoisted to a top-level field
- Never call a provider from a hub or a controller — it goes through `IAiChatClient`
- The integration tests pin `Bot:Mode` to `Rules` so nothing reaches for a network

## Frontend conventions

- `ChatService` owns the session id: read it from `localStorage` on init,
  generate and store a new GUID if none exists, and mint a new one for
  "New conversation"
- `ChatService` wraps the `HubConnectionBuilder` connection, sends the
  `sessionId` with every `sendMessage()` call, and exposes an **Observable** of
  incoming messages (`messages$`)
- `ChatComponent` only renders and calls `ChatService` — no transport logic, no
  session-id handling in the component itself
- `AuthService` owns the token and the current user, exposes `user`,
  `isAuthenticated` and `isAdmin` as signals, and resolves the user once per
  session via `loadCurrentUser()` so guards never check roles against an empty user
- Components are zoneless: read signals directly, or consume Observables with
  `| async`. A plain `subscribe()` assigning to a field will not update the view
- Angular Material for all UI; custom CSS only for the chat bubbles

## Repository layout

Two halves, side by side at the root: the Angular application and the .NET backend.

- `client/` — Angular 22 app, plus its Dockerfile and the nginx config
- `server/Intellifer.Chatbot.slnx` — the solution, kept next to the backend it describes
- `server/src/` — Domain, Application, Infrastructure, Web
- `server/tests/` — UnitTests (no Docker needed), IntegrationTests (Testcontainers)
- `docker-compose.yml` — brings up SQL Server, the API and the client

## Tests

Four layers, each covering what the others cannot:

- `server/tests/Intellifer.Chatbot.UnitTests` — 84 tests, no Docker. Application logic,
  the bot rules, `AccountPolicy`, and both AI adapters against a stub `HttpMessageHandler`
- `server/tests/Intellifer.Chatbot.IntegrationTests` — 26 tests, real SQL Server via
  Testcontainers. The schema, cascade delete, 401/403, session isolation, the responder
  the composition root actually picks, and a hub round-trip proving the reply reaches
  only the sending connection
- `client` unit + component tests (55) — the services, guards, interceptor, and the
  components driven through their real templates against mocked HTTP and a stubbed hub
- `client/e2e` (17, Playwright) — the only layer that crosses browser → API → WebSocket →
  database

Run the client suites from `client/`: `npm test` and `npm run e2e`. The end-to-end suite
starts the API and the dev server itself but needs SQL Server up first
(`docker compose up -d sqlserver`).

Never let a test reach a real AI provider. The integration tests pin `Bot:Mode` to
`Rules`, and the adapter tests assert the request shape against a stub handler.

## Commands

```bash
docker compose up --build            # whole stack, then http://localhost:4200
docker compose up -d sqlserver       # just the dev database
dotnet run --project server/src/Intellifer.Chatbot.Web
cd client && npm start               # ng serve on :4200, proxies to :5080

dotnet test server/tests/Intellifer.Chatbot.UnitTests          # no Docker needed
dotnet test server/tests/Intellifer.Chatbot.IntegrationTests   # needs Docker
cd client && npm test

dotnet ef migrations add <Name> --project server/src/Intellifer.Chatbot.Infrastructure \
  --startup-project server/src/Intellifer.Chatbot.Web --output-dir Persistence/Migrations
```

## Definition of done

1. `docker compose up` brings up SQL Server, the API and the client
2. A user can register or sign in and get a bot reply in real time
3. Messages persist in SQL Server, scoped by user and session, and survive a
   page refresh (same browser, same `localStorage` session id)
4. Two different accounts never see each other's messages, enforced server-side
5. An admin can list, create, edit, delete users, assign roles and reset passwords
6. Non-admins get 403 on the admin endpoints, unauthenticated callers get 401
7. `dotnet test` passes for both test projects, and `npm test` passes for the client
8. README explains the four layers, the session model, the auth flow and how to
   run the project locally

## Out of scope

- No NoSQL/Mongo — SQL Server via EF Core only, per the JD
- No multi-room / multi-user chat, no SignalR Groups
- No email of any kind — no confirmation, no self-service reset links
- No refresh tokens or token revocation
- The AI path is optional and dormant until `Bot:Ai` is configured; there is no token,
  cost or rate control on it
- No admin access to other users' conversations; that boundary is deliberate

## Skills

Do not load any skill by default. Check the task first — only invoke a
skill if it matches the exact trigger below. Never invoke a skill just
because it exists.

- `/architect` — before building something non-trivial with no plan yet
- `/review` — when a feature is done and needs a production check
- `/recover` — when something is broken and the fix isn't obvious
- `/remember` — at the start of a new session to restore context,
  and at the end to save progress

## Session continuity

REQUIRED — do not skip, do not wait to be asked:

- **First action of every session:** run `/remember restore` before doing anything else.
- **Last action of every session:** run `/remember save` before closing.
