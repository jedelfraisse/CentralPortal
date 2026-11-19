# Central Portal — Product Direction
### One Platform Engine, Multiple Deployments, Tools Instead of Sub-Products

## Status

Vision / product-direction doc. It explains **why** Central Portal is built as one
shared engine with pluggable capability rather than a family of separate apps, and
**when** a genuinely separate deployment (On-Prem) is warranted versus when a new
idea should just be another Tool. It does not define implementation mechanics —
those live in the documents linked below, and where this doc used to describe
mechanics directly (module packaging, per-module databases, an installer wizard),
it now defers to them instead of restating a second, drifting version.

Nothing in the "Installation Profiles" or "CMS Layer" sections below is built yet.
Today there is one running profile: Central Portal Cloud, single database, CMS
layer always on. On-Prem is a target, not a shipped mode.

---

## Relationship to Other Documents

- **[CLAUDE.md](../CLAUDE.md)** — the standing architecture rules: Site hierarchy
  (§1), the Organization Framework (§1a), Tools pattern (§6), page folder structure
  (§7). This doc doesn't override any of it.
- **[ToolsArchitecture.md](ToolsArchitecture.md)** — owns everything about how a
  capability is packaged, discovered, enabled per-Organization, and configured.
  When this doc talks about "a new idea becomes a Tool," that Tool is built exactly
  as ToolsArchitecture.md describes: a project under repo-root `/Tools/<ToolName>/`,
  in the same solution, referencing `SiteEngine` only, registered via
  `ToolManifest`, turned on per Organization via `OrganizationTools`. This doc does
  **not** define a separate "Module" concept — earlier drafts did, and that was a
  naming split with no real difference. "Module" below always means "Tool" in
  ToolsArchitecture.md's sense.
- **[Summary.md](Summary.md)** — a dated architecture snapshot (Event Framework
  planning). Not a direction doc; consult it for "what exists today," not "where
  we're headed."

---

## Overview

**Central Portal** is the name of the software platform, not any one organization
using it. It's a hybrid CMS + operations engine, and it's designed so that the
things that used to look like "separate future projects" — CueConnect, a money
manager, a civic-group variant — turn out to be **Tools running inside one Central
Portal installation**, not new codebases.

There is exactly one axis where a genuinely separate deployment still makes sense:
**who is hosting it and for whom.**

- **Central Portal Cloud** — hosted at Delfraisse.com, multi-tenant, public-facing.
  This is where PTA units, booster clubs, civic groups, CueConnect-as-a-Tool, and
  everything else in the "community group" space lives, as Organizations within
  one installation.
- **Central Portal (On-Prem)** — installed inside a single company's own
  environment, for internal/employee use, with public CMS features turned off.

Both run the **same platform engine and the same Tools codebase**. The difference
is deployment target and which Tools an installation's Organizations have enabled
— not a fork, and not a separate product line.

---

## Core Concept

**One shared platform engine and Tools catalog. A small number of installation
profiles. Per-Organization control over which Tools are turned on.**

This gives:

- Shared development, shared bug fixes, shared Tools catalog across every
  installation.
- Deployment-level separation only where it's actually needed (On-Prem's data and
  auth boundary), not per idea.
- New product ideas default to "a Tool," which is cheap: a project under
  `/Tools/`, a manifest, an `OrganizationTools` row. They only become their own
  installation if there's a real hosting/tenancy reason to split — see
  "When Something Should Be Its Own Installation" below.

---

## Platform Layers

### 1. Platform Engine (Shared)

The core codebase every installation runs: `SiteEngine` + `WebApp` per
[CLAUDE.md](../CLAUDE.md) §7, plus the Tools discovery/registration pipeline in
[ToolsArchitecture.md](ToolsArchitecture.md). Includes the Organization Framework
(§1a), permissions, the Tool registry, dashboard, shared UI components, routing.

Always enabled, on every installation.

### 2. CMS Layer (Optional, target)

Public-facing content: page builder, themes, public pages/announcements/events.
Central Portal Cloud needs it; an internal On-Prem deployment doesn't.

**Not implemented today** — there is no code-level toggle for this. Today's
`WebApp` always runs with public pages available. Treat "CMS Layer" as a design
target for On-Prem (§ below), to be built when On-Prem itself is built — not a
switch that exists now.

---

## Installation Profiles

### Central Portal Cloud

Hosted at Delfraisse.com. The community-facing, multi-tenant SaaS deployment.

- Every PTA unit, booster club, civic league, or community group is an
  **Organization** (per CLAUDE.md §1a), not a separate installation.
- Enabled layers: Platform Engine + CMS Layer, public routing/auth/themes, the
  Organization Framework's multi-organization hierarchy.
- Ideas like CueConnect, a money-manager Tool, or a civic-group variant live here
  **as Tools**, scoped to whichever Organizations/`OrganizationType`s enable them
  — see "When Something Should Be Its Own Installation" below for the exception.

### Central Portal (On-Prem)

Installed inside a company's own environment, for internal/employee use. Target
design, not yet built:

- CMS layer disabled; internal routing/auth/themes only.
- At install time, a **Base Site Name** (e.g. "Hops Inspire") brands the
  deployment. This is not a new mechanism — it's the existing Portal Site's own
  branding fields (`SiteAdminService`, per [Branding.md](Branding.md)), set once
  during setup rather than left as "Central Portal."
  Internally, deployment is single-tenant or multi-company, using the same
  Organization Framework Cloud uses — a company and its departments are just an
  Organization tree with `Site` disabled/private, not a different model.
- Enabled Tools are internal-workflow ones (dashboards, reports, data entry) —
  same Tools mechanism as Cloud, different catalog turned on.

Both profiles use the same codebase and the same Tools pipeline; they differ in
configuration (CMS on/off, which Tools are enabled, auth/branding defaults) — not
in engine, not in repository.

---

## When Something Should Be Its Own Installation

Default assumption: **a new idea is a Tool inside Central Portal Cloud.** Only
split it into its own installation when there's a real hosting reason, e.g.:

- It needs to be sold/operated as a fully standalone product under its own brand,
  independent of Delfraisse.com/Central Portal entirely.
- It has data-isolation or compliance requirements that justify a dedicated
  deployment rather than per-Tool configuration (see ToolsArchitecture.md's Open
  Question 1 on tool-owned data — that's the mechanism to reach for first).

CueConnect, a money-manager Tool, and similar ideas raised so far don't currently
meet that bar — they're Tools. Keep this list short in practice; every entry here
is a maintenance and identity-federation cost (see Identity Provider below).

---

## Tools (formerly "Module Distribution")

Fully owned by [ToolsArchitecture.md](ToolsArchitecture.md) — repo-root
`/Tools/<ToolName>/`, same solution as `WebApp`/`SiteEngine`, `ToolManifest`
self-registration, per-Organization enablement via `OrganizationTools`.

Two things earlier drafts of this document got wrong, corrected here:

- **Not a separate repository.** Tools live in this repo, alongside
  `SiteEngine`/`WebApp`, per ToolsArchitecture.md §1. A separate
  `CentralPortal.Modules` repo isn't part of the plan.
- **Not a per-installer database picker.** Whether a Tool gets its own database,
  shares the main one, or uses some other storage split is
  ToolsArchitecture.md's **Open Question 1** — explicitly unresolved there,
  not a decided "prompt the installer" feature. Don't build toward a database-
  choice wizard until that question is answered.

### Identity Provider

All Tools within one installation share that installation's identity/auth —
today, passwordless email-code login (`PasswordlessSignInService`, real and
shipped). This is already "one identity across every Tool," because Tools run
inside one `WebApp` process — there's no cross-boundary problem to solve as long
as an idea stays a Tool.

Cross-installation identity (one login spanning Central Portal Cloud and a
separately-branded standalone product) only becomes a real question if something
graduates out of Tools into its own installation per the section above. It isn't
a concern for anything currently planned.

---

## Deployment Model

### Cloud Deployment (Central Portal Cloud)
- Multi-tenant, multi-organization, public-facing.
- Hosted by Delfraisse.com.
- CMS + Platform Engine layers.
- Shared Tools catalog; Organizations opt into the Tools relevant to them.

### On-Prem Deployment (Central Portal On-Prem) — target, not yet built
- Single-tenant or multi-company, internal-only.
- Installed on company servers.
- Platform Engine only (CMS layer disabled).
- Internal-workflow Tools; branding via Base Site Name at install (§ above).

Both deployments run the **same codebase**, configured differently. Neither forks
the engine or the Tools pipeline.

---

## Benefits of This Architecture

1. **One development effort.** Fixes, improvements, and new Tools benefit every
   installation.
2. **Two deployment models, not N products.** Cloud (community-facing) and
   On-Prem (internal) cover the real hosting distinction; everything else is a
   Tool choice within one of them.
3. **Maximum reuse.** Dashboards, reports, permissions, the Organization
   Framework, the Tools pipeline — shared everywhere.
4. **New ideas are cheap by default.** A new idea starts as a Tool (a project,
   a manifest, an `OrganizationTools` row) — not a new repo, database, or
   installation, unless it earns that per "When Something Should Be Its Own
   Installation."
5. **Scalable without proliferation.** Sports, church, neighborhood, school
   variants are `OrganizationType`s and Tool bundles within Central Portal
   Cloud, not new installations.

---

## Summary

**Central Portal is the platform.** Central Portal Cloud is the hosted,
multi-tenant SaaS; Central Portal On-Prem (target) is the internal deployment,
branded per company via its Portal Site's own branding fields. Both run the same
engine and the same Tools pipeline defined in ToolsArchitecture.md.

New capability defaults to **a Tool**, scoped per Organization via
`OrganizationTools`. It becomes its own installation only when there's a genuine
hosting/tenancy reason to split — not because it's a distinct idea.

Same engine. Same Tools pipeline. Two deployment profiles. Per-Organization
configuration for everything else.
