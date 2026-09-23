# Layout Architecture
## Shared Layout Primitives + Type/Level Configuration

## Status

Target design, mostly unbuilt — with one real first step shipped ahead of the
phasing in §6: `Site.HeaderLayoutStyle` (Inline/Stacked) and `SiteShellLayout`
exist today as a genuine Site-level layout override (§4.3), rendered at
`/dev/layout-preview` for side-by-side comparison against the live
Division/Unit layouts. It is **not yet wired into `SiteLayoutSelector`** — every
real site still renders through `DivisionLayout`/`UnitLayout` as before. See §2.

## Purpose
Layouts should not be hard-coded per organization type. Instead, Central Portal uses **shared layout primitives** that adapt based on OrganizationType and OrganizationLevel configuration.

This allows:
- PTA National → structured masthead with affiliation  
- PTA Local Unit → school‑colored masthead with crest  
- Scouts Pack → simple masthead with uploaded logo  
- Booster Club → school‑branded masthead with stadium background  
- Pool League → sponsor‑branded masthead  

All using the **same underlying layout engine**.

(The names above — "PTA National", "PTA Local Unit", etc. — are `OrganizationLevel`s
in the target model. Today's `SiteType` of Portal / Division / LocalUnit will be
replaced by OrganizationLevel.)

---

# 1. Layout Inheritance Model

Layout configuration flows through the same tiers as [Branding.md](Branding.md):

Site override (existing, expanded in future)
→ ParentSite override (existing)
→ Organization override (future)
→ OrganizationLevel layout config (future)
→ OrganizationType layout config (future)
→ Shared layout primitives

**ParentSite** is the fallback that already exists in code: a Local Unit Site with
an unset value inherits from its parent Division Site. It stands in for the
Organization-level tier until Organizations carry their own layout config.


---

# 2. Existing Layout Code (Today)

The primitives model below is a generalization of code that already exists. The
migration converts these into configuration rather than replacing them wholesale.

- **`SiteLayoutBase`** — shared base for the site layouts; already computes logo-box
  styles from per-Site sizing fields.
- **`CityWideLayout`** — the Division (city-wide) masthead/nav/footer layout.
- **`UnitLayout`** — the Local Unit masthead/nav/footer layout (school crest, etc.).
- **`SharedPages/Home.razor`** — selects the home component by `SiteType`
  (`PortalHome` / `DivisionHome` / `UnitHome`). This is the layout-selection point.
- **Existing per-Site masthead sizing fields** on `Site`:
  `MastheadLogoDefaultWidth/Height`, `GeneratedLogoWidth/Height/PreserveAspectRatio`,
  `PtaVariantLogo*`, `DistrictLogo*`, `PartnerLogo*`. These are Site-level layout
  config that already ships — "Site override" is not greenfield.
- **`SiteShellLayout`** (new) — a generic, data-driven masthead/nav/footer shell
  meant to eventually replace `DivisionLayout`/`UnitLayout`. Renders only what a
  Site actually has data for (no hardcoded PTA nav/copy) and supports exactly two
  masthead arrangements via `Site.HeaderLayoutStyle` (Inline/Stacked) — a first,
  intentionally narrow instance of §4.3's "Site-level layout overrides," shipped
  ahead of the Type/Level config in §4.1/§4.2. Editable at Global Admin → Sites →
  a site → Layout; previewable at `/dev/layout-preview` on that site's hostname.
  Not yet wired into `SiteLayoutSelector`, which still resolves every real site
  to `DivisionLayout`/`UnitLayout` by `SiteType`.

**Layout selection will migrate from `SiteType`-driven to `OrganizationLevel`-driven.**
Today only Portal/Division/LocalUnit exist; the target model keys layout choice off
the Organization's Level instead.

---

# 3. Shared Layout Primitives

These are the building blocks of every page.

## 3.1 Masthead Primitives
- Primary logo slot  
- Secondary logo slot (district/partner)  
- Affiliation lockup slot  
- Title/subtitle slot  
- Masthead background image  
- Accent bar  

## 3.2 Navigation Primitives
- Horizontal nav  
- Vertical nav  
- Collapsible mobile nav  
- Icon-only nav  

## 3.3 Footer Primitives
- Multi-column footer  
- Simple footer  
- Disclaimer slot  
- Social links slot  

## 3.4 Page Primitives
- Hero section  
- Content section  
- Sidebar section  
- Card grid  
- Directory list  
- Announcement bar  

These primitives are shared across all types.

---

# 4. Layout Configuration

## 4.1 OrganizationType (future)
Defines:
- Default masthead structure  
- Default footer variant  
- Default navigation style  
- Optional layout presets  

## 4.2 OrganizationLevel (Phase 2)
Defines:
- Level-specific masthead structure  
- Whether affiliation appears  
- Whether partner/district logos appear  
- Navigation style  
- Footer variant  
- Background image behavior (show/hide, position, overlay)  

## 4.3 Site (existing, expanded in future)
Per-Site layout overrides already exist (masthead logo-box sizing). Future work adds
the rest: masthead structure, nav style, footer variant.

---

## Background image ownership

The background image is split between the two documents:

- **Brand kit** ([Branding.md](Branding.md)) owns the asset — the image URL itself.
- **Layout** owns the render behavior — show/hide, position, and overlay, configurable per level.

---

# 5. Examples

> In the target model these rows are **OrganizationLevels**. Today's `SiteType`
> (Portal / Division / LocalUnit) is what gets replaced by OrganizationLevel.

### PTA National
- Masthead: primary logo + affiliation  
- Footer: multi-column + disclaimer  
- Nav: horizontal  

### PTA Local Unit
- Masthead: generated logo + school crest  
- Footer: simple  
- Nav: horizontal  

### Scouts Pack
- Masthead: uploaded logo only  
- Footer: simple  
- Nav: horizontal  

### Booster Club
- Masthead: uploaded logo + stadium background  
- Footer: simple  
- Nav: vertical  

All using the same primitives.

---

# 6. Phase Summary

### Phase 1
- Keep PTA layouts (`DivisionLayout`/`UnitLayout`) as the live path for every real site
- One real Site-level override shipped early: `HeaderLayoutStyle` (Inline/Stacked) on
  `SiteShellLayout`, previewable but not yet switched on for real traffic — see §2
- Begin defining shared primitives internally

### Phase 2+
- Introduce Type/Level layout configuration
- Convert PTA layouts into configuration presets
- Switch `SiteLayoutSelector` over to `SiteShellLayout` for real sites, retiring
  `DivisionLayout`/`UnitLayout`
- Expand Site-level overrides beyond masthead arrangement (nav style, footer variant)
- Add theme presets and layout variants
