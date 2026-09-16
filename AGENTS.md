# Intellifer Chatbot — ASP.NET Core + SignalR + Angular

.NET 10 solution, Clean Architecture. Take-home interview assignment for a
Full Stack Developer role — needs to be textbook-correct because I have to
explain every decision in the interview, not just demo it.

This is a **chatbot**, not a chat room: each visitor has a private,
1-1 conversation with the bot. Nobody sees anyone else's messages.

## Role

You are a senior ASP.NET Core / .NET developer. Always apply ASP.NET Core-first
patterns and Clean Architecture decisions — SOLID, dependency injection,
async/await — not generic script-style or Node/Express-style shortcuts.

## Code standards

- Never instantiate services directly (no `new ChatDbContext()`, no
  `new SomeService()`) — always use constructor injection, registered in
  `Program.cs`
- Every infrastructure integration gets its own interface + implementation:
  `Application/Interfaces/IChatRepository.cs` + `Infrastructure/Persistence/ChatRepository.cs`
- Register infrastructure services once in `Program.cs` with the correct
  DI lifetime (`Scoped` for `ChatDbContext`, etc.) — don't re-resolve manually
  elsewhere
- Respect the layer boundaries, nothing crosses the wrong direction:
  `Domain` (entities only, zero dependencies) → `Application` (interfaces,
  DTOs, use-case services, no EF Core/ASP.NET Core references) →
  `Infrastructure` (EF Core, repo implementations) → `Web` (controllers,
  `ChatHub`, composition root — the only layer allowed to see all the others)
- Shared cross-cutting code (filters, middleware, hub base classes) goes in
  `Web/Common/`
- Use the `dotnet` CLI to scaffold: `dotnet new webapi`,
  `dotnet ef migrations add <Name>`, `dotnet ef database update`
- DTOs at every API/hub boundary — never expose an EF entity directly
- Async all the way through — no `.Result` / `.Wait()`

## Tech stack

- .NET 10 (current LTS) — ASP.NET Core Web API + SignalR
- EF Core 10, Code-First migrations, SQL Server (LocalDB for dev, container
  for docker-compose)
- Angular (latest CLI, standalone components) + `@microsoft/signalr`
- Docker + docker-compose (api, sqlserver, client)
- xUnit for backend tests

## Session model

- No rooms, no `Conversations` table, no SignalR Groups — those are
  multi-user-room mechanisms and this app has no shared rooms
- Each visitor gets a `SessionId` (a GUID) generated once by the Angular
  client and kept in `localStorage`. It's sent with every `SendMessage`
  call and with the history fetch — it's what survives a page refresh
  (SignalR's own `ConnectionId` does not; it resets on reconnect)
- `ChatMessage` carries `SessionId`; every query and broadcast is scoped
  to it. Two browser tabs with different session ids never see each
  other's messages — that's correct behavior, not a bug to fix later

## SignalR hub

- `ChatHub` stays thin — persistence and bot-reply logic live in
  `Application` services, never inline in the hub
- Flow on `SendMessage(sessionId, content)`: hub → `IChatMessageService
  .SaveAndRespondAsync` → persist user message → `IBotResponder
  .GetReplyAsync` → persist reply → hand both DTOs to `IChatBroadcaster
  .BroadcastAsync`
- `IChatBroadcaster` is defined in `Application`, implemented in `Web`
  over `IHubContext<ChatHub>`. This is a deliberate SOLID/DIP call:
  Application decides *that* a reply happened, the Web-layer
  implementation decides *how* the caller finds out
- The broadcaster sends to **`Clients.Caller`**, not `Clients.All` — a
  reply belongs only to the session that sent the message
- `IBotResponder` is rule-based for the MVP, deliberately behind an
  interface — swapping in a real LLM later is a design talking point, not
  a rewrite
- `GET /api/messages?sessionId=...` hydrates that session's history on
  load; SignalR only carries live updates, not the initial load

## Frontend conventions

- `ChatService` owns the session id: read it from `localStorage` on
  init, generate and store a new GUID if none exists
- `ChatService` wraps the `HubConnectionBuilder` connection, sends
  `sessionId` with every `sendMessage()` call, and exposes an Observable
  of incoming messages
- `ChatComponent` only renders and calls `ChatService` — no transport
  logic, no session-id handling in the component itself

## Definition of done

1. `docker compose up` brings up the API + SQL Server
2. A visitor can send a message and get a bot reply in real time
3. Messages persist in SQL Server, scoped by session, and survive a page
   refresh (same browser, same `localStorage` session id)
4. Two different browser sessions never see each other's messages
5. README explains the four layers, the session model, and how to run
   the project locally

## Out of scope

- No NoSQL/Mongo — SQL Server via EF Core only, per the JD
- No multi-room / multi-user chat, no SignalR Groups
- No auth system unless the MVP is solid with time left over
- No real LLM call by default — mention it as an extension point, don't
  build it

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
