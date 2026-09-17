# Muntonrecht — Cluedo Game Implementation Plan

> Deliverable of the architect subtask. Code-mode subtasks must follow this document.
> Workspace root: `/home/dirk/DEV` — backend `muntonrecht/` (.NET 9 API), frontend `Muntonrecht-FE/` (Next.js + shadcn/ui).

---

## 1. Current state (as analyzed)

### 1.1 Backend (`muntonrecht/Muntonrecht.ApiService`)

**Architecture conventions**
- EF Core + Npgsql, `IdentityDbContext<UserModel>` ([MuntonrechtContext.cs](../Muntonrecht.ApiService/Context/MuntonrechtContext.cs)). No EF migrations for game tables — schema is managed by **SQL scripts** in `SqlScripts/`:
  - `auto_<Model>.sql` — generated at startup by [SqlScriptGenerator.cs](../Muntonrecht.ApiService/SqlScripts/SqlScriptGenerator.cs) for every class tagged `[GenerateCrud(true)]` (only written if the file does not exist yet; **adding columns to an existing model does NOT alter the table** — a `manual_0XX` script is required).
  - `manual_0XX_*.sql` — hand-written, idempotent scripts applied in filename order by `RunMigrations()` (retry loop on `42P01`/`42703`), tracked in the `applied_scripts` table.
- [CrudGenerator.cs](../CrudGenerator/CrudGenerator.cs) (Roslyn source generator) generates per model: a `<Entity>DTO`, a `<Entity>Manager`, a full CRUD controller at `api/<Entity>` (`[Authorize(Roles="admin")]` when `GenerateCrud(true)`), DI registration, AutoMapper profile, and `DbSet`s + relations in a partial `MuntonrechtContext`. **A new model with `[GenerateCrud(true)]` gets a full admin CRUD API for free.**
- Auth: cookie auth ("Cookies" scheme) + Google external; roles `admin`. `UserModel.TeamId` links a user to a team. CORS for `localhost:3000` in DEBUG ([Program.cs](../Muntonrecht.ApiService/Program.cs)).

**Entities** ([Models/](../Muntonrecht.ApiService/Models))
| Entity | Purpose / key fields |
|---|---|
| `GameSettingModel` | Single row: solution `MurdererCharacterId` / `MurderWeaponId` / `MurderLocationId` (0 = unconfigured) + `SpeluitlegTitle/Backstory/Rules` (empty = fall back to `GameDefaults`). |
| `CharacterModel` | Suspect: Name, Description, SystemPrompt, AvatarUrl, Personality, 4 stop-keyword columns. |
| `WeaponModel` | Weapon: Name, Description, StopKeywordWeapon. |
| `LocationModel` | Map location: Name, Description, Lat/Lng, `CharacterId` (suspect at that location). |
| `LocationCodeModel` | Physical QR/NFC code: Code, LocationName, UnlockMessage, `CharacterId`. |
| `TeamModel` | Team: Name, IsPlaytest, BarName. |
| `TeamProgressModel` | Per team: `CanAccessChat`, `CanSubmitTip`, `TipSubmitted`, `TipSuspectId` (string), `TipWeaponId`, `TipLocationId`, `TipIsCorrect`, assigned `LocationId`, deduced `WeaponId`. |
| `TeamUnlockModel` | Team × LocationCode unlock with `UnlockedAt`. |
| `TeamWeaponModel` | Team × Weapon discovered via chat keyword. |
| `ChatModel` | Chat transcript rows (Role User/Assistant/System, Message, CharacterId, TeamId). |
| `UserModel` | IdentityUser + FullName + TeamId. |

**Controllers**
- Generated CRUD (admin-only): `api/Character`, `api/Weapon`, `api/Location`, `api/LocationCode`, `api/Team`, `api/TeamProgress`, `api/TeamUnlock`, `api/TeamWeapon`, `api/Chat`, `api/GameSetting`.
- [GameElementController.cs](../Muntonrecht.ApiService/Controllers/GameElementController.cs) (`/api/game`, `[Authorize]`): `Speluitleg` (settings w/ GameDefaults fallback), `Locations` (map data + per-team `isUnlocked`), `Status` (progress flags + counts), `UnlockedCharacters`, `AssignedLocation`.
- [UnlockController.cs](../Muntonrecht.ApiService/Controllers/UnlockController.cs): `GET api/Unlock/{nfcCode}` (redirect flow), `POST EnterCode` (records `TeamUnlock`, sets `CanAccessChat`, sets `CanSubmitTip` when all codes entered), `GET GetUnlocked`.
- [TipController.cs](../Muntonrecht.ApiService/Controllers/TipController.cs): `POST Submit` (one-shot accusation suspect+weapon+location, validated server-side against GameSettings), `POST UploadLocations` (CSV).
- [ChatFEController.cs](../Muntonrecht.ApiService/Controllers/ChatFEController.cs) + [ChatFEManager.cs](../Muntonrecht.ApiService/Managers/ChatFEManager.cs): SSE AI chat (`getchats`, `stream`, `admin-test-stream`, `request-end`), stop-keyword logic, weapon discovery writes `TeamWeapons`.
- [AdminController.cs](../Muntonrecht.ApiService/Controllers/AdminController.cs): users, `TeamProgress` overview, `SetProgress`, `AssignLocation`, `TeamDetail` (incl. chat transcripts).

**`manual_010_cluedo_overhaul.sql` already did**
- Created + seeded the `GameSettings` single row (solution + speluitleg texts).
- Dropped notebook remnants (`Teams.NotebookLocation`, `TeamProgresss.IsNotebookUnlocked`) and `TeamProgresss.TipMotive` — accusation is strictly suspect+weapon+location.

### 1.2 Frontend (`Muntonrecht-FE`)

- **Language: Dutch** throughout (UI labels, error messages, admin pages). Fonts: Libre Baskerville (serif), Inter, Caveat (handwritten). Theme: dark stone/amber "noir" with newspaper styling ([newspaper-headline.tsx](../Muntonrecht-FE/components/game/newspaper-headline.tsx)). Keep this style.
- **Routing/flow today**: `/login` → `/` (speluitleg home: newspaper + backstory + rules + CTA to `/map`) → `/map` (Leaflet map + location list; unlocked → `/chat?character=ID`, locked → `/unlock`) → `/chat` (AI interrogation) → `/tip` (final accusation, gated by `canSubmitTip`). `/unlock` for code entry. `/admin/*` (teams, gebruikers, characters, locations=locatiecodes, weapons, settings, progress).
- **State**: SWR for fetching, `AuthProvider` ([auth-context.tsx](../Muntonrecht-FE/lib/auth-context.tsx)) + `AuthGuard` ([auth-guard.tsx](../Muntonrecht-FE/components/game/auth-guard.tsx)) for auth; `NEXT_PUBLIC_REQUIRE_AUTH=false` bypasses auth with a mock admin user.
- **API client**: [api.ts](../Muntonrecht-FE/lib/api.ts) — `fetchApi` helper with `credentials: 'include'`, grouped `authApi`, `gameApi`, `unlockApi`, `tipApi`, `gameSettingApi`, `weaponApi`, `locationApi`, `characterApi`, `chatApi`, `chatFeApi`, `teamApi`, `locationCodeApi`, `adminApi`.
- **Game shell**: [game-layout-client.tsx](../Muntonrecht-FE/components/game/game-layout-client.tsx) only wraps `AuthProvider`; [game-header.tsx](../Muntonrecht-FE/components/game/game-header.tsx) renders nav (Kaart / Onderzoek / Meld dader / Admin) driven by `gameStatus` flags.
- **Map**: [location-map.tsx](../Muntonrecht-FE/components/game/location-map.tsx) — react-leaflet, custom div icons (🔍 unlocked / 🔒 locked), popups link to chat/unlock.
- **Admin pages** follow a consistent pattern: SWR list + inline create/edit form cards + delete confirm, dark stone theme, red-800 primary buttons ([admin/locations/page.tsx](../Muntonrecht-FE/app/admin/locations/page.tsx), [admin/settings/page.tsx](../Muntonrecht-FE/app/admin/settings/page.tsx), [admin/characters/page.tsx](../Muntonrecht-FE/app/admin/characters/page.tsx)).

### 1.3 How chat is wired in (must not be deleted)

- Backend: `ChatFEController` + `ChatFEManager` (SSE), `ChatModel` rows, `CharacterModel` stop keywords, `TeamWeaponModel` discovery, `CanAccessChat` flag, `UnlockController` redirects to `/chat`.
- Frontend: `/chat` page, `chat-message.tsx`, `character-selector.tsx`, `chatFeApi` in api.ts, nav item "Onderzoek" in game-header, map popups linking to `/chat?character=`, unlock success CTA to `/chat`.

---

## 2. Gap analysis (requirements vs. current state)

| # | Requirement | Current state | Gap |
|---|---|---|---|
| 1 | Skip AI chats (hide, don't delete) | Chat fully wired into nav, map, unlock flow | Need a **feature flag** that hides chat entry points; keep all code paths |
| 2 | Intro "paper cut-out / telegram" message from the detective, admin-editable | Home page has newspaper + backstory (admin-editable via GameSettings) but no detective-telegram intro, and it is not a gated post-login step | New **GameContent** storage + intro screen styled as telegram/paper |
| 3 | Rules screen after intro, admin-editable | Rules exist as `SpeluitlegRules` on home page | Reuse as rules screen content (admin-editable already); needs its own step in the flow |
| 4 | 3 tabs: Map / Logigram / Investigation | Separate pages `/map`, no logigram, no investigation tab | New **tabbed game shell**; logigram is entirely new; investigation tab is new |
| 5a | Location type: redacted interview excerpt | Not present | New content type + redaction rendering + admin authoring |
| 5b | Location type: search picture with interactive hotspots | Not present | New content type + hotspot viewer + admin hotspot editor |
| 6 | Admin panels for everything (intro/rules, locations incl. images/hotspots, logigram, map placement, final solution) | Solution + speluitleg admin exists; locations admin exists (codes only, no map placement UI, no content types); no logigram admin | Extend admin: game content editor, location content editor, logigram editor, map placement |
| — | Final accusation | Exists (`/tip` + TipController) | Keep; surface it from the game shell (Investigation tab) instead of nav-only |

**Key insight**: unlocks today are *character*-scoped (`LocationCode → CharacterId`), while the new game needs *location*-scoped content (interview excerpt / search picture per location). The plan keeps the existing unlock mechanics (codes, `TeamUnlocks`, `CanSubmitTip`) but re-points what a unlock *reveals*: the location's content instead of a chat.

---

## 3. Design decisions (recommended, with rationale)

| Decision | Recommendation |
|---|---|
| **Chat skip mechanism** | Single constant `FEATURE_CHAT_ENABLED = false` in a new `Muntonrecht-FE/lib/feature-flags.ts`. All chat entry points (game-header nav item, map popup/list links, unlock success CTA) check the flag. **Zero deletion**: `/chat` page, `chatFeApi`, backend `ChatFEController`/`ChatFEManager` stay untouched. Backend needs no flag (frontend simply never calls it); optionally guard `ChatFEController` routes later. |
| **Intro/rules storage** | Reuse the existing single-row `GameSettings` pattern: add columns `IntroTitle`, `IntroBody` (telegram text), `RulesTitle`, `RulesBody`. Empty = built-in defaults in `GameDefaults` (extend it). No new table needed; admin settings page already edits this row. |
| **Location content types** | Add columns to `Locations`: `ContentType` TEXT (`'interview'` | `'search_picture'` | `''` = plain), `ContentJson` TEXT (JSON). JSON keeps admin-authored flexibility without new tables. |
| **Interview redaction format** | Author-friendly inline markup in plain text: `[[redacted]]` → black bar, `[[text]]` or plain text → readable. Admin writes e.g. `Het slachtoffer arriveerde rond [[23:15]] bij [[locatie onbekend]].` Renderer converts to spans. No per-word DB rows. |
| **Search picture hotspots** | `ContentJson` = `{ "note": "...", "image": "/images/...", "hotspots": [ { "id": "hs1", "x": 42.5, "y": 30.0, "label": "Glas wijn", "detail": "..." } ] }` — x/y are **percentages** of image size (resolution-independent). Hotspot click opens a detail popover/dialog. Admin editor: image URL + click-to-place hotspots on a preview. |
| **Logigram model** | New tables `LogigramCategories` (dimension: suspect/weapon/location), `LogigramEntries` (row within category, e.g. "Dolk"), `LogigramClues` (admin-authored hint texts, ordered). The **true solution already lives in `GameSettings`** (murderer/weapon/location) — do not duplicate it. Team progress: new `TeamLogigramMarks` table (team × entryId × mark state) so the 7×7×7 grid state persists per team. |
| **Logigram grid UI** | 7 suspects × 7 weapons × 7 locations = three pairwise 7×7 grids (suspect×weapon, suspect×location, weapon×location) — the standard logigram layout, far more usable on mobile than a 3D cube. Cell states: empty / ✕ (ruled out) / ✓ (confirmed). Client-side only; marks persisted via API. |
| **Investigation tab gating** | A location appears in Investigation when the team has unlocked it (existing `TeamUnlocks` via `LocationCode`, or pre-assigned `TeamProgress.Location`). Reuse `GET /api/game/Locations` `isUnlocked`. `CanSubmitTip` (all codes found) still gates the accusation. |
| **Final accusation** | Keep existing `TipController.Submit` + `/tip` page logic, but embed it as a section/dialog inside the Investigation tab (and keep `/tip` route working). No schema change needed. |
| **Map placement admin** | Add lat/lng inputs + a small Leaflet "pick on map" control to the existing admin locations page (locations already have lat/lng columns). |
| **Game content keys** | Not a key-value table — plain columns on `GameSettings` (matches existing pattern, CrudGenerator handles it, admin page already exists). |

---

## 4. Data model changes

### 4.1 New SQL script: `SqlScripts/manual_011_cluedo_game_content.sql`

Follow the conventions of `manual_010` (idempotent, quoted PascalCase identifiers):

```sql
-- GameSettings: intro (telegram) + rules content (empty = GameDefaults fallback)
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "IntroTitle"  TEXT NOT NULL DEFAULT '';
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "IntroBody"   TEXT NOT NULL DEFAULT '';
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "RulesTitle"  TEXT NOT NULL DEFAULT '';
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "RulesBody"   TEXT NOT NULL DEFAULT '';

-- Locations: content type + flexible admin-authored content (JSON)
ALTER TABLE "Locations" ADD COLUMN IF NOT EXISTS "ContentType" TEXT NOT NULL DEFAULT '';
ALTER TABLE "Locations" ADD COLUMN IF NOT EXISTS "ContentJson" TEXT NOT NULL DEFAULT '';
```

### 4.2 New models (each gets `[GenerateCrud(true)]` → free admin CRUD API + auto table script)

```csharp
// Models/LogigramCategoryModel.cs
[GenerateCrud(true)]
public class LogigramCategoryModel
{
    public int Id { get; set; }
    /// <summary>Stable key: "suspect" | "weapon" | "location".</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Display label, e.g. "Verdachten".</summary>
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

// Models/LogigramEntryModel.cs
[GenerateCrud(true)]
public class LogigramEntryModel
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public LogigramCategoryModel Category { get; set; } = null!;
    /// <summary>Display name, e.g. "De barman" or "Dolk".</summary>
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
}

// Models/LogigramClueModel.cs
[GenerateCrud(true)]
public class LogigramClueModel
{
    public int Id { get; set; }
    /// <summary>Clue text shown to players (admin-authored).</summary>
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

// Models/TeamLogigramMarkModel.cs
[GenerateCrud(true)]
public class TeamLogigramMarkModel
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public TeamModel Team { get; set; } = null!;
    /// <summary>LogigramEntryModel Id this mark applies to.</summary>
    public int EntryId { get; set; }
    public LogigramEntryModel Entry { get; set; } = null!;
    /// <summary>"none" | "cross" | "check".</summary>
    public string Mark { get; set; } = "none";
}
```

Notes:
- The generator maps `string` → `TEXT`, so `ContentJson`/`Mark` need no special handling. FKs are auto-created because the `*Id` property names match model names.
- The true solution stays in `GameSettings` (`MurdererCharacterId`, `MurderWeaponId`, `MurderLocationId`). **Admin must pick logigram entries whose names match the configured Characters/Weapons/Locations** — the settings page should show a warning if the solution IDs are 0. (Mapping entry→entity by name is admin's responsibility; documented in the admin UI copy.)

### 4.3 Model changes

- `GameSettingModel`: add `IntroTitle`, `IntroBody`, `RulesTitle`, `RulesBody` (string, default empty).
- `LocationModel`: add `ContentType` (string) and `ContentJson` (string).
- `GameDefaults`: add `IntroTitle`, `IntroBody` (detective telegram text), `RulesTitle`, `RulesBody` defaults (Dutch).

### 4.4 JSON schemas (documented contract)

**Location `ContentJson` for `ContentType = "interview"`**
```json
{
  "header": "Politie-interview · Verklaring #4",
  "meta": "Afdeling Zutphen · 25-04-2026 · 02:14",
  "lines": [
    { "speaker": "Rechercheur De Groot", "text": "Waar was u om [[23:00]]?" },
    { "speaker": "Getuige", "text": "Ik was in de [[keuken]]. Ik hoorde [[een klap]] uit de tuin." }
  ]
}
```
Rendering rule: `[[...]]` → redacted span (black bar; optionally reveal on long-press/tap-hold for accessibility testing — recommended: **not** revealable for players).

**Location `ContentJson` for `ContentType = "search_picture"`**
```json
{
  "note": "Politievondst: doorzocht de werkkamer op 25-04-2026.",
  "image": "/images/pages/werkkamer.jpg",
  "hotspots": [
    { "id": "hs1", "x": 42.5, "y": 30.0, "label": "Leeg glas wijn", "detail": "Vingerafdruk van de barman gevonden." },
    { "id": "hs2", "x": 70.0, "y": 55.5, "label": "Scheurkalender", "detail": "Laatste afgestreepte dag: zaterdag." }
  ]
}
```

---

## 5. API design

### 5.1 Player-facing (extend `GameElementController`, pattern: `[Authorize]`, anonymous objects, camelCase)

| Endpoint | Method | Returns |
|---|---|---|
| `/api/game/Intro` | GET | `{ title, body }` — GameSettings w/ GameDefaults fallback (same pattern as `Speluitleg`) |
| `/api/game/Rules` | GET | `{ title, body }` — idem (or extend `Speluitleg` response; **recommended: separate endpoints** so home page and flow steps are independent) |
| `/api/game/Locations` | GET | **extend existing** with `contentType` and `content` (parsed JSON, only included when `isUnlocked`; locked locations get `content = null`) |
| `/api/game/Logigram` | GET | `{ categories: [{id, key, name, sortOrder}], entries: [{id, categoryId, name, imageUrl, sortOrder}], clues: [{id, text, sortOrder}], marks: [{entryId, mark}] }` — marks scoped to current team |
| `/api/game/Logigram/Marks` | PUT | body `{ marks: [{ entryId, mark }] }` — replaces the team's marks (bulk save; simple + idempotent) |
| `/api/game/Status` | GET | **extend existing** with `introSeen`, `rulesSeen` (see 5.2) |

### 5.2 Intro/rules completion tracking

Add two columns to `TeamProgressModel` (→ `manual_011` script): `IntroSeenAt TIMESTAMP NULL`, `RulesSeenAt TIMESTAMP NULL`, plus player endpoints:
- `POST /api/game/MarkIntroSeen` and `POST /api/game/MarkRulesSeen` — set timestamp for current team (idempotent, uses `GetOrCreateTeamProgress`).

The frontend flow uses these to decide which step to show (see 6.1). Admin can reset via existing `SetProgress` pattern (optional: extend `SetProgressDTO`).

### 5.3 Admin

- **Free** via CrudGenerator for the three new models: `api/LogigramCategory`, `api/LogigramEntry`, `api/LogigramClue` (+ `api/TeamLogigramMark` for debugging).
- `api/GameSetting` PUT already updates the row — the new intro/rules columns flow through automatically once the DTO/model have the properties (DTO is generated from model properties).
- `api/Location` PUT already updates the row — `contentType`/`contentJson` flow through automatically.
- Optional: extend `AdminController.SetProgressDTO` with `introSeen`/`rulesSeen` overrides.

---

## 6. Frontend architecture

### 6.1 Post-login flow

```
/login ──► /intro (telegram from detective) ──► /rules ──► /game (3 tabs)
```

- `/intro` and `/rules` are new pages wrapped in `AuthGuard`. Each fetches its content via SWR, renders it, and has a "Verder →" button that POSTs the seen-marker and routes to the next step.
- Routing guard logic (client-side, in the game shell): on `/game`, check `Status.introSeen`/`rulesSeen`; if not seen, redirect to `/intro`. `/intro` always accessible post-login (re-readable); `/rules` likewise. Returning teams skip straight to `/game` because the flags are set.
- `/` (home) keeps working as a landing/speluitleg page; its CTA points to `/game` (or `/intro` when flags unset). Keep the newspaper component for flavor.

### 6.2 Game shell with 3 tabs

- New route `/game` + `components/game/game-tabs.tsx` (client component using shadcn `Tabs`): tabs **Kaart**, **Logigram**, **Onderzoek** (Dutch labels matching existing conventions).
- Each tab lazily mounts its content; SWR keys shared (`game-locations`, `game-status-*`, `logigram`).
- `GameHeader` nav updated: replace `Kaart` link with `Spel` → `/game`; keep `Meld dader` (gated by `canSubmitTip`) and `Admin`; **remove the `Onderzoek` chat nav item behind the feature flag** (conditional, not deleted).
- Keep `/map` as a thin redirect to `/game` (or re-export the map tab) so old links/NFC flows don't break.

### 6.3 Map tab

- Reuse `LocationMap` as-is inside the Map tab. Change popup/list CTAs: unlocked → link to `/game?tab=onderzoek&location=ID` (opens the location's content in the Investigation tab) **when chat is disabled**; when the flag is re-enabled, restore the `/chat?character=` links (both code paths present, selected by flag).
- Keep the location list under the map (mobile-friendly fallback) with the same flag-driven CTA logic.

### 6.4 Logigram tab

- `components/game/logigram-grid.tsx`: renders three 7×7 matrices from `GET /api/game/Logigram`:
  - grid A: suspects (rows) × weapons (cols)
  - grid B: suspects (rows) × locations (cols)
  - grid C: weapons (rows) × locations (cols)
- Cell tap cycles `none → cross → check → none`. Row/col headers show entry names (+ small avatar images when `imageUrl` set).
- Clues listed above/below the grids (accordion or plain list).
- Marks kept in local state (SWR mutate optimistic) and saved via `PUT /api/game/Logigram/Marks` (debounced or on tab switch / "Opslaan" button — **recommended: save on every toggle, debounced 800 ms**, simple and robust).
- Optional validation aid (client-side only): when a row has exactly one ✓, highlight it. No server-side solution checking (solution stays hidden).

### 6.5 Investigation tab

- Lists unlocked locations (`GET /api/game/Locations`, filter `isUnlocked`); locked ones shown greyed with a lock + link to `/unlock`.
- Tapping a location opens its content:
  - `interview` → `components/game/interview-transcript.tsx`: police-format transcript (monospace/typewriter header, speaker labels, redacted spans rendered as black bars from `[[...]]` markup).
  - `search_picture` → `components/game/search-picture.tsx`: image with absolutely-positioned hotspot buttons (percent coords); tapping a hotspot opens a `Dialog`/`Popover` with `label` + `detail`. Include a subtle pulsing ring animation on hotspots. Fallback: if `hotspots` empty, show image + note only.
  - `''` (no content type) → plain `description` text (current behavior).
- Accusation CTA at the bottom when `canSubmitTip` — opens the existing accusation form (extract the form from `/tip` into `components/game/accusation-form.tsx` and reuse it on both `/tip` and the Investigation tab).

### 6.6 Feature flag & chat skip (exact mechanics)

- New file `Muntonrecht-FE/lib/feature-flags.ts`:
  ```ts
  /** Master switch for the AI-chat feature. Set true to re-enable chats. */
  export const FEATURE_CHAT_ENABLED = false;
  ```
- Consumers (all keep their code, just flag-gated):
  1. `game-header.tsx` — nav item `{ href: '/chat', label: 'Onderzoek' }` only when `FEATURE_CHAT_ENABLED`.
  2. `location-map.tsx` popups + `app/map/page.tsx` list — unlocked CTA: `FEATURE_CHAT_ENABLED ? /chat?character=ID : /game?tab=onderzoek&location=ID`.
  3. `app/unlock/page.tsx` success CTA — `FEATURE_CHAT_ENABLED ? router.push('/chat') : router.push('/game?tab=onderzoek')`.
  4. `app/chat/page.tsx` — top-of-component guard: `if (!FEATURE_CHAT_ENABLED) router.replace('/game')` (page unreachable, code intact).
- Backend: **no changes, no deletions**. `ChatFEController`/`ChatFEManager`/`ChatModel`/stop-keywords remain fully functional; they are simply never called by the FE while the flag is false. (Optional hardening, non-blocking: return 503 from `ChatFEController` when an appsettings flag `Features:ChatEnabled=false` — decision left to implementer, default: skip.)

### 6.7 Styling

- Intro: "paper cut-out / telegram" — reuse the paper-texture + serif approach of `newspaper-headline.tsx`: amber-50 card, dashed border, `--font-handwritten` accents, "TELEGRAM" header band, per-line typewriter feel. Content fully admin-authored.
- Rules: same paper card style, numbered list rendering (reuse the `ruleLines` pattern from `app/page.tsx`).
- All new UI in Dutch, stone/amber noir palette, shadcn components (`Tabs`, `Dialog`, `Accordion`, `Progress`).

---

## 7. Admin panel designs (match existing admin patterns)

All under `/admin/*`, added to `NAV_ITEMS` in `app/admin/layout.tsx`:

1. **`/admin/settings` (extend)** — add cards for *Intro (telegram)* (`IntroTitle`, `IntroBody` textarea) and *Regels* (`RulesTitle`, `RulesBody` textarea) next to the existing solution + speluitleg cards. Same save button/draft pattern.
2. **`/admin/locations` (extend)** — per location: `ContentType` select (Geen / Interview / Zoekfoto), dynamic content editor:
   - Interview: header/meta inputs + repeatable speaker/text rows; live preview with redaction rendering; hint text explaining `[[...]]` syntax.
   - Search picture: image URL input (with preview), note textarea, **interactive hotspot editor**: click on the image preview to add a hotspot at those percent-coordinates, list of hotspots with label/detail inputs and delete buttons, drag or re-click to reposition (simplest robust version: click-to-place + numeric x/y inputs).
   - Also add lat/lng inputs + optional Leaflet pick-on-map mini-map for map placement.
3. **`/admin/logigram` (new)** — three sections (Categorieën / Items / Aanwijzingen) with the standard list + inline form pattern: categories (key select suspect/weapon/location, name, sortOrder), entries (category select, name, imageUrl, sortOrder), clues (textarea, sortOrder). Show a live 7×7×7 count summary and a warning when counts ≠ 7 per category or when the solution in settings doesn't correspond to any entry names.
4. **`/admin/progress` (extend, optional)** — show logigram mark counts per team; extend `SetProgress` overrides if implemented.

---

## 8. Phased code subtasks (execution order)

Dependencies: Phase 1 → 2 → 3; Phases 4–6 depend on 1–3; Phase 7 last. Each subtask is delegatable to code mode independently.

### Phase 1 — Backend: schema + game content API
**Scope**: `manual_011` SQL script; `GameSettingModel` + `LocationModel` new columns; `GameDefaults` intro/rules texts; `GameElementController` `Intro`/`Rules` endpoints; extend `Locations` response with `contentType`/`content` (content only when unlocked); `TeamProgressModel` `IntroSeenAt`/`RulesSeenAt` + `MarkIntroSeen`/`MarkRulesSeen` endpoints; extend `Status` with `introSeen`/`rulesSeen`.
**Files**: `SqlScripts/manual_011_cluedo_game_content.sql`, `Models/GameSettingModel.cs`, `Models/LocationModel.cs`, `Models/GameDefaults.cs`, `Models/TeamProgressModel.cs`, `Controllers/GameElementController.cs`.
**Acceptance**: API starts, scripts apply idempotently; `GET /api/game/Intro` and `/Rules` return defaults; `Locations` includes content only for unlocked locations; seen-flags persist per team; swagger shows new endpoints.

### Phase 2 — Backend: logigram entities + API
**Scope**: three new models with `[GenerateCrud(true)]` + `TeamLogigramMarkModel`; player endpoints `GET /api/game/Logigram` and `PUT /api/game/Logigram/Marks` (team-scoped, bulk replace).
**Files**: `Models/LogigramCategoryModel.cs`, `Models/LogigramEntryModel.cs`, `Models/LogigramClueModel.cs`, `Models/TeamLogigramMarkModel.cs`, `Controllers/GameElementController.cs`.
**Acceptance**: auto scripts created; admin CRUD endpoints live (admin-role gated); player endpoint returns categories/entries/clues + own team marks; PUT replaces marks; solution never exposed.

### Phase 3 — Frontend: feature flag + game shell + flow
**Scope**: `lib/feature-flags.ts`; `/intro` and `/rules` pages (paper/telegram styling, seen-markers, Verder-flow); `/game` shell with 3 tabs (`game-tabs.tsx`); `/map` redirects to `/game`; GameHeader nav update (Spel + flag-gated chat item); flag-gate map/unlock CTAs and `/chat` guard; extend `lib/api.ts` (Intro/Rules/Logigram DTOs + calls, GameLocationDTO `contentType`/`content`, Status flags).
**Files**: `lib/feature-flags.ts`, `lib/api.ts`, `app/intro/page.tsx`, `app/rules/page.tsx`, `app/game/page.tsx`, `components/game/game-tabs.tsx`, `app/map/page.tsx`, `components/game/game-header.tsx`, `app/unlock/page.tsx`, `app/chat/page.tsx`, `app/page.tsx` (CTA → `/game`).
**Acceptance**: login → intro → rules → 3-tab shell; returning users land on `/game`; chat nav/CTAs hidden with flag false; no chat code deleted; `/map` still works via redirect.

### Phase 4 — Frontend: investigation tab (interview + search picture)
**Scope**: `interview-transcript.tsx` (redaction renderer), `search-picture.tsx` (percent-based hotspots + dialog), location list + detail rendering in the Onderzoek tab, accusation form extraction + reuse (`accusation-form.tsx`, used by `/tip` and the tab).
**Files**: `components/game/interview-transcript.tsx`, `components/game/search-picture.tsx`, `components/game/accusation-form.tsx`, `app/game/page.tsx` (tab content), `app/tip/page.tsx` (refactor to reuse form).
**Acceptance**: unlocked locations render their content type correctly; `[[...]]` renders as black bars; hotspots clickable with detail dialog; locked locations show lock + `/unlock` link; accusation reachable when `canSubmitTip`.

### Phase 5 — Frontend: logigram tab
**Scope**: `logigram-grid.tsx` (three 7×7 matrices, tap-cycle marks, clue list), marks persistence via `PUT /api/game/Logigram/Marks` (debounced), mobile-friendly layout.
**Files**: `components/game/logigram-grid.tsx`, `app/game/page.tsx`.
**Acceptance**: grids render from API data; toggling persists after reload; clues visible; usable on phone widths.

### Phase 6 — Admin panels
**Scope**: settings page intro/rules cards; locations page content-type editor (interview builder + hotspot editor + map placement); new `/admin/logigram` page (categories/entries/clues CRUD); nav items.
**Files**: `app/admin/settings/page.tsx`, `app/admin/locations/page.tsx`, `app/admin/logigram/page.tsx` (new), `app/admin/layout.tsx`, `lib/api.ts` (logigram admin APIs).
**Acceptance**: a full game can be authored end-to-end without code changes: intro/rules text, location content (both types incl. hotspots), logigram categories/entries/clues, solution, map coordinates.

### Phase 7 — Polish & QA pass
**Scope**: seed/verify a full playthrough with 7×7×7 content; check unlock → investigation gating; verify NFC redirect still lands sensibly (`/api/Unlock/{code}` currently redirects to `/chat` — change target to `/game?tab=onderzoek` when flag false, keeping the chat branch); admin progress overview still correct.
**Files**: `Controllers/UnlockController.cs` (redirect target only), minor FE fixes.
**Acceptance**: complete playtest: login → intro → rules → map → unlock code → read interview/search picture → fill logigram → accusation → result screen.

---

## 9. Risks & open points

1. **SqlScriptGenerator does not ALTER existing tables** — new columns on `GameSettings`/`Locations`/`TeamProgresss` MUST go through `manual_011` (the generator only writes `auto_*.sql` once). Covered in Phase 1.
2. **Logigram ↔ solution linkage** — solution references Character/Weapon/Location IDs while logigram entries are separate rows. Recommended: admin keeps names consistent; settings page shows a validation warning. Alternative (rejected for now): FK columns on logigram entries — more admin friction, no player benefit.
3. **Hotspot authoring UX** — click-to-place on a preview image is the pragmatic choice; drag-resize is explicitly out of scope. Percent coordinates keep it resolution-independent.
4. **Redaction reveal** — decided: redactions are never revealable in the player UI (mystery integrity). Admin preview in the admin editor shows the full text.
5. **Marks sync across devices** — marks are per team (not per user); last-write-wins on bulk PUT. Acceptable for co-located teams sharing one device.
6. **NFC redirect** — `ScanNfc` redirects to `/chat`; must become flag-aware (`/game?tab=onderzoek` when chat disabled) — included in Phase 7 (or Phase 3 if preferred).
7. **`/tip` route** — keep alive (deep links), reuse the extracted accusation form.
8. **Language** — everything player- and admin-facing stays Dutch; game content is admin-authored Dutch.

---

## 10. Contract update (logigram marks)

A 7×7×7 logigram needs **pairwise cell marks** (each unordered cross-category pair
appears in exactly one cell), but per-entry storage gives each entry a single mark
while it participates in ~12 independent cells. The marks contract therefore
distinguishes **per-entry marks** from **unordered pair marks** via a nullable
second entry reference (`TeamLogigramMarkModel.LogigramEntryBId`, added by
`SqlScripts/manual_012_logigram_pair_marks.sql`; unique index replaced by
`(TeamId, LogigramEntryId, COALESCE(LogigramEntryBId, 0))`; FK cascade to
`LogigramEntrys`).

**`GET /api/game/Logigram`** — `marks` shape (camelCase, solution never exposed):
```json
{
  "categories": [{ "id": 1, "key": "suspect", "name": "Verdachten", "sortOrder": 0 }],
  "entries":    [{ "id": 11, "categoryId": 1, "name": "De barman", "imageUrl": null, "sortOrder": 0 }],
  "clues":      [{ "id": 21, "text": "…", "sortOrder": 0 }],
  "marks":      [
    { "entryAId": 11, "entryBId": 12, "mark": "cross" },
    { "entryAId": 11, "entryBId": null, "mark": "check" }
  ]
}
```
- `entryBId: null` → per-entry mark (row/column header conclusion).
- Both ids set → unordered pair mark (one grid cell); the API normalizes
  `entryAId = min(idA, idB)`, `entryBId = max(idA, idB)`.

**`PUT /api/game/Logigram/Marks`** — body (same normalization server-side):
```json
{ "marks": [ { "entryAId": 11, "entryBId": 12, "mark": "check" },
             { "entryAId": 11, "entryBId": null, "mark": "cross" } ] }
```
Validation: `mark` ∈ `none|cross|check`; every referenced entry (both sides of a
pair) must exist; `entryA == entryB` collapses to a per-entry mark; last write
wins per `(entryA, COALESCE(entryB, 0))`; bulk replace upsert (rows missing from
the payload are removed); `"none"` marks are not persisted. Response: `{ "saved": true }`.

A ✓ (check) pair mark client-side also auto-derives ✕ (cross) marks for the rest
of that row/column — derived marks persist like manual ones.

---

## 11. Spel in elkaar klikken — quick start

Handleiding voor een spelleider/auteur om **een compleet spel zonder code-aanpassingen** in elkaar te klikken. Volgorde is bewust: eerst de oplossing, dan de puzzel, dan de locaties, dan teams.

### 11.1 Beheerdersstappen (in deze volgorde)

1. **Instellingen (`/admin/settings`) — oplossing + intro/regels**
   - Kies de oplossing in de *Oplossing*-kaart: **Moordenaar** (character), **Moordwapen** (weapon) en **Moordplek** (location). Niet-geconfigureerd = 0; de aanklacht wordt server-side tegen deze drie waarden gevalideerd (`TipController.Submit`).
   - Vul de *Intro (telegram)*-kaart (`IntroTitle`/`IntroBody`) en de *Regels*-kaart (`RulesTitle`/`RulesBody`). Leeg = ingebouwde `GameDefaults`-teksten.
2. **Logigram (`/admin/logigram`) — categorieën, items, aanwijzingen**
   - Maak drie categorieën met de vaste keys `suspect`, `weapon`, `location` (de speler-grids zijn op deze keys gebouwd) en vul elk met (idealiter 7) items.
   - **Belangrijk:** geef items namen die exact overeenkomen met de Characters/Weapons/Locations die de oplossing aanwijzen — de koppeling tussen logigram-items en de echte oplossing gaat via namen (het admin-scherm waarschuwt bij counts ≠ 7 en bij een oplossing zonder matchende itemnamen).
   - Schrijf aanwijzingen die de oplossing uniek afleidbaar maken, zonder de oplossing direct te noemen. Toets de redeneerketen tegen de drie pairwise grids.
3. **Locaties (`/admin/locations`) — dossier per locatie + kaart + NFC-codes**
   - Vul naam, omschrijving en **latitude/longitude** (kaartcoördinaat voor de pin op `/game?tab=kaart`).
   - Kies per locatie een inhoudstype:
     - *Interview*: kop + meta-regel + spreker/beurt-regels. Gebruik `[[...]]` rond fragmenten die zwartgeplakt moeten zijn (bijv. `rond [[23:00]]`); de editor valideert gebalanceerde markeringen en toont een live voorbeeld.
     - *Zoekfoto*: afbeeldings-URL (bijv. `/images/pages/hotel.png` — bestanden staan in `Muntonrecht-FE/public/images/pages/`), notitie en **klik op de voorbeeldfoto om hotspots te plaatsen** (x/y in procenten, label + toelichting per vondst; `[[...]]` mag ook in toelichtingen).
   - **NFC/QR-codes**: maak per fysieke locatie een *locatiecode* aan (locatiecodes-sectie op dezelfde admin-pagina). De NFC-tag-URL is `https://<host>/api/Unlock/<CODE>`; een scan ontgrendelt de locatie en stuurt spelers (met chat uit) naar `/game?tab=onderzoek`, (met chat aan) naar `/chat`. Handmatig invoeren kan altijd via `/unlock`. Zodra alle bestaande codes gevonden zijn, zet de backend `canSubmitTip` → de aanklacht verschijnt in het Onderzoek-tabblad én op `/tip`.
4. **Teams (`/admin/teams`) + koppeling**
   - Maak teams aan en koppel gebruikers (`/admin/gebruikers` → *Koppel aan team*). Voortgang en vlaggen: `/admin/progress`; optioneel startlocatie toewijzen (`AssignLocation`) of `IsPlaytest` zetten voor testteams.

### 11.2 Demo-seed uitproberen (optioneel)

`SqlScripts/optional_seed_example_game.sql` zet een compleet **voorbeeldspel** neer: 3 categorieën × 7 items (suspect/weapon/location), 5 strikt afleidbare aanwijzingen met oplossing *de huiskeeper · touw · de wijnkelder*, en 2 voorbeeldlocaties (interview-dossier *De Wijnkelder* + zoekfoto-dossier *De Werkkamer* met hotspots op `/images/pages/hotel.png`).

Het script is bewust **niet** geregistreerd in de `.csproj` en wordt daardoor **nooit automatisch** uitgevoerd bij het starten van de API:

```bash
# Vereist: de API is minstens één keer gestart (auto_* + manual_001..012 toegepast)
psql "<connectionstring>" \
  -f muntonrecht/Muntonrecht.ApiService/SqlScripts/optional_seed_example_game.sql
```

Idempotent: veilig om meerdere keren te draaien — alleen ontbrekende rijen worden toegevoegd (items alleen in lege categorieën, locaties alleen bij een nog onbekende naam, aanwijzingen alleen in een lege aanwijzingentabel). Onderaan het script staat een **uitgecommentarieerde `UPDATE`** die de echte oplossing in `GameSettings` op het voorbeeld-drieluik zet (maak eerst Characters/Weapons/Locations met exact de genoemde namen aan via de admin, verwijder dan de streepjes zodat het voorbeeldpuzzeltje echt oplost).

### 11.3 Chat later weer aanzetten

- **Frontend**: in `Muntonrecht-FE/lib/feature-flags.ts` zet je `FEATURE_CHAT_ENABLED = true`. Alle afgeschermde paden (nav-item *Gesprek* in de game-header, kaart-popups/-lijst, unlock-succes-CTA, `/chat`-guard) vallen dan automatisch terug op het oude gedrag — er is geen chatcode verwijderd.
- **Backend**: zet in `appsettings.json` `"Features": { "ChatEnabled": true }` zodat de NFC-scan (`GET /api/Unlock/<code>`) weer naar `/chat?character=…` stuurt. Houd beide vlaggen samen in sync.

### 11.4 Rooktest — volledige speelronde (handmatig)

Geautomatiseerde E2E vraagt om de echte DB/auth-omgeving; loop dit lijstje handmatig na (speler-apparaat + zo nodig tweede scherm voor de admin):

1. **Inloggen** — `/login` (of Google via `/api/ExternalAuth/LoginGoogle`); de speler is aan een team gekoppeld.
2. **Intro** — na de login landt de speler op `/intro` (detective-telegram); *Verder* markeert `IntroSeenAt` en gaat naar `/rules`.
3. **Regels** — `/rules` tonen; *Verder* markeert `RulesSeenAt` en gaat naar `/game`.
4. **Flow-guards** — opnieuw inloggen landt direct op `/game`; `/` stuurt terug naar `/game` (of `/rules` als de regels nog niet gezien zijn); `/map` stuurt door naar `/game?tab=kaart` waarbij query-parameters (zoals `?code=…`) behouden blijven; `/chat` is onbereikbaar en stuurt naar `/game` zolang chat uit staat.
5. **Kaart** — pinnen tonen locaties op de kaart (lat/lng); vergrendelde locaties linken naar `/unlock`.
6. **Ontgrendelen via code** — voer een geldige locatiecode in op `/unlock` (handmatig of via NFC-scan): success-melding + CTA *Ga naar onderzoek*; ongeldige of al-gebruikte codes geven een nette fout; de ontgrendelde code verschijnt in de lijst.
7. **Onderzoeksdossier** — ontgrendelde locatie toont het interview (met zwarte `[[...]]`-balken) of de zoekfoto (hotspots klikbaar, toelichting in een dialoog, geredigeerde fragmenten blijven zwart); vergrendelde locaties tonen een slotje + link naar `/unlock`; deep link `/game?tab=onderzoek&location=<ID>` opent het dossier direct.
8. **Logigram** — cellen cirkelen leeg → ✕ → ✓; een ✓ leidt automatisch ✕ af in de rest van de rij/kolom; rij/kolom-koppen tonen de ✓/✕-conclusie; na herladen zijn alle markeringen terug (500 ms debounce, bulk replace per team).
9. **Aanklacht** — pas zichtbaar nadat **alle** locatiecodes gevonden zijn (`canSubmitTip`): kies dader/wapen/plek in het Onderzoek-tabblad of op `/tip`; juist → felicitatie, fout → eindbeeld; een tweede inzending wordt geweigerd (one-shot).
10. **Backend-guards** — `/api/game/Logigram` bevat nooit de oplossing; vergrendelde locaties geven `content: null`; een speler zonder team krijgt nette foutmeldingen in plaats van crashes.
