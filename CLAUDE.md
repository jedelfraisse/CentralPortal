# **CLAUDE.md (Updated with Generic Portal + Rich PortalHome Requirements)**

# Central Portal — Architecture & Conventions

These rules govern all scaffolding, page generation, navigation, permissions, and terminology for this project. Apply them to all future code suggestions unless the user explicitly overrides them.

## **0. Project Identity (Important)**

The system originally began as a **VBPTA‑specific portal**, but has now evolved into a **generic Division/Local Unit portal** designed for:

- PTA groups  
- school organizations  
- booster clubs  
- councils  
- community groups  
- any future organization that needs a Division → Local Unit structure  

The **Portal site** is the neutral, central hub that introduces the platform, explains its purpose, and guides visitors to Divisions, Local Units, and tools.  
It must **not** be branded as any single organization (e.g., “VBPTA”) — it's now called **Central Portal**.  
It must feel **inclusive**, **organizational**, and **open to all groups**.

---

## **1. Site Hierarchy**

`SiteType` (SiteEngine/Enums/SiteType.cs) still has exactly two real levels today, unchanged:

Portal → Division → Local Unit

Division — e.g., Chesapeake, Norfolk, VB
Local Unit — e.g., Luxford ES, Tallwood ES
“Group” is never a site type. Always say “Division” when referring to a site.

This is still literally how hostname/site resolution works (`RuntimeSiteContext`, `SiteContextResolver`) — that hasn't changed. But it is **no longer the source of truth for organizational modeling**. See §1a — a PTA's real hierarchy (or a Scout troop's, or a billiards league's) is now expressed through `Organization`/`OrganizationLevel`, which supports any depth and any Organization Type, not just this fixed two-level shape. Don't add a third `SiteType` value to model a deeper hierarchy — that's what the Organization Framework is for.

---

## **1a. Organization Framework**

As of Phase 1 (complete — this section is the current source of truth; the original design/implementation-plan docs it was drafted from have been retired), the portal has a second, richer model layered on top of Site:

- **Organization** (SiteEngine/Entities/Organization.cs) — the community itself: identity, self-referencing parent/child hierarchy, and an optional 0-or-1 link to a `Site`. Always exists once a community is created, regardless of whether it has a hosted website.
- **OrganizationLevel** — a position within an Organization Type's hierarchy (data-driven, not an enum — "National", "Council", "Unit" for PTA; "Network", "League", "Team" for a billiards community). A level is a classification an Organization references, not a layer it lives under.
- **OrganizationType** — enhanced from its original minimal form; now owns its Levels and Operational Cycles.
- **OperationalCycle** — a structured, dated operating period (StartDate/EndDate/DisplayLabel/Type), generalizing what `SchoolYear` used to assume for everyone.
- **ParentAccessGrant** — the *only* mechanism for cross-organization access (View/Participation/Administrative). There is no implicit privilege cascade — holding a role on a parent grants nothing on a child unless a grant row says so.

Managed at Global Admin → Organizations (`/globaladmin/organizations/**`, `OrganizationService`).

**What Phase 1 deliberately did NOT touch** — still working exactly as before, don't assume otherwise:
- `SiteUser`/`SiteUserRole`/`CustomRole`/`BoardPosition` and `SiteRoleResolver` — still `Site`-scoped, still keyed by the `SchoolYear` string. Membership migration to Organization-scoping is analysis-only so far, a later phase's work.
- Site/hostname resolution (`RuntimeSiteContext`, `SiteContextResolver`) — still resolves directly against `Site`, not through `Organization`.
- §2's Groups (permission bundles) — still a documented-but-unimplemented concept, unrelated to `ParentAccessGrant`.

Existing Division/Local Unit `Site` rows are backfilled into Organizations automatically on startup (`SeedData.BackfillOrganizations`) — the Portal site is deliberately excluded, since it isn't a community in this model.

---

## **2. Permission Groups**

Groups are permission bundles, not sites.  
They are assigned to users within a site’s scope and are unrelated to the site hierarchy.

Examples: Unit Admins, Division Admins, Event Managers, Membership Importers, Newsletter Editors, Finance Viewers, tool‑scoped groups (e.g., Bingo.Caller).

---

## **3. User Modes**

Anonymous  
Authenticated (Guest)  
Member  
Admin  
SuperAdmin

(Your previous descriptions remain unchanged.)

---

## **4. Navigation by Mode**

(Your previous navigation table remains unchanged.)

---

## **5. Global Admin Navigation**

(Your previous list remains unchanged.)

---

## **6. Tools Pattern (Modular Tool Projects)**

See [md/ToolsArchitecture.md](md/ToolsArchitecture.md) for the full design. In short:

- Every feature beyond the core platform is a **Tool** — its own project under the repo-root `/Tools` folder (`/Tools/<ToolName>/`), compiling to its own DLL plus a `wwwroot/` of UI assets.
- A tool references **`SiteEngine`** only — never `WebApp`. `WebApp` holds no compile-time reference to a tool (that is what keeps tools modular).
- Each tool ships one **`ToolManifest`** class (implements `SiteEngine`'s `IToolManifest`) declaring: `ToolName`, `Version`, `Scope` (`ToolScope`), `Category`, `IconClass`, the routes it exposes, the permission keys it requires, its per-organization configuration schema type, and a `RegisterServices(IServiceCollection)` for its own DI.
- On build, MSBuild targets in the tool `.csproj` copy the DLL to `WebApp/bin` and the UI assets to `WebApp/wwwroot/Tools/<ToolName>/`. Works with plain `dotnet build`.
- At startup SiteEngine scans the build output for `IToolManifest` implementations and populates an in-process `ToolRegistry` (routes, permissions, config schema). Nav / dashboard / `/portal-tools` read that registry — no hardcoded tool list, no hand-seeded `PortalTools` rows.
- Tools are **per organization**: an `OrganizationTools` row (`OrganizationId`, `ToolName`, `ToolConfigJson`, `Enabled`) turns a tool on for an Organization and stores its configuration. Managed at Global Admin → Organizations → Tools tab.
- `<ToolName>` is one stable string used for the folder, the project, `ToolManifest.ToolName`, `OrganizationTools.ToolName`, and the `wwwroot/Tools/<ToolName>/` path.

The old `PortalTools` / `ToolPermission` / `ToolRule` entities are legacy: `PortalTools` (hand-seeded catalog) is superseded by manifest scanning; `ToolPermission` / `ToolRule` remain the intended backing for the (still unbuilt) per-organization permission-check layer that manifests' permission keys feed.

---

## **7. Page Folder Structure (Important)**

Inside `WebApp` (core UI, compiled into the app):

SharedPages/  
PortalPages/  
DivisionPages/  
UnitPages/  

Tool UI does **not** live here. Each tool is a separate project at the repo root — `/Tools/<ToolName>/` with its own `Pages/`, `Services/`, `Models/`, `wwwroot/` — see §6 and [md/ToolsArchitecture.md](md/ToolsArchitecture.md). Its build output lands in `WebApp/bin` and `WebApp/wwwroot/Tools/<ToolName>/`.

(Your previous descriptions of the `WebApp` folders remain unchanged.)

---

## **SharedPages/Home.razor**

SharedPages/Home.razor is the universal entry point for all site types.  
It detects the current SiteType and loads:

PortalHome.razor  
DivisionHome.razor  
UnitHome.razor

SharedPages/Home.razor must not contain site‑specific content directly.

---

# **PortalPages/PortalHome.razor (Expanded Requirements)**

PortalHome.razor is the home component for the **Central Portal**, the main entry point for PTA groups, school organizations, and community groups.

This page must be **rich, descriptive, and welcoming**.  
It should clearly explain what the platform is, who it serves, and how it works — **without requiring the user to click anything**.

### **Anonymous users should see a flowing, multi‑section introduction including:**

• A welcoming headline introducing the Central Portal  
• A short narrative explaining that the platform supports PTA groups, school organizations, booster clubs, councils, and community groups  
• A description of the Division → Local Unit structure  
• A section explaining what visitors can do here:  
 – browse Divisions and Local Units  
 – explore public information  
 – learn about tools  
 – understand how memberships work  
• A section explaining what registered users can do:  
 – apply for Division or Local Unit sites  
 – access member‑only content  
 – use tools  
 – manage their memberships  
• Clear call‑to‑action buttons:  
 – Browse Unit Sites  
 – Explore Tools  
 – Login / Create Account

### **Authenticated users should also see:**

• A “Go to Dashboard” button  
• A brief explanation of what the Dashboard provides

### **Styling and structure requirements:**

• Use headings, subheadings, and short paragraphs  
• Use friendly, inclusive language  
• Use placeholder text where needed  
• Do not implement business logic  
• Do not hard‑code any single organization's branding  
• The page should feel like a real landing page, not a placeholder

---

## 8. Example Tool: GiveBacks Membership Import Tool

Illustrative only — packaged as a modular tool project per §6 and [md/ToolsArchitecture.md](md/ToolsArchitecture.md). The first tool actually being built is `SchoolCafeTool` (`/Tools/SchoolCafe/`); see that doc's §4 for the worked example.

Purpose:  
Import a CSV of member emails for a Local Unit for the current school year (GiveBacks CSV export).

Scope: LocalUnit  
Access: Local Unit Admins only

Behavior:  
Upload CSV → parse emails → create/update membership records → mark admins if CSV includes admin emails → log import history

Project structure (repo-root `/Tools`, one project per tool):  
/Tools/GiveBacksImportTool/  
 GiveBacksImportTool.csproj — references SiteEngine only  
 ToolManifest.cs  
 Pages/ImportPage.razor  
 Services/ImportService.cs  
 Models/ImportModel.cs, GiveBacksImportConfig.cs
 wwwroot/

ToolManifest declares:  
ToolName "GiveBacksImportTool", DisplayName "GiveBacks Membership Import Tool",  
Scope ToolScope.LocalUnit, Category "Membership",  
IconClass "fa-solid fa-file-import",  
RequiredPermissions [ "GiveBacksImport.Run" ],  
ConfigSchemaType typeof(GiveBacksImportConfig) — or null if the tool needs no per-org config.  

Enabled per Local Unit via an `OrganizationTools` row; any configuration lives in that row's `ToolConfigJson`.
