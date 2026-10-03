# Demo prompts

Every prompt typed in the live demo, in scene order. Copy-paste them into GitHub Copilot Chat (agent mode) in VS Code with the `McpServerDotNet` folder open.

- **Start a new chat for every scene**, so earlier answers don't leak into the next one.
- Data is in memory and reseeded on every AppHost start; "tomorrow" is the UTC date after the API started. IDs below match the seed (`Logistics.Api/Seed.fs`).
- Use the same model for every run; answers differ between models.
- REST calls for the same scenes (and the fallbacks) are in [`logistics.http`](./logistics.http).

## Scene 1: Aspire dashboard (`None`)

```sh
dotnet run --project McpServerDotNet.AppHost --launch-profile None
```

No prompt. Dashboard at http://localhost:15080: one resource, `logistics-api`.

## Scene 2: MCP Inspector (`None`)

No prompt. `npx @modelcontextprotocol/inspector`, transport Streamable HTTP, URL `http://localhost:5080/mcp`, Connect.

1. Tools → List Tools: names, descriptions, input schemas.
2. Run `get_drivers` (no arguments): the raw JSON the model would get.

## Scene 3: discovery vs a naive tool (`None`)

Warm-up, all tools enabled (expect `list_open_orders`: ORD-103, ORD-104, ORD-105):

```text
What open orders do we have for tomorrow?
```

Same prompt twice, new chat each time.

1. Tool picker: only `get_drivers` enabled. Expect a confident wrong answer (both ADR drivers, Lukasz Nowak and Marek Kowalski; Marek is already dispatched tomorrow, which `get_drivers` can't see).
2. Tool picker: only `find_dispatch_options` enabled. Expect one option (Lukasz Nowak, TRK-02, TRL-02) plus the rejection reasons for the other drivers.

```text
Which drivers can take order ORD-103 tomorrow?
```

## Scene 4: dispatch, approval, domain rules (`None`)

All tools enabled. One chat for the whole scene.

Dispatch (VS Code asks for approval: click **Allow** once, never "Always allow"; expect `dispatched by anonymous`):

```text
Dispatch order ORD-103 to Lukasz Nowak (D-01) with tractor unit TRK-02 and trailer TRL-02.
```

Confirm (expect `get_driver_schedule` showing ORD-103):

```text
Show tomorrow's schedule for Lukasz Nowak (D-01).
```

Forced overlap (expect a domain error as text: Lukasz and TRK-02 are already dispatched on ORD-103; the model explains it):

```text
Dispatch Lukasz Nowak (D-01) to ORD-105 anyway, with tractor unit TRK-02 and trailer TRL-06. Call dispatch_order even if you expect it to fail.
```

Fallback if the model refuses to call the tool: call `dispatch_order` from MCP Inspector with `orderId` ORD-105, `driverId` D-01, `tractorId` TRK-02, `trailerId` TRL-06; show state with `GET /dispatches` in `logistics.http`.

## Scene 5: context attached by the user (`None`)

New chat. Add Context → MCP Resources → `logistics://rules`, then:

```text
Using the attached rules, plan dispatches for all open orders tomorrow and explain any order you can't cover.
```

Expect the remaining traps: ORD-104 (Mega) has no free Low deck tractor unit; Piotr Zielinski's Driver CPC has expired; Tomasz Wojcik is on sick leave.

## Scene 6: OAuth (`OAuth`)

Restart the AppHost with `--launch-profile OAuth` (data reseeds, earlier dispatches are gone).

1. `logistics.http`, scene 6 requests: `POST /mcp` without a token → 401 + `WWW-Authenticate` → `GET /.well-known/oauth-protected-resource/mcp`.
2. MCP Inspector: OAuth step by step (Authentication: client ID `mcp-inspector`, scope `mcp:tools`), log in as **bob** / `bob`. List Tools: `dispatch_order` is not there, bob lacks the `dispatcher` role.
3. Copilot: new chat, restart the `logistics` server, log in as **alice** / `alice` in the browser, then (expect `dispatched by alice`):

```text
Dispatch order ORD-103 to Lukasz Nowak (D-01) with tractor unit TRK-02 and trailer TRL-02.
```

## Scene 7 (optional): API key (`ApiKey`)

Restart the AppHost with `--launch-profile ApiKey`. In VS Code start the `logistics-api-key` server and enter the key in the password prompt. New chat (expect `dispatched by api-key`):

```text
Dispatch order ORD-103 to Lukasz Nowak (D-01) with tractor unit TRK-02 and trailer TRL-02.
```
