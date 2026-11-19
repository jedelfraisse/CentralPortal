using SiteEngine.Enums;

namespace SiteEngine.Entities;

// One piece of unit-site content: a newsletter issue, an event, a deadline,
// a "get involved" ask, or a standing Good to Know entry. Deliberately one
// table for every kind (see md/LaunchFocus.md, "Launch scope") so a single
// volunteer edits everything in one place.
//
// Dates are school-local calendar dates (DateOnly/TimeOnly), not instants:
// "Paint Night, Nov 19, 6 PM" means that wall-clock time at the school.
// Expiry: an item with a date stops showing after (EndDate ?? StartDate) has
// passed. GoodToKnow items normally carry no dates and never expire — they
// use LastReviewedOn instead so stale entries can be spotted each August.
//
// LinkUrl should be the clean destination (e.g. the public Smore newsletter),
// never a personal email-tracking link such as ParentSquare's click URLs.
public class SiteContentItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid SiteId { get; set; }
	public Site Site { get; set; } = null!;

	public ContentKind Kind { get; set; }
	public ContentSource Source { get; set; } = ContentSource.School;
	public ContentVisibility Visibility { get; set; } = ContentVisibility.Public;

	public string Title { get; set; } = string.Empty;
	public string? Summary { get; set; }

	public string? LinkUrl { get; set; }
	public string? LinkLabel { get; set; }   // e.g. "Read the newsletter", "Sign-up form"
	public string? Location { get; set; }    // events

	// Event: when it happens (EndDate for multi-day, e.g. an art show).
	// ActionNeeded: EndDate is the due date. Newsletter: StartDate is the issue date.
	public DateOnly? StartDate { get; set; }
	public TimeOnly? StartTime { get; set; }
	public DateOnly? EndDate { get; set; }
	public TimeOnly? EndTime { get; set; }

	// GoodToKnow grouping ("Every school day", "Rules & expectations", ...).
	// Free text rather than an enum so each school can name its own groups.
	public string? GroupName { get; set; }
	public int SortOrder { get; set; }
	public DateOnly? LastReviewedOn { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
	public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
	public string? UpdatedByIdentityUserId { get; set; }
}
