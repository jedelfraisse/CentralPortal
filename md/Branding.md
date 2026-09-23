# Branding Architecture
## Organization Types, Levels, and Sites

## Purpose
Branding in Central Portal must support many kinds of organizations—PTA units, Scout packs, booster clubs, leagues, and more—each with their own identity, colors, logos, and layout preferences. The goal is not a rigid cookie‑cutter website, but a **dynamic, flexible cookie‑cutter system** where each organization inherits defaults but can customize its own look.

This document defines the **branding inheritance model**, the **brand‑kit fields**, and how Organization Types, Organization Levels, and Sites work together to produce a consistent yet customizable experience.

---

# 1. Branding Inheritance Model

Branding flows through five tiers plus a global safety net:

Site override
→ ParentSite override (existing)
→ Organization override (future)
→ OrganizationLevel defaults (future)
→ OrganizationType defaults
→ Global safety net

**ParentSite** is the inheritance that already exists in code today: a Local Unit
Site with an unset value falls back to its parent Division Site
(`SiteThemeExtensions.ResolveOwnOrParent`). It acts as the Organization-level tier
until Organizations themselves carry branding — at which point the "Organization
override (future)" tier takes over this role and the ParentSite step is retired.


### Why this model?
- **OrganizationType** defines what a type *can* customize (brand kit capabilities) and the defaults every community of that type inherits.
- **OrganizationLevel** (future) defines defaults for a specific level — but for most levels the useful "default" is "no palette; defer to Site" (see §3.2).
- **ParentSite / Organization** (future) allows a district or council to define its own defaults for the communities beneath it.
- **Site** allows final customization by the community itself.

This gives every organization a unique identity while keeping the system consistent and maintainable.

---

# 2. Branding Concepts

Branding is divided into **brand kit fields** and **layout configuration fields**.
Layout configuration fields are defined in [Layout.md](Layout.md).

## 2.1 Brand Kit Fields (visual identity)

### Colors
- PrimaryColor  
- AccentColor  
- TopBarColor  
- FooterColor1..4  

### Typography
- HeadingFontFamily  
- BodyFontFamily  

### Logos
- Generated logo template (optional)
- DefaultLogoUrl  
- UsesGeneratedLogo (bool)  

### Secondary Logos
These already exist on `Site` today and stay as Site overrides; their **defaults**
move to OrganizationLevel in the future (Phase 2), with the ParentSite fallback
covering the Division → Local Unit case until then.
- DistrictLogoUrl  
- SchoolCrestUrl  
- PartnerLogoUrl  
- Per-slot sizing: `Width`, `Height`, `PreserveAspectRatio` (for the generated logo, the PTA/variant logo, the district logo, and the partner logo)  

Default tier: **OrganizationLevel** (future) → **Site override** (today).

### Backgrounds
- MastheadBackgroundImageUrl  
- PageBackgroundImageUrl  

> **Field naming:** `MastheadBackgroundImageUrl` maps to the existing
> `Site.MenuBackgroundImageUrl`. The Phase 1 migration renames or aliases that
> field. `PageBackgroundImageUrl` already exists under that name on `Site`.
> The image *asset/URL* is a brand-kit field; how it renders (position, overlay,
> show/hide per level) is a layout-configuration field — see [Layout.md](Layout.md).

### Affiliation Lockup
- AffiliationName  
- AffiliationLogoUrl  
- AffiliationUrl  
- BrandFooterDisclaimer  

### Brand Guidelines
- BrandGuidelinesUrl  
- BrandGuidelinesLabel  

### Email Branding (future)
- EmailHeaderLogoUrl  
- EmailAccentColor  
- EmailFooterText  

### Theme Presets (future)
Multiple named themes per type.

---

# 3. Where Branding Lives

## 3.1 OrganizationType (defines capabilities + defaults)
Each type defines:
- What branding fields exist  
- Default colors  
- Default fonts  
- Default logo template  
- Default affiliation lockup  
- Default backgrounds  
- Default brand guidelines  

This is the **brand kit owner**.

Examples:
- PTA defines navy/blue palette, Arial Black logo template, PTA affiliation.
- Scouts define BSA colors, fleur‑de‑lis fallback logo.
- Booster Clubs define school‑color defaults.

## 3.2 OrganizationLevel (defines level‑specific defaults)
Each level can override Type defaults. A Level is shared by *every* organization at
that level, so it can only hold values common to all of them — never a single
community's colors. For many levels the useful default is simply "defer to Site".

Examples:
- PTA Local Unit → "no palette; defer to Site (school branding expected)"  
- PTA State → PTA palette  
- Scouts Pack → Cub Scouts palette  
- Booster Club → "no palette; defer to Site"  

## 3.3 Organization (future)
Allows a district/council to define its own defaults.

## 3.4 Site (final override)
Each community can override:
- Colors  
- Logos  
- Backgrounds  
- Fonts  
- Header text  

This is where school branding, mascot colors, or local identity is applied.

---

# 4. Logo Generation

Logo generation is a **tool**, not a layout.

### Template lives at:
- OrganizationType (Phase 1)
- OrganizationLevel (Phase 2, optional)

### Behavior:
- If `UsesGeneratedLogo = true`, the system generates a masthead logo using the template.
- If false, the community uploads its own logo or uses the fallback.

---

# 5. Global Safety Net

If no Type/Level/Site values exist:
- Colors fall back to a neutral palette  
- Logos fall back to nothing  
- Fonts fall back to system defaults  

This ensures the site always renders cleanly.

---

# 6. Phase Summary

### Phase 1
- Move brand kit defaults to OrganizationType  
- Add logo template, palette, fonts, affiliation, guidelines  
- Site overrides remain  
- No Level branding yet  
- No layout configuration yet  

### Phase 2+
- Add OrganizationLevel branding defaults  
- Add layout configuration  
- Add theme presets  
- Add background defaults  
- Add email branding  

---

# 7. Phase 1 Implementation Notes

Concrete work for the "move brand kit defaults to OrganizationType" step:

- **Move `PortalConfig.LogoTemplate*` → `OrganizationType`.** The 8 template fields (image URL, box X/Y/W/H %, font family, font color, text alignment) become columns on `OrganizationType`. Font family stops being hard-locked to "Arial Black" and becomes editable per type.
- **Seed the PTA type** with the existing template values and today's `SiteThemeDefaults` palette in the migration's `Up()`, so nothing changes visually for existing PTA communities.
- **Rename `PtaLogoGenerationService` → `LogoGenerationService`**; it takes an `OrganizationType` (or the template fields) instead of reading `PortalConfig` / `SetupService.SaveLogoTemplateAsync`.
- **Add a Branding tab to `OrganizationTypeDetail.razor`** (alongside Description / Levels / Operational Cycles) carrying the logo-template editor, palette pickers, font-stack inputs, affiliation fields, and brand-guidelines link.
- **Retire `/globaladmin/branding`** — redirect it to the Organization Types page (or the PTA type's Branding tab). Drop the `PortalConfig.LogoTemplate*` columns once nothing reads them.
- **Recolor `SiteThemeDefaults` to a neutral palette** (e.g. slate grey) so a type with no palette set still looks deliberate rather than PTA-navy.
- **Ensure Site creation always creates/links an `Organization`.** Branding resolution needs `Site → Organization → OrganizationType`; backfill only covered pre-existing sites. Either make Site creation create an Organization, or have the resolver fall straight to the safety net when none is found (prefer the former).
- **Keep the ParentSite fallback** in `SiteThemeExtensions` until Organization-level branding exists — it is the only thing giving Local Units their Division's colors today.
