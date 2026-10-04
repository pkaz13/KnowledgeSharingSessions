# Script: MCP servers in .NET

> Pełny przebieg Session dla prowadzącego, po polsku. Wszystko, co widzi publiczność (slajdy, prompty, dane, opisy narzędzi), jest po angielsku.
> Speaker notes w `slides.html` to krótkie podpowiedzi do slajdów; ten plik prowadzi przez całość, łącznie z demo na żywo.
> Prompty **nie są tu kopiowane**: każda scena linkuje do swojej sekcji w [`prompts.md`](../prompts.md), stamtąd się je wkleja (publiczność może je potem powtórzyć).

**Czas:** ~31 min planowo, limit 45 min, reszta to bufor; Q&A po części głównej.

**Ściąga:** [`cheatsheet.md`](cheatsheet.md) (po angielsku): architektura projektu, OAuth w MCP krok po kroku, Keycloak, słownik skrótów, „What's next”. Do nauki przed Session i do podglądu w Q&A.

| Sekcja | Czas | Narastająco |
|---|---|---|
| 2. Blok A, slajdy 1–9 | 7 min | 7 |
| 3. Sceny 1–2 | 4 min | 11 |
| 4. Scena 3 | 5 min | 16 |
| 5. Scena 4 | 4 min | 20 |
| 6. Scena 5 | 2 min | 22 |
| 7. Blok B, slajd 10 | 1,5 min | 23,5 |
| 8. Scena 6 | 5 min | 28,5 |
| 9. Blok C, slajdy 11–14 | 2,5 min | 31 |

Zasada cięcia: jeśli po scenie 6 zegar pokazuje więcej niż ~33 min, slajd 11 skracam do jednego zdania (fallback w sekcji 9).

Format każdej sceny: **Mówię** (co powiedzieć), **Klikam / wpisuję** (co zrobić), **Oczekiwany wynik**, **Fallback**.

## 1. Przed Session (checklista)

Porty: API `5080`, dashboard Aspire `15080`, Keycloak `8080` (tylko `OAuth`), MCP Inspector `6274`.

- [ ] Docker działa. Kontener Keycloak **już utworzony** (persistent): raz uruchomić AppHost z `--launch-profile OAuth`, poczekać aż Keycloak wstanie, zalogować się raz jako alice (http://localhost:8080/realms/mcp/account) i wylogować, zatrzymać AppHost. Kontener zostaje, więc w scenie 6 start jest szybki. Bez działającego Keycloaka nie wstanie też API (AppHost czeka na Keycloak), a scena 6 nie ma nagrania zapasowego.
- [ ] AppHost na `None` uruchomiony **tuż przed** Session (świeży seed, "tomorrow" = data UTC po starcie): `dotnet run --project McpServerDotNet.AppHost --launch-profile None` w folderze `McpServerDotNet`. Dashboard otwarty na http://localhost:15080 (link z loginem w konsoli).
- [ ] Opcjonalnie: `DEMO_API_URL=http://localhost:5080 DEMO_API_MODE=None dotnet test --filter "FullyQualifiedName~ArtefactTests"` — sprawdza `logistics.http` i `prompts.md` na żywym API. **Uwaga:** zapisuje dispatch i go anuluje; po teście zrestartować AppHost, żeby seed był czysty.
- [ ] MCP Inspector otwarty (`npx @modelcontextprotocol/inspector`), transport Streamable HTTP, URL `http://localhost:5080/mcp`, jeszcze niepołączony.
- [ ] VS Code otwarty na folderze `McpServerDotNet` (`.vscode/mcp.json`). Serwer `logistics` uruchomiony; `logistics-api-key` (profil `ApiKey`, nie pokazuję go) zatrzymany.
- [ ] Copilot Chat w trybie agent, **model przypięty** (ten sam co na próbie). Tool picker zresetowany: wszystkie narzędzia `logistics` włączone.
- [ ] Wylogowany z Keycloak: w VS Code (menu Accounts) i w przeglądarce (brak sesji SSO alice/bob), żeby logowania w scenie 6 były widoczne i na właściwego użytkownika.
- [ ] `prompts.md` i `logistics.http` otwarte w zakładkach; `slides.html` w przeglądarce, widok prowadzącego (`S`).
- [ ] Powiadomienia wyłączone (Slack, Teams, mail, system), zoom czcionek w VS Code i przeglądarce powiększony.

Do sprawdzenia na próbie (zależy od modelu i wersji VS Code): czy model faktycznie wywołuje `dispatch_order` w wymuszonym nakładaniu (scena 4); czy VS Code automatycznie zatwierdza narzędzia z adnotacją `ReadOnly`; logowanie alice (Copilot) i bob (Inspector).

## 2. Blok A, slajdy 1–9 (~7 min)

**Mówię:**

- Slajd 1 „MCP servers in .NET” (0:15): powitanie. Session opiera się na pomyśle z talku J. Towera z NDC Oslo 2026 „Old API, New Tricks”: dodać MCP obok istniejącego API w .NET. Dziś: jedno API w F#, jeden serwer MCP, na żywo w Copilocie i MCP Inspectorze.
- Slajd 2 „Copilot can already `curl` your REST API. Why MCP?” (0:30): zadaję pytanie i **zostawiam je bez odpowiedzi**. Sekunda ciszy. „Zapamiętajcie to pytanie, wrócimy do niego po demo.” (odpowiedź na slajdzie 12).
- Slajd 3 „Agenda” (0:15): kanapka: krótka teoria, demo, krótko o auth, demo, zamknięcie. Większość czasu to demo na żywo.
- Slajd 4 „MCP architecture” (1:30): trzy role: host (VS Code + Copilot), jeden MCP client na serwer (1:1), MCP server; wiadomości to JSON-RPC. Kluczowe: **model nigdy nie rozmawia z serwerem bezpośrednio**. Model tylko proponuje wywołanie; host je wykonuje, pyta o zgodę i decyduje, co trafia do kontekstu. „Host decyduje” wróci przy zatwierdzaniu w scenie 4.
- Slajd 5 „Server primitives” (1:00): podział według tego, kto steruje: tools wywołuje model, resources dołącza użytkownik/aplikacja, prompts wywołuje użytkownik. Zapowiadam dzisiejszą powierzchnię: **5 tools + 1 resource** (`logistics://rules`). Prompts dopiero w „What's next”.
- Slajd 6 „Transports: local vs remote” (1:00): stdio = lokalny proces w środowisku użytkownika, auth poza specyfikacją. Streamable HTTP = zdalnie, wielu użytkowników, tu obowiązuje autoryzacja MCP (OAuth 2.1). Demo: HTTP na `http://localhost:5080/mcp`.
- Slajd 7 „Deployment variants” (1:00): trzy sposoby dodania MCP do istniejącego API: (a) in-process obok API, wspólna domena F# — **to jest demo**; (b) sidecar wołający REST — API nietknięte, ale dodatkowy hop i kształty REST przeciekają; (c) lokalny wrapper stdio — zero zmian po stronie serwera, ale per użytkownik i auth na własną rękę. (b) i (c) tylko na slajdzie.
- Slajd 8 „Demo domain: terms” (0:45): tabela pojęć z przykładami z seeda. Czytam tylko dwa wiersze: Transport Order (np. ORD-103 Poznań → Berlin, jutro 08–16, Dangerous goods) i Dispatch (Driver + Tractor unit + Trailer, np. DSP-003: Marek na ORD-101). Reszty nie czytam: „Te ID i nazwiska zobaczycie zaraz w demo; tabela zostaje jako ściąga.”
- Slajd 9 „Demo domain: rules” (0:45): Transport Order realizuje Dispatch = Driver + Tractor unit + Trailer. Czytam **5 reguł** raz, powoli: brak nakładania okien (Absence = zajęty); ważne C+E i Driver CPC; Dangerous goods → ADR; Mega → Low deck; typ naczepy zgodny z zamówieniem. „Wrócą w scenach 3–5: ADR w trzeciej, nakładanie w czwartej, reszta w piątej.” Linia z uprawnieniami (C+E, CPC, ADR, Low deck) pod regułami jest dla widowni; czytam ją tylko, gdy ktoś pyta.

**Klikam / wpisuję:** strzałka w prawo przez slajdy 1–9. Na slajdzie 9 znacznik „Live: scenes 1–5” → przełączam się na dashboard Aspire.

**Fallback:** jeśli start się opóźnił, skracam slajdy 6–7 do jednego zdania każdy (pozostaje informacja: demo = HTTP, in-process).

## 3. Sceny 1–2: dashboard Aspire i MCP Inspector (~4 min)

Prompty i kroki: [prompts.md, Scene 1](../prompts.md#scene-1-aspire-dashboard-none), [prompts.md, Scene 2](../prompts.md#scene-2-mcp-inspector-none).

**Mówię:**

- Scena 1 (1 min): „Jeden projekt, `logistics-api`, tryb `None`, czyli bez auth. REST i MCP w tym samym procesie, na porcie 5080. Dane w pamięci, seed od nowa przy każdym starcie, daty liczone od startu.”
- Scena 2 (3 min): „Inspector pokazuje dokładnie to, co dostaje model: nazwy, opisy i schematy wejścia. Nic więcej. Opis narzędzia to jego jedyna instrukcja obsługi.” Przy `list_open_orders`: parametry `from`/`to` są opcjonalne, nie ma ich w `required`. Przy narzędziach tylko do odczytu: adnotacja `ReadOnly`; `dispatch_order` jej nie ma.
- Przy surowym JSON z `get_drivers`: „To jest naiwne narzędzie 1:1 nad `GET /drivers`. Licencje, certyfikaty, nieobecności. Zapamiętajcie, czego tu **nie ma**: dispatchy, ciągników, naczep. Za chwilę to wróci.”

**Klikam / wpisuję:**

1. Dashboard Aspire (http://localhost:15080): zasób `logistics-api`, stan Running, endpoint `http://localhost:5080`.
2. Inspector: Connect → Tools → List Tools; klikam po kolei `list_open_orders`, `find_dispatch_options`, `dispatch_order` (schematy).
3. `get_drivers` → Run Tool (bez argumentów), przewijam JSON.

**Oczekiwany wynik:** 5 tools na liście (`get_drivers`, `list_open_orders`, `find_dispatch_options`, `dispatch_order`, `get_driver_schedule`); w zakładce Resources `logistics://rules`. `get_drivers` zwraca JSON z kierowcami (m.in. Lukasz Nowak, Marek Kowalski) bez żadnej informacji o dispatchach.

**Fallback:**

- Inspector się nie łączy: sprawdzam transport (Streamable HTTP, nie SSE) i URL z `/mcp`; dalej nie działa → `logistics.http` „All Drivers (what get_drivers returns: licences, certificates, Absences, but no Dispatches)” pokazuje te same dane, a schematy narzędzi pokazuję w `Logistics.Api/Mcp.fs` (atrybuty `McpServerTool` i `Description`: dokładnie to, co dostaje model).
- AppHost nie wstał: zajęty port 5080/15080 → zamykam stary proces i startuję ponownie (ok. 20 s); w tym czasie mówię o slajdzie 7 (in-process).

## 4. Scena 3: discovery kontra naiwne narzędzie (~5 min)

Prompty: [prompts.md, Scene 3](../prompts.md#scene-3-discovery-vs-a-naive-tool-none). Nowy czat dla każdego uruchomienia.

**Mówię:**

- Rozgrzewka: „Nie mówię modelowi, jakie są narzędzia. Sam je znalazł i sam wybrał `list_open_orders`.”
- Przed dwoma uruchomieniami: „To samo pytanie dwa razy. Zmieniam tylko to, jakie narzędzia model ma do dyspozycji.” ORD-103 to Dangerous goods, więc potrzebny kierowca z ADR.
- Po uruchomieniu z samym `get_drivers`: **nazywam błąd wprost**: „Model pewnie podaje Lukasza i Marka. Marek jest błędną odpowiedzią: jutro jest już zajęty na ORD-101. Model tego nie mógł wiedzieć, bo `get_drivers` nie widzi dispatchy.” Do tego ciągniki i naczepy: nie wie, które są wolne.
- Po uruchomieniu z samym `find_dispatch_options`: jedna opcja (Lukasz, TRK-02, TRL-02) **plus powody odrzucenia** pozostałych kierowców, czytelnym tekstem.
- Podsumowanie, trzy liczby: liczba wywołań (jedno vs. wiele albo zgadywanie), rozmiar kontekstu (cała lista kierowców vs. gotowa odpowiedź), brakujące fakty (dispatche, sprzęt). „Narzędzie w kształcie zadania, nie w kształcie tabeli.”

**Klikam / wpisuję:**

1. Nowy czat, wszystkie narzędzia włączone, wklejam prompt rozgrzewkowy z `prompts.md`.
2. Nowy czat. Tool picker: odznaczam wszystko poza `get_drivers`. Wklejam prompt o ORD-103.
3. Nowy czat. Tool picker: tylko `find_dispatch_options`. Ten sam prompt.
4. Tool picker z powrotem: wszystkie narzędzia włączone (przed sceną 4).

**Oczekiwany wynik:** rozgrzewka: `list_open_orders`, jutro ORD-103, ORD-104, ORD-105. Uruchomienie 1: pewna siebie, błędna odpowiedź (Lukasz Nowak i Marek Kowalski). Uruchomienie 2: jedna opcja D-01 / TRK-02 / TRL-02 i powody odrzucenia (m.in. Marek: już dispatchowany na ORD-101).

**Fallback:**

- Naiwne uruchomienie przypadkiem trafi (np. model sam zastrzeże niepewność albo poda tylko Lukasza): „Trafił, ale nie mógł tego wiedzieć.” Pokazuję w `get_drivers` brak danych o dispatchach i w `logistics.http` „Existing Dispatches (seed has DSP-001 to DSP-004). Fallback in scene 4 to show state.” DSP-003 = Marek na ORD-101. Fakt rozstrzygający leży poza danymi kierowców.
- Model w uruchomieniu 2 nie woła narzędzia: dopisuję „Use find_dispatch_options.” albo pokazuję wynik w `logistics.http` „Dispatch options for ORD-103: one option (Lukasz, TRK-02, TRL-02) plus rejection reasons”.

## 5. Scena 4: dispatch, zatwierdzenie, reguły domeny (~4 min)

Prompty: [prompts.md, Scene 4](../prompts.md#scene-4-dispatch-approval-domain-rules-none). Jeden czat na całą scenę, wszystkie narzędzia włączone.

**Mówię:**

- Przy oknie zatwierdzenia: „To jest host decydujący (slajd 4). Klikam **Allow** raz. Nigdy »Always allow« dla narzędzia, które zmienia stan: wtedy następne wywołanie, z innymi argumentami, przejdzie bez pytania.” Jeśli narzędzia `ReadOnly` przeszły bez pytania, wspominam, że to ta adnotacja.
- Czytam odpowiedź na głos: „`dispatched by anonymous` — tryb `None`, nie wiemy, kto to zrobił. Wrócimy do tego w scenie 6.”
- Grafik: „Potwierdzenie przez inne narzędzie: dispatch jest w grafiku Lukasza.”
- Wymuszone nakładanie: „Teraz każę modelowi zrobić coś złego: ORD-105 nakłada się w czasie z ORD-103.” Po błędzie: „Reguła domeny wróciła jako zwykły tekst w wyniku narzędzia, z `isError`, a model go przeczytał i wyjaśnił. Nie wyjątek, nie stack trace.”
- Puenta: **„Zatwierdzanie chroni przed modelem; reguły domeny chronią przed człowiekiem.”** Kliknąłem Allow, a i tak nie przeszło.

**Klikam / wpisuję:**

1. Nowy czat, prompt dispatchu z `prompts.md` → okno zatwierdzenia `dispatch_order` → **Allow**.
2. Prompt o grafik Lukasza.
3. Prompt z wymuszonym nakładaniem (ORD-105 „anyway”) → **Allow**.

**Oczekiwany wynik:** 1: „Dispatched ORD-103 as DSP-005: … dispatched by anonymous.” 2: `get_driver_schedule` pokazuje ORD-103 jutro. 3: wynik narzędzia z `isError: true`, tekst „Can't dispatch ORD-105:” z listą złamanych reguł (m.in. „Already dispatched on ORD-103” dla Lukasza i TRK-02); model tłumaczy, dlaczego się nie da.

**Fallback:**

- Model odmawia wywołania `dispatch_order` w kroku 3 (zgaduje z góry, że się nie uda): „Model jest ostrożny, to dobrze; sprawdźmy, co powie serwer.” Wywołuję `dispatch_order` z Inspectora z argumentami podanymi w `prompts.md` (sekcja Scene 4, „Fallback”) → ten sam tekst błędu.
- Stan po dispatchu pokazuję w `logistics.http` „Existing Dispatches (seed has DSP-001 to DSP-004). Fallback in scene 4 to show state.” (po kroku 1 jest tam DSP-005).
- Okno zatwierdzenia się nie pojawiło (wcześniej ktoś kliknął „Always allow”): mówię o tym otwarcie jako o przykładzie, dlaczego nie klikać „Always allow”, i resetuję zgody narzędzi przed sceną 6.

## 6. Scena 5: kontekst dołączony przez użytkownika (~2 min)

Prompt: [prompts.md, Scene 5](../prompts.md#scene-5-context-attached-by-the-user-none). Nowy czat.

**Mówię:**

- „Do tej pory model sam wybierał narzędzia. Teraz to **ja** dołączam resource: reguły w markdown. Tools wywołuje model, resources dołącza użytkownik” (slajd 5).
- Przechodzę przez każdą pułapkę w planie, nawiązując do reguł ze slajdu 9: ORD-104 to Mega, a oba ciągniki Low deck są już zajęte; Piotr Zielinski ma przeterminowany Driver CPC; Tomasz Wojcik jest jutro na zwolnieniu (Absence = zajęty).

**Klikam / wpisuję:** Add Context → MCP Resources → `logistics://rules`; widać załącznik w polu czatu. Wklejam prompt planowania z `prompts.md`.

**Oczekiwany wynik:** plan na jutro, w którym model wyjaśnia: ORD-104 bez opcji (brak wolnego Low deck), Piotr odpada przez Driver CPC, Tomasz przez nieobecność. ORD-103 jest już obsłużone (dispatch ze sceny 4).

**Fallback:**

- Brak „MCP Resources” w Add Context: pokazuję resource w Inspectorze (Resources → `logistics://rules` → Read) i wklejam prompt bez załącznika; model i tak dojdzie do pułapek przez `find_dispatch_options`, ale kontrast „dołączone przez użytkownika” opowiadam słownie.
- Model pominie którąś pułapkę: dopytuję „Why can't Piotr Zielinski or Tomasz Wojcik take any order tomorrow?”, albo pokazuję `logistics.http` „Dispatch options for ORD-104 (Mega): no option, both Low deck tractor units are dispatched”.

## 7. Blok B, slajd 10 (~1,5 min)

**Mówię:**

- Slajd 10 „What MCP adds to OAuth” (1:30). Najpierw role, jednym zdaniem: „Keycloak wydaje tokeny (Authorization Server), nasz serwer MCP je sprawdza (Protected Resource), a VS Code i Inspector to klienci.” Potem trzy czerwone kroki, prostym językiem:
  - **Krok 3, PRM discovery:** „Klient nic nie wie o Keycloaku. Puka do `/mcp` bez tokena, dostaje 401, a w nagłówku adres. Pod tym adresem serwer mówi: tokeny wydaje ten Keycloak, potrzebujesz scope `mcp:tools`. Zero konfiguracji po stronie klienta.”
  - **Kroki 5–6, audience:** „Token ma pieczątkę »ważny tylko dla `http://localhost:5080/mcp`« (claim `aud`). Nasz serwer odrzuci token wydany dla innego serwera, a inny serwer odrzuci nasz. Skradziony token nie zadziała gdzie indziej.”
  - **Krok 5, PKCE:** „Logowanie w przeglądarce kończy się jednorazowym kodem, który klient wymienia na token. PKCE to dowód, że wymienia go ta sama aplikacja, która zaczęła logowanie: klient trzyma sekret u siebie, a do Keycloaka wysłał tylko jego hash. W MCP to obowiązek, metoda S256.”
  - Zamknięcie: „Statyczny nagłówek z bearerem albo klucz API to nie jest autoryzacja MCP: brak discovery, brak audience, brak tożsamości użytkownika.”
- Linia o Entra ID tylko jeśli ktoś zapyta (albo jednym zdaniem): „Keycloak to jeden z wielu Authorization Serverów. Z Entra ID protokół jest ten sam; różnią się konfiguracja, format claimów (audience, role) i rejestracja klientów.”
- Analogie i szczegóły na pytania: [`cheatsheet.md`, OAuth in MCP](cheatsheet.md#oauth-in-mcp-step-by-step).

**Klikam / wpisuję:** slajd 10. Znacznik „Live: scene 6” → przełączam się na terminal AppHosta.

**Fallback:** brak czasu → tylko krok 3 (PRM), resztę jednym zdaniem: „token ważny tylko dla tego serwera, logowanie zabezpieczone PKCE”.

## 8. Scena 6: OAuth (~5 min)

Kroki i prompt: [prompts.md, Scene 6](../prompts.md#scene-6-oauth-oauth); żądania HTTP: `logistics.http` „===== Scene 6 (AppHost profile OAuth) =====”.

**Mówię:**

- Przy restarcie: „Przełączam tryb na `OAuth`. Dane są w pamięci, więc seed jest od nowa: dispatch ze sceny 4 zniknął i ORD-103 znowu jest otwarte.” Keycloak już stoi (kontener persistent).
- `.http`: „Klient bez tokena dostaje 401, a w `WWW-Authenticate` adres metadanych. Tam serwer mówi, kto wydaje tokeny i jakie scope. To jest PRM ze slajdu 10 — klient niczego nie musi mieć skonfigurowanego.” REST zostaje otwarty, chronione jest tylko `/mcp`.
- Inspector jako bob: „bob jest zalogowany, więc uwierzytelnienie przeszło: narzędzia do odczytu działają. Ale **bob nie widzi `dispatch_order` w ogóle**, nie ma go na liście. bob nie ma roli `dispatcher`. To jest autoryzacja, nie uwierzytelnienie.” (Gdyby ktoś wywołał je po nazwie: błąd JSON-RPC `-32600` „Access forbidden: This tool requires authorization.”)
- Copilot jako alice: „alice ma rolę `dispatcher`. Ten sam prompt co w scenie 4, a odpowiedź mówi teraz `dispatched by alice` zamiast `anonymous`: tożsamość przechodzi aż do domeny.”

**Klikam / wpisuję:**

1. Terminal: Ctrl+C, `dotnet run --project McpServerDotNet.AppHost --launch-profile OAuth`; czekam aż `logistics-api` i Keycloak są Running w dashboardzie.
2. `logistics.http`, sekcja Scene 6: wysyłam `POST /mcp` bez tokena, potem `GET /.well-known/oauth-protected-resource/mcp`.
3. Inspector: Authentication → client ID `mcp-inspector`, scope `mcp:tools` → OAuth krok po kroku → logowanie **bob** / `bob` → Connect → List Tools.
4. Przeglądarka: wylogowanie bob z Keycloak (żeby Copilot nie przejął jego sesji SSO).
5. VS Code: restart serwera `logistics` → logowanie w przeglądarce **alice** / `alice`. Nowy czat, wklejam prompt dispatchu ze Scene 6 w `prompts.md` → **Allow**.

**Oczekiwany wynik:** 2: `401` z `WWW-Authenticate: Bearer resource_metadata="http://localhost:5080/.well-known/oauth-protected-resource/mcp"`; PRM z `authorization_servers` = `http://localhost:8080/realms/mcp` i `scopes_supported` z `mcp:tools`. 3: lista narzędzi bob bez `dispatch_order` (4 narzędzia). 5: „Dispatched ORD-103 as DSP-005: … dispatched by alice.”

**Fallback:**

- Keycloak nie startuje albo logowanie się sypie: API w `OAuth` też nie wstanie (AppHost czeka na Keycloak), więc nie pokażę nawet 401 → PRM. Wracam do slajdu 10 i opowiadam przepływ krok po kroku przy diagramie, pokazuję `McpAuth.fs` (walidacja `iss`/`aud`, PRM) i `[<Authorize(Roles = Dispatcher.Role)>]` na `dispatch_order` w `Mcp.fs`, a słownie: alice dispatchuje, bob nie widzi narzędzia. Potem restart na `None`, żeby mieć działające API na Q&A.
- Copilot loguje się jako bob (sesja SSO z kroku 3): wylogowuję w przeglądarce (http://localhost:8080/realms/mcp/account → Sign out), w VS Code Accounts → wyloguj, restart serwera `logistics`.
- Inspector nie wraca po logowaniu: redirect musi iść na `http://127.0.0.1:6274/oauth/callback` — otwieram Inspector pod `127.0.0.1`, nie `localhost`.

## 9. Blok C, slajdy 11–14 (~2,5 min)

**Mówię:**

- Slajd 11 „Demo shortcuts vs production” (0:45): teraz, po demo, skróty, na które poszliśmy świadomie: http zamiast HTTPS (specyfikacja: Authorization Server MUSI używać HTTPS); REST otwarty, bo to inny adapter i chroni się go osobno; klienci (`vscode`, `mcp-inspector`) zarejestrowani w Keycloak z góry, publiczny serwer potrzebowałby CIMD (DCR jest deprecated); dane w pamięci.
- Slajd 12 „Why MCP?” (1:00): **odpowiadam na pytanie ze slajdu 2**: tak, Copilot może `curl`-ować API. MCP dodaje: discovery (schematy i opisy, scena 2), wyselekcjonowaną powierzchnię z zatwierdzaniem per narzędzie (scena 4), standardowe auth z tożsamością i rolami (scena 6), interfejs w kształcie zadania (scena 3: mniej wywołań, mniej kontekstu, czytelne błędy domeny), przenośność między klientami. Ustępstwo: wrapper 1:1 jak `get_drivers` dodaje niewiele. Skills + scripts: inne warstwy; MCP, gdy potrzebna tożsamość, dostęp współdzielony albo zdalny.
- Slajd 13 „What's next” (0:30): trzy rzeczy, każda z przykładem z naszej domeny. **Prompts** (trzeci prymityw serwera, wybiera go użytkownik): np. komenda `/plan-tomorrow`. **Elicitation** (serwer w trakcie wywołania pyta użytkownika o brakującą informację): np. `dispatch_order` dla ORD-106 widzi dwie wolne naczepy Curtainsider i pyta „TRL-01 czy TRL-02?”. **CIMD** zamiast klientów zarejestrowanych z góry: client ID to URL do metadanych klienta, więc Keycloak przyjmie klienta, którego nigdy nie widział (DCR, starszy sposób, jest deprecated). Ostatnia linijka: w specyfikacji 2026-07-28 Sampling i Roots są deprecated. Pytania o nie: [`cheatsheet.md`, What's next](cheatsheet.md#whats-next).
- Slajd 14 „Resources” (0:15): repo Topicu, **`prompts.md`** — każdy prompt z demo po kolei, do samodzielnego powtórzenia; specyfikacja MCP, C# SDK, Aspire, talk J. Towera. Przechodzę do Q&A.

**Klikam / wpisuję:** z powrotem do przeglądarki ze slajdami, slajdy 11–14.

**Fallback:** brak czasu → slajd 11 pomijam (jedno zdanie: „demo ma świadome skróty, lista w slajdach”), slajd 12 zostaje zawsze.

## 10. Przygotowanie do Q&A

- **Q:** Czemu nie skill + skrypt zamiast MCP? **A:** Otwieram ukryty slajd A1 „A1. MCP vs skills + scripts”: `slides.html?showHiddenSlides=true#/a1`. Najpierw ustępuję w kwestii tokenów: skills ładują z góry tylko nazwę i opis, klient MCP ładuje definicje wszystkich narzędzi w każdej turze (Anthropic proponuje „code execution with MCP”: 150k → 2k tokenów w ich przykładzie, kosztem sandboxa). Potem: inne warstwy — skill to wiedza i procedura, MCP to dostęp do systemu; skrypt i tak woła REST. Skill + skrypt przegrywa na tożsamości (bob vs alice), dystrybucji (skrypt na każdej maszynie vs serwer wdrożony raz) i zatwierdzaniu (komenda shella vs `dispatch_order`). Uczciwie: przenośność to dziś słabszy argument, Agent Skills to otwarty standard. Reguła: lokalnie, jeden użytkownik, liczy się budżet tokenów → skill + skrypt; współdzielone, zdalne, wielu użytkowników, tożsamość i role → MCP. Często oba.
- **Q:** Czemu nie zwykły klucz API? **A:** Da się: profil `ApiKey` jest w repo (README, „Auth modes”), nagłówek `X-Api-Key`. Ale klucz to wspólny sekret: brak tożsamości (każdy dispatch to `api-key`, nie alice), brak ról, brak discovery (klucz trzeba ręcznie wkleić w każdym kliencie) i brak audience. Pasuje do service-to-service albo wewnętrznego narzędzia; gdy serwer MCP jest współdzielony i zdalny, a liczy się, kto co zrobił → OAuth.
- **Q:** Jak zbudowany jest projekt / gdzie czego szukać? **A:** Otwieram ukryty slajd A2 „A2. Project architecture”: `slides.html?showHiddenSlides=true#/a2`. Jedno zdanie: dwa adaptery (REST i MCP) nad jedną czystą domeną F# i jednym magazynem w pamięci, w jednym procesie; AppHost Aspire uruchamia API i (w `OAuth`) Keycloak. Mapa plików: [`cheatsheet.md`, Project architecture](cheatsheet.md#project-architecture).
- **Q:** Czemu model nie może po prostu wołać REST? **A:** Może (slajd 2). MCP dodaje discovery, wyselekcjonowaną i zatwierdzaną powierzchnię, standardowe auth z PRM i rolami, interfejs w kształcie zadania i przenośność. Ustępstwo: wrapper 1:1 nad REST (`get_drivers`) daje mało, scena 3 to pokazała.
- **Q:** Jak by to wyglądało z Entra ID? **A:** Protokół ten sam, inna konfiguracja (szczegóły w zwiniętej sekcji ticketu o auth, #11): issuer `https://login.microsoftonline.com/<tid>/v2.0`; `requestedAccessTokenVersion = 2`; App ID URI = dokładnie URL MCP, bez końcowego slasha; w tokenie v2 `aud` = GUID aplikacji API; `scopes_supported` jako pełne URI; płaski claim `roles` natywnie; tylko OIDC discovery (bez RFC 8414), brak `code_challenge_methods_supported`; brak DCR/CIMD; klient VS Code (`aebc6443-996d-45c2-90f0-388ff96faa56`) pre-autoryzowany w „Expose an API”.
- **Q:** Co dostaje bob, gdy wywoła `dispatch_order` po nazwie? **A:** Błąd JSON-RPC `-32600` „Access forbidden: This tool requires authorization.” Filtry autoryzacji SDK (`AddAuthorizationFilters`) ukrywają narzędzie w `tools/list` i blokują wywołanie.
- **Q:** Czemu F# i C#? **A:** API, domena i MCP w F# (`ModelContextProtocol.AspNetCore` 2.2.0 działa z F#: `task {}` zamiast `Async`, parametry opcjonalne przez `[<Optional; DefaultParameterValue(...)>]`); AppHost Aspire w C#.
- **Q:** Czy REST też jest chroniony? **A:** Nie, celowo (slajd 11): chronione jest tylko `/mcp`; REST to inny adapter i w produkcji chroni się go osobno.
