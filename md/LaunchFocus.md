# Launch Focus — First Real Unit Site (Middle School PTA)

_Created 2026-11-19. Read this before starting new work. If a task doesn't move an
item on the "Launch scope" list forward, it waits until after launch._

## Guiding principle: every page answers questions

Every page answers one main question. The test: **can a parent on a phone, between
other things, get the answer in about 10 seconds, without logging in or learning any
terms?** Parents never see "Division", "Local Unit", or "Organization Level".

### Central Portal (platform) questions
| Question | Answered by |
|---|---|
| What is it? | PortalHome headline and story |
| Who do we serve? | PTAs, school groups, boosters, councils, community groups |
| How does it keep an org and its units connected and on mission? | Shared look, shared calendar and announcements flowing down, tools per org (later) |
| How do I find my school's site? | Community Directory / Browse Units |

Most parents will land directly on their school's site, not the portal. The portal
mainly speaks to PTA leaders deciding whether to use it.

### Unit site questions (a parent's view)
1. **What's happening this week?** The weekly newsletter, featured at the top.
2. **What do I need to do?** Deadlines, forms, money due, sign-ups.
3. **What's coming up?** The next few weeks of events.
4. **How can I help or get involved?** PTA membership, volunteering, meetings.
5. **How does this school work?** Good to Know (standing info).
6. **Who do I contact?**
7. *(Members)* **What's the PTA working on?** Minutes, budget, board contacts.

## Content model decisions

- **One feed for school life, with a source tag.** Items are tagged `School`, `PTA`, or
  `District`. Parents care about what and when, not who organized it. The tag still
  gives the PTA visibility.
- **Visibility on every item:** `Public` or `Members`. Default is public for events,
  the newsletter, and Good to Know. Directory, minutes, finances, and board contacts
  are members-only.
- **Dated items expire.** Every time-sensitive item has an end date and hides itself
  after it passes. Nobody has to remember to remove it.
- **Link, don't copy.** Good to Know items are a one-line explanation plus a link to
  the school or district page or PDF, so they stay correct when the source changes.
  Each has a "last reviewed" date for the August refresh.
- **No student names or photos** on public pages without consent.

### Weekly newsletter
- The school sends a ParentSquare email linking to a **public Smore newsletter**
  (e.g. `https://app.smore.com/n/<id>`). There's no RSS feed or embed code, so a volunteer posts it by hand.
- **Never store the ParentSquare email-tracking link.** It's personal to the
  recipient and expires. Store the clean Smore URL.
- Volunteer's weekly job (about 2 minutes): paste the Smore link, write a 2–3 line
  summary, add any new dated items, move any new standing reminder into Good to Know.
- No separate on/off setting: the This Week section appears only once a
  Newsletter item exists, and the item's title carries the name (e.g. "Raider
  Reader — Nov 13"). The newest issue is always the one featured.
- "Today" for expiry uses US Eastern time for every site (`SchoolClock`), which
  is a launch simplification. Add a time zone per site when a school outside
  Eastern time joins.

### Mapping the real newsletter onto the site
Roughly 60% of a typical issue is standing reference repeated every week.

**Good to Know** (permanent page, edited a few times a year, collapsible groups on phone):

| Group | Examples |
|---|---|
| Every school day | Hours, A-B day schedule, block schedule |
| Rules & expectations | Dress code, Chromebook take-home policy, student pledge, house system |
| Health & safety | Sick-child guidance, physical form needed before tryouts |
| Meals & money | Meals free for all, free/reduced application |
| Parent access | ParentVue, Canvas parent access, district app guide |
| Support & programs | Tutoring and homework club, gifted, Title I, mental health resources |
| Contact | Main office, address, school website |

**This Week / Coming Up:** dated events and deadlines with source tags.
**Get Involved:** PTA membership, meetings, volunteer asks.

### Membership expiry
Roles and memberships do **not** lapse automatically. The July 1 school year
(`SchoolYear.Current()`) is a PTA convention, not a rule for every group, and
automatic lapsing could lock a site's only volunteer out over the summer. For
launch, a unit admin removes or renews members by hand. Later, expiry becomes an
option per Organization Type ("memberships end with each cycle"), driven by
that type's Operational Cycles. Types with no cycles never lapse. A test in
`SiteRoleResolverTests` pins the current behavior.

## Launch scope (the whole first release)

1. **Content items:** one entity for site content with a kind (Newsletter, Event,
   Action Needed, Good to Know, Get Involved), source tag, visibility, dates or
   expiry, Good to Know group, sort order, and last reviewed. Core platform, not a Tool.
2. **Content editor:** a simple, phone-friendly page under Site Admin, so one
   Officer or Site Admin can add, edit, and expire items.
3. **UnitHome rebuild:** This Week (newsletter), Action Needed, Coming Up, Get
   Involved, plus member-only sections shown only to members.
4. **Good to Know page.**
5. **Go live:** load the real school's content, deploy, and have a few parents try
   it on their phones.

### Deployment path
`main` → `stage` (stage.yml, FTP deploy) → `live` (live.yml, FTP deploy to
https://centralportal.org/). Each push builds, tests, deploys, and runs a health
check. Promote through stage first and check it there before live. As of
2026-11-19, live is 10 commits behind main and does not have the Organization
Framework yet, so its next promotion carries those database migrations too.

## Not yet (after launch)

- All Tools (SchoolCafe, GiveBacks import, Event Framework, Poll/Question/Meeting tools)
- Automatic newsletter import or Smore scraping
- A built-in translate button (rely on the browser's translate for now)
- Organization Framework phase 2+ (membership migration), Permission Groups
- Membership expiry per cycle (an option per Organization Type, using Operational Cycles)
- On-Prem profile, CueConnect, other org types' special features
- Division-level content flowing down to units
- **Fundraising Partners directory** (see below)

## Future idea: Fundraising Partners directory

_Captured 2026-11-19. Post-launch, not in scope now._

Nearly every group on the platform is a nonprofit that is always looking for
fundraisers. Central Portal could host a directory of local fundraising opportunities,
focused on small businesses near each group but open to any business.

**Example:** "Big Apple Pizzeria" partners with Central Portal to run a "Spirit
Night". The school community is invited to order from the business on a set night,
and the business donates x% of those sales to the group.

**What the platform adds:**
- **Directory:** businesses near a group, with what they offer (percentage, which
  days, any rules such as mentioning the school or using a flyer or code).
- **Scheduling:** many businesses don't want to manage dates. Central Portal (or the
  directory) can handle requests, avoid two schools claiming the same night, and run
  a recurring weekly rotation across schools.
- **Promotion:** a booked Spirit Night shows up automatically on the unit site as a
  PTA-tagged item in Coming Up.
- **Follow-up (later):** a record of results, such as the amount raised, to help the
  group and the business decide whether to repeat it.

**Fit:** This would likely be a Tool (per ToolsArchitecture.md), plus a portal-level
directory of businesses shared across Organizations. It also gives Central Portal a
reason for businesses to know about it, which supports the "who do we serve" story.

**Already possible at launch:** a volunteer can post a Spirit Night by hand as a
PTA-tagged Coming Up event. No new work is needed to support the basic case.

A new idea gets in before launch only if it helps a parent answer one of the
unit site questions above.
