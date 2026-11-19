# Central Portal Tools Architecture

## Status

Proposed (2026-09-07). Design document. The `/Tools` folder exists but is empty; none of `ToolManifest`, `OrganizationTools`, or the discovery pipeline described here is built yet. This document defines the target.

---

# Purpose

Every feature beyond the core platform — SchoolCafe / Lunch Menu, PTA Broadcast, Morning Show, Volunteer Manager, Budget Manager, Event Manager, Game Mode, Digital Signage, CastPoint integration — is built as a **Tool**: a self-contained project under `/Tools` that compiles to its own DLL, ships its own UI and API, registers itself with SiteEngine through a `ToolManifest` class, and is enabled and configured **per organization**.

This replaces the earlier model where a "tool" was just a folder of Razor pages compiled directly into `WebApp` (see [CLAUDE.md](../CLAUDE.md) §6–§8, now updated) and the hand-seeded `PortalTools` catalog table (see [Summary.md](Summary.md) §4). Tools are now discovered from their build output, not edited into the app and not seeded by hand.

Goals:

- **Isolation** — a tool is a separate project. It builds, versions, and fails independently. Adding one does not mean editing `WebApp`.
- **Per-organization** — a tool is off until an Organization enables it, and each Organization carries its own configuration for it.
- **Auto-discovery** — SiteEngine finds tools from their compiled output. No central registration list to maintain.
- **Full capability** — a tool can contribute Razor pages, Blazor components, static assets, HTTP API endpoints, DI services, and a configuration schema.

---

# Relationship To Other Documents

- [CLAUDE.md](../CLAUDE.md) §1a (Organization Framework) — `Organization`/`OrganizationLevel`/`OrganizationType` as the hierarchy a Tool is enabled against. The original Organization Framework design doc additionally described "Organization Types may define … Available Tools" and "Tool Availability Inheritance"; that doc has since been retired and the detail wasn't carried forward elsewhere — treat §3 below (`OrganizationTools`, inheritance as a future addition) as this document's own current statement of that intent. A Tool is enabled against an `Organization`; whether a Tool is *offered* can be gated by `OrganizationType`.
- [Summary.md](Summary.md) §4 (Existing Extensibility), §10 (Tool Registry recommendation), §12 (`PortalTools` unseeded). This document supersedes the "finish wiring `PortalTools`" recommendation with "scan `ToolManifest` from build output instead".
- [CLAUDE.md](../CLAUDE.md) §6 (PortalTools Pattern), §7 (folder structure), §8 (first tool) — updated to point here.

---

# Current State

- `SiteEngine` (class library) + `WebApp` (Blazor Server, .NET 9) is the whole solution (`centralportal.slnx`). No plugin loading, no assembly scanning today — every feature is compiled into `WebApp`.
- `PortalTools` / `ToolPermission` / `ToolRule` / `PortalToolsSessions` entities exist and are EF-mapped but are unseeded and unread by any live UI (`/portal-tools` renders a hardcoded placeholder list).
- `/Tools/SchoolCafe/` is an empty directory — a placeholder for the first real modular tool.
- `Organization` / `OrganizationType` / `OrganizationLevel` are real and Global-Admin-managed. There is no `OrganizationTools` table yet.

---

# 1. Tools Folder Structure

Tool projects live at the **repository root**, one folder per tool, each its own `.csproj`:

```text
CentralPortal/
    SiteEngine/                  core engine: routing, permissions, shared services, ToolManifest contract
    WebApp/                      the running web application
    Tools/
        SchoolCafeTool/
            SchoolCafeTool.csproj
            ToolManifest.cs      the tool's self-registration
            Services/            DI services the tool registers (SchoolCafeService, SchoolCafeMenuFetcher, …)
            Models/              config POCO + DTO records
            Pages/               Razor pages / components (@page routes)
            wwwroot/             JS, CSS, images, other static files
        PTABroadcastTool/
        MorningShowTool/
        VolunteerTool/
        BudgetTool/
        EventTool/
        LunchMenuTool/
        …
```

Each tool is isolated and self-contained. A tool references **`SiteEngine`** (for the `ToolManifest` contract, `ToolScope`, entity types, and shared services) and its own NuGet packages. A tool must **not** reference `WebApp`. `WebApp` must **not** hold a compile-time reference to any tool (that is what keeps them modular — see §5).

`<ToolName>` (the folder name, the project name, the `ToolManifest.ToolName`, the `OrganizationTools.ToolName` value, and the `wwwroot/Tools/<ToolName>/` asset path) must all be the **same stable string**. It is the tool's identity everywhere.

---

# 2. ToolManifest

Every tool ships exactly one class implementing `SiteEngine`'s `IToolManifest`. It is the tool's entire registration surface — SiteEngine never looks anywhere else.

A manifest declares:

| Member | Purpose |
|---|---|
| `ToolName` | Stable identity key. Matches folder / project / asset path / `OrganizationTools.ToolName`. |
| `DisplayName` | Human-facing name for nav, the tools directory, admin toggles. |
| `Version` | The tool's own version string. Surfaced in Global Admin; used for config-migration decisions. |
| `Scope` | `ToolScope` — `Public` / `Division` / `LocalUnit` / `Organization`. Which kind of context the tool attaches to. |
| `Category` | Grouping for the tools directory (e.g. "Membership", "Communications", "Finance"). |
| `IconClass` | FontAwesome class, same convention as `PortalTools.IconClass` / `OrganizationType.IconClass`. |
| `Routes` | The route templates the tool exposes, each with its component type and the permission required to reach it. |
| `RequiredPermissions` | The dotted permission keys the tool defines (e.g. `SchoolCafe.View`, `SchoolCafe.Configure`). Feeds the permission layer — see §6. |
| `ConfigSchemaType` | The POCO that `OrganizationTools.ToolConfigJson` deserializes to for this tool. `null` = the tool has no per-organization configuration. |
| `RegisterServices(IServiceCollection)` | The tool's own DI registrations (its services, HTTP clients, options). Called once at startup. |

Illustrative contract (final shape is an implementation decision):

```csharp
namespace SiteEngine.Tools;

public interface IToolManifest
{
    string ToolName { get; }
    string DisplayName { get; }
    string Version { get; }
    ToolScope Scope { get; }
    string Category { get; }
    string? IconClass { get; }

    IReadOnlyList<ToolRoute> Routes { get; }
    IReadOnlyList<string> RequiredPermissions { get; }
    Type? ConfigSchemaType { get; }

    void RegisterServices(IServiceCollection services);
}

public sealed record ToolRoute(string Template, Type Component, string? RequiredPermission);
```

SiteEngine scans the Tools build output for DLLs, finds every `IToolManifest` implementation, instantiates it, calls `RegisterServices`, and records its routes / permissions / config schema in an in-process `ToolRegistry`. See §5 for the discovery mechanics.

---

# 3. OrganizationTools Table

Tools are **not global**. A registered tool does nothing for an Organization until that Organization has an enabled `OrganizationTools` row.

```text
OrganizationTools
    OrganizationId    Guid    FK → Organization.Id
    ToolName          string  matches IToolManifest.ToolName
    ToolConfigJson    string  tool-specific configuration, serialized ConfigSchemaType (nullable)
    Enabled           bool
    (PK: OrganizationId + ToolName)
```

- **`Enabled`** — the on/off switch. A disabled row keeps the configuration around without exposing the tool.
- **`ToolConfigJson`** — the JSON serialization of the tool's `ConfigSchemaType` POCO (e.g. `SchoolCafeConfig`). SiteEngine exposes a typed accessor, roughly `ToolConfigService.GetConfigAsync<TConfig>(organizationId, toolName)`, which reads this column and deserializes. A tool never reads the column directly.
- **Resolution for a request** — `RuntimeSiteContext` / `SiteContextResolver` resolve the incoming host to a `Site`, and a `Site` maps to at most one `Organization` (`Organization.SiteId`). Tool availability and config for the current request are looked up against that Organization's `OrganizationTools` rows. `Public`-scope tools may resolve against the Portal-level context instead.
- **Inheritance (future)** — a parent Organization enabling a tool may make it available to children ("Tool Availability Inheritance," originally described in the now-retired Organization Framework design doc). Phase 1 is a flat per-Organization row; inheritance is a later addition and would layer on top of this table, not replace it.

Managed at **Global Admin → Organizations → (an Organization) → Tools tab**: a list of every registered manifest with an enable toggle and, when `ConfigSchemaType` is set, a configuration form bound to that schema. Which manifests appear in the list can be filtered by the Organization's `OrganizationType` (offer SchoolCafe to PTA, not to a pool league).

---

# 4. SchoolCafe Tool (worked example)

`Tools/SchoolCafeTool/` — pulls a school's published lunch menu from SchoolCafe and renders it as a PTA "Lunch Menu" page.

```text
SchoolCafeTool/
    SchoolCafeTool.csproj
    ToolManifest.cs
    Models/
        SchoolCafeConfig.cs        the per-organization configuration POCO
        MenuDay.cs, MenuItem.cs    DTO records for fetched data
    Services/
        SchoolCafeService.cs       orchestration: read config, call fetcher, shape for the page
        SchoolCafeMenuFetcher.cs    HTTP client against the SchoolCafe API
    Pages/
        LunchMenu.razor            @page "/tools/schoolcafe/menu" — the public PTA Lunch Menu page
        LunchMenuConfig.razor       admin config UI (optional; the Global Admin Tools tab can also generate one)
    wwwroot/
        schoolcafe.css, schoolcafe.js
```

`SchoolCafeConfig` (serialized into `OrganizationTools.ToolConfigJson`):

```csharp
public sealed class SchoolCafeConfig
{
    public Guid SchoolId { get; set; }          // the school's GUID in SchoolCafe
    public string DefaultServingLine { get; set; } = "";
    public string DefaultMealType { get; set; } = "Lunch";
    public string DefaultGrade { get; set; } = "";
    public bool Enabled { get; set; }           // tool-level feature flag, distinct from OrganizationTools.Enabled
}
```

`ToolManifest.cs` declares: `ToolName = "SchoolCafeTool"`, `Scope = ToolScope.LocalUnit`, `Category = "Communications"`, `IconClass = "fa-solid fa-utensils"`, one route (`/tools/schoolcafe/menu` → `LunchMenu`, no permission — it is public), `RequiredPermissions = ["SchoolCafe.Configure"]`, `ConfigSchemaType = typeof(SchoolCafeConfig)`, and `RegisterServices` adds `SchoolCafeService`, `SchoolCafeMenuFetcher`, and a named `HttpClient`.

Flow: a Local Unit admin enables **SchoolCafeTool** on their Organization and fills in `SchoolId` + defaults → the config is stored as `ToolConfigJson` → the `/tools/schoolcafe/menu` route becomes live for that Organization's site → `SchoolCafeService` reads the config for the current Organization, `SchoolCafeMenuFetcher` calls SchoolCafe, the page renders the menu.

---

# 5. Build Process

Each tool's `.csproj` carries MSBuild targets that run after build and place its output where `WebApp` expects it:

- **DLL** (and the tool's own non-framework dependencies) → `WebApp/bin/<Configuration>/net9.0/`
- **UI assets** (everything under the tool's `wwwroot/`) → `WebApp/wwwroot/Tools/<ToolName>/`

Sketch:

```xml
<Target Name="DeployToolToWebApp" AfterTargets="Build">
  <PropertyGroup>
    <WebAppDir>$(MSBuildThisFileDirectory)..\..\WebApp</WebAppDir>
  </PropertyGroup>
  <ItemGroup>
    <_ToolAssets Include="$(MSBuildThisFileDirectory)wwwroot\**\*" />
  </ItemGroup>
  <Copy SourceFiles="$(TargetPath)"
        DestinationFolder="$(WebAppDir)\bin\$(Configuration)\$(TargetFramework)" />
  <Copy SourceFiles="@(_ToolAssets)"
        DestinationFolder="$(WebAppDir)\wwwroot\Tools\$(MSBuildProjectName)\%(RecursiveDir)"
        SkipUnchangedFiles="true" />
</Target>
```

This works with plain `dotnet build` — no Visual Studio, no `.sln`-only tooling. Tool projects are added to `centralportal.slnx` (or discovered by a `Directory.Build` glob) so CI builds them alongside the core.

**Discovery at runtime.** `WebApp` startup calls into `SiteEngine` (e.g. `builder.Services.AddCentralPortalTools()` + `app.MapCentralPortalTools()`), which:

1. Loads every tool assembly from the bin folder (assembly names matched by convention, e.g. `*Tool.dll`, or a `tools.manifest` file dropped by the build).
2. Finds all `IToolManifest` types, instantiates them, calls `RegisterServices` on each.
3. Populates the in-process `ToolRegistry` with manifests, routes, permission keys, and config-schema types.
4. Registers the tool assemblies with the Blazor `Router` (`AdditionalAssemblies`) so their `@page` components resolve, and any tool-defined minimal-API endpoints via their route table.
5. Static files under `WebApp/wwwroot/Tools/<ToolName>/` are served by the existing static-file middleware — no per-tool wiring.

Navigation, the dashboard, and `/portal-tools` read `ToolRegistry` filtered by the current Organization's `OrganizationTools` rows, instead of a hardcoded list.

**Two coupling options** (decision, not yet made):

- **(A) `ProjectReference` from `WebApp` to each tool.** Simplest — build order and bin-copy are automatic, no reflection load. But `WebApp` recompiles when any tool changes, and "modular" is only organizational. Reasonable as an interim while the pipeline is young.
- **(B) No reference; MSBuild copy + runtime assembly load** (described above). True isolation, tools swappable without recompiling `WebApp`. This is the target.

---

# 6. Integration With SiteEngine and WebApp

| Concern | How |
|---|---|
| **Contract** | Tools depend on `SiteEngine` only. `IToolManifest`, `ToolScope`, `ToolRoute`, `ToolConfigService`, entity types, and shared services (`FileUploadService`, `IDbContextFactory<AppDbContext>`, `SiteRoleResolver`, `OrganizationService`) are the surface a tool builds against. Consider a thin `SiteEngine.Tools` namespace/assembly for the contract so tools do not pull the whole engine's transitive graph. |
| **Discovery** | `SiteEngine.ToolRegistry` scans build output for `IToolManifest` (§5). |
| **Routes / UI** | Manifest `Routes` + Blazor `Router.AdditionalAssemblies`. Pages use `@page`; components are ordinary Razor. Assets served from `wwwroot/Tools/<ToolName>/`. |
| **API endpoints** | A tool may expose HTTP endpoints (minimal API handlers referenced from the manifest, or attribute-routed controllers in the tool assembly registered during discovery). Namespaced under `/api/tools/<ToolName>/…`. |
| **Services** | `RegisterServices(IServiceCollection)` — the tool's DI registrations, run once at startup. Tools resolve shared services by constructor injection like any `WebApp` service. |
| **Configuration** | `ConfigSchemaType` POCO ⇄ `OrganizationTools.ToolConfigJson`, via `ToolConfigService.GetConfigAsync<TConfig>(orgId, toolName)`. |
| **Enablement** | `OrganizationTools` row per (Organization, tool). Global Admin → Organizations → Tools tab. |
| **Permissions** | Manifest `RequiredPermissions` are dotted keys (`SchoolCafe.Configure`). These populate the still-unbuilt permission-check layer around `ToolPermission` (Summary.md §3/§4). Gate shape: `HasToolPermission(user, organization, "SchoolCafe.Configure")`. **Until that layer exists**, tool admin routes fall back to coarse `SiteRole` checks (SiteAdmin+). Building the permission layer is a dependency for any tool that needs finer-grained delegated access (Director/Helper-style). |
| **Data / migrations** | Open question — see below. |

## Relationship to the existing `PortalTools` schema

- **`PortalTools`** (hand-seeded catalog table) is **superseded** by `ToolManifest` scanning. It can be dropped, or kept only as a read model / cache of discovered manifests if a queryable table is convenient.
- **`ToolPermission`** / **`ToolRule`** remain the intended backing store for per-organization permission grants and rate/feature limits. Manifests supply the key vocabulary; a permission-check service still needs to be built.
- **`PortalToolsSessions`** — out of scope here.

---

# 7. Future Tool Development

All new capability ships as a tool in `/Tools`: SchoolCafe / Lunch Menu, PTA Broadcast, Morning Show, Volunteer Manager, Budget Manager, Event Manager, Game Mode, Digital Signage, CastPoint integration, and whatever follows.

Every tool must be:

- **Self-contained** — its own project, DLL, assets, version.
- **Configurable per organization** — a `ConfigSchemaType` (or explicitly none) stored in `OrganizationTools.ToolConfigJson`.
- **Auto-discovered** — a single `ToolManifest`; nothing edited in `WebApp` or `SiteEngine` to add it.
- **Capable of UI and API** — Razor pages/components and, where needed, `/api/tools/<ToolName>/…` endpoints.
- **Able to register services** — via `RegisterServices`, depending on `SiteEngine` only.

An Event *Controller* (Meeting, Talent Show, Bingo — see [README.md](../README.md) "Event Controllers") is a specialization built on a shared Event core, which itself may be delivered as `EventTool`. Controllers and Event *Tools* (Poll, Question, Display) compose on that core; they still package and register as Tools.

---

# Open Questions

1. **Tool-owned data + migrations.** Does a tool get its own tables? Options: (a) the tool ships its own `DbContext` + migrations against the same database; (b) the tool contributes entity types to `AppDbContext` via a discovery hook, and migrations are generated centrally; (c) tools that need storage use a generic per-tool JSON/document table keyed by Organization. This is the single biggest unresolved design point.
2. **Coupling model** — (A) `ProjectReference` vs. (B) runtime assembly load (§5). Pick one; (B) is the stated target.
3. **Contract assembly** — split a `SiteEngine.Tools` contract out of `SiteEngine`, or let tools reference all of `SiteEngine`?
4. **Assembly load context / unload** — hot add/remove of a tool without an app restart? Assume "restart required" for v1.
5. **Manifest/engine compatibility** — a `ContractVersion` on `IToolManifest` so the registry can refuse a tool built against an incompatible engine.
6. **Trust boundary** — tools run in-process at full trust. First-party only for now; no third-party/marketplace loading.
7. **Config form generation** — does the Global Admin Tools tab auto-render a form from `ConfigSchemaType` (reflection/attributes), or does each tool ship its own config component (like `LunchMenuConfig.razor`)? Probably: auto-render by default, tool component overrides when present.
8. **`OrganizationTools.ToolConfigJson` typing** — `nvarchar(max)` + app-side serialization, or a provider JSON column for queryability?
9. **Public-scope tools and Organization resolution** — a `Public` tool has no single Organization; confirm it resolves against the Portal context and stores config (if any) against a well-known Portal Organization row or `PortalConfig`.

---

# Phasing (suggested)

**Phase 1 — pipeline + first tool.**
- `IToolManifest` / `ToolScope` / `ToolRoute` / `ToolRegistry` / `ToolConfigService` in `SiteEngine`.
- `OrganizationTools` entity + migration + `OrganizationService` CRUD.
- Discovery wiring in `WebApp` startup (option A `ProjectReference` acceptable here to de-risk).
- MSBuild deploy target template for tool projects.
- Global Admin → Organizations → Tools tab (enable toggle + config).
- Build `SchoolCafeTool` end-to-end as the proof.

**Phase 2 — hardening.**
- Move to runtime assembly load (option B).
- Permission-check layer over `ToolPermission`; manifests gate real permission keys.
- Auto-generated config forms.
- Retire `PortalTools` seeding path and the hardcoded `/portal-tools` list; drive nav/dashboard/directory from `ToolRegistry` + `OrganizationTools`.

**Phase 3 — scale.**
- Tool-owned data/migration story (Open Question 1).
- Tool availability inheritance down the Organization hierarchy.
- Additional tools (PTA Broadcast, Morning Show, Volunteer, Budget, Event, …).
