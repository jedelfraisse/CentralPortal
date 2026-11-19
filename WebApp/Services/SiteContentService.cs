using Microsoft.EntityFrameworkCore;
using SiteEngine.Data;
using SiteEngine.Entities;
using SiteEngine.Enums;

namespace WebApp.Services;

// What a unit site shows right now, already filtered for visibility and
// expiry and sorted for display. One list per parent question — see
// md/LaunchFocus.md, "Unit site questions".
public record SiteContentView(
	SiteContentItem? LatestNewsletter,
	IReadOnlyList<SiteContentItem> ActionNeeded,
	IReadOnlyList<SiteContentItem> ComingUp,
	IReadOnlyList<SiteContentItem> GetInvolved,
	IReadOnlyList<GoodToKnowGroup> GoodToKnow);

public record GoodToKnowGroup(string Name, IReadOnlyList<SiteContentItem> Items);

// Editor input. Kept separate from the entity so the editor can't set
// SiteId/audit fields, and so a save never trusts a client-supplied SiteId.
public record SiteContentEdit
{
	public Guid? Id { get; init; }                // null = new item
	public ContentKind Kind { get; init; }
	public ContentSource Source { get; init; } = ContentSource.School;
	public ContentVisibility Visibility { get; init; } = ContentVisibility.Public;
	public string Title { get; init; } = string.Empty;
	public string? Summary { get; init; }
	public string? LinkUrl { get; init; }
	public string? LinkLabel { get; init; }
	public string? Location { get; init; }
	public DateOnly? StartDate { get; init; }
	public TimeOnly? StartTime { get; init; }
	public DateOnly? EndDate { get; init; }
	public TimeOnly? EndTime { get; init; }
	public string? GroupName { get; init; }
	public int SortOrder { get; init; }
	public DateOnly? LastReviewedOn { get; init; }
}

public record SiteContentResult(bool Success, string? Error, SiteContentItem? Item = null);

// Backs unit-site content: the public/member read used by UnitHome and the
// Good to Know page, and the CRUD used by the Site Admin content editor.
// Authorization (who may edit) is the calling page's job, as with
// SiteAdminService; this service only guarantees an item is never read or
// written through a site it doesn't belong to.
public class SiteContentService(IDbContextFactory<AppDbContext> dbFactory)
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

	// Suggested Good to Know groups, from mapping a real middle school
	// newsletter (md/LaunchFocus.md). Suggestions only — GroupName is free text.
	public static readonly IReadOnlyList<string> SuggestedGroups =
	[
		"Every school day",
		"Rules & expectations",
		"Health & safety",
		"Meals & money",
		"Parent access",
		"Support & programs",
		"Contact",
	];

	public async Task<SiteContentView> GetCurrentAsync(
		Guid siteId, bool includeMembersOnly, DateOnly today, CancellationToken cancellationToken = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

		var query = db.SiteContentItems.AsNoTracking().Where(x => x.SiteId == siteId);
		if (!includeMembersOnly)
			query = query.Where(x => x.Visibility == ContentVisibility.Public);

		var items = await query.ToListAsync(cancellationToken);

		// Latest newsletter issue regardless of age — the newest issue is
		// always the right one to feature, even if the school skipped a week.
		var newsletter = items
			.Where(x => x.Kind == ContentKind.Newsletter)
			.OrderByDescending(x => x.StartDate)
			.ThenByDescending(x => x.CreatedAtUtc)
			.FirstOrDefault();

		var current = items.Where(x => !IsExpired(x, today)).ToList();

		var actionNeeded = current
			.Where(x => x.Kind == ContentKind.ActionNeeded)
			.OrderBy(x => x.EndDate ?? DateOnly.MaxValue)
			.ThenBy(x => x.SortOrder)
			.ToList();

		var comingUp = current
			.Where(x => x.Kind == ContentKind.Event)
			.OrderBy(x => x.StartDate ?? DateOnly.MaxValue)
			.ThenBy(x => x.StartTime ?? TimeOnly.MinValue)
			.ToList();

		var getInvolved = current
			.Where(x => x.Kind == ContentKind.GetInvolved)
			.OrderBy(x => x.SortOrder)
			.ThenBy(x => x.Title)
			.ToList();

		var goodToKnow = current
			.Where(x => x.Kind == ContentKind.GoodToKnow)
			.GroupBy(x => string.IsNullOrWhiteSpace(x.GroupName) ? "Other" : x.GroupName.Trim())
			.Select(g => new GoodToKnowGroup(
				g.Key,
				g.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToList()))
			.OrderBy(g => GroupRank(g.Name))
			.ThenBy(g => g.Name)
			.ToList();

		return new SiteContentView(newsletter, actionNeeded, comingUp, getInvolved, goodToKnow);
	}

	// Everything for the editor, including expired items (shown greyed so a
	// volunteer can reuse or delete them).
	public async Task<List<SiteContentItem>> ListForEditingAsync(Guid siteId, CancellationToken cancellationToken = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
		return await db.SiteContentItems.AsNoTracking()
			.Where(x => x.SiteId == siteId)
			.OrderBy(x => x.Kind)
			.ThenBy(x => x.GroupName)
			.ThenBy(x => x.SortOrder)
			.ThenByDescending(x => x.StartDate ?? x.EndDate)
			.ToListAsync(cancellationToken);
	}

	public async Task<SiteContentItem?> GetAsync(Guid siteId, Guid id, CancellationToken cancellationToken = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
		return await db.SiteContentItems.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == id && x.SiteId == siteId, cancellationToken);
	}

	public async Task<SiteContentResult> SaveAsync(
		Guid siteId, SiteContentEdit edit, string? identityUserId, CancellationToken cancellationToken = default)
	{
		var error = Validate(edit);
		if (error is not null)
			return new SiteContentResult(false, error);

		await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

		SiteContentItem item;
		if (edit.Id is Guid id)
		{
			var existing = await db.SiteContentItems
				.FirstOrDefaultAsync(x => x.Id == id && x.SiteId == siteId, cancellationToken);
			if (existing is null)
				return new SiteContentResult(false, "That item no longer exists.");
			item = existing;
		}
		else
		{
			if (!await db.Sites.AnyAsync(s => s.Id == siteId, cancellationToken))
				return new SiteContentResult(false, "Site not found.");
			item = new SiteContentItem { SiteId = siteId };
			db.SiteContentItems.Add(item);
		}

		item.Kind = edit.Kind;
		item.Source = edit.Source;
		item.Visibility = edit.Visibility;
		item.Title = edit.Title.Trim();
		item.Summary = Clean(edit.Summary);
		item.LinkUrl = Clean(edit.LinkUrl);
		item.LinkLabel = Clean(edit.LinkLabel);
		item.Location = Clean(edit.Location);
		item.StartDate = edit.StartDate;
		item.StartTime = edit.StartTime;
		item.EndDate = edit.EndDate;
		item.EndTime = edit.EndTime;
		item.GroupName = Clean(edit.GroupName);
		item.SortOrder = edit.SortOrder;
		item.LastReviewedOn = edit.LastReviewedOn;
		item.UpdatedAtUtc = DateTimeOffset.UtcNow;
		item.UpdatedByIdentityUserId = identityUserId;

		await db.SaveChangesAsync(cancellationToken);
		return new SiteContentResult(true, null, item);
	}

	public async Task<bool> DeleteAsync(Guid siteId, Guid id, CancellationToken cancellationToken = default)
	{
		await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
		var deleted = await db.SiteContentItems
			.Where(x => x.Id == id && x.SiteId == siteId)
			.ExecuteDeleteAsync(cancellationToken);
		return deleted > 0;
	}

	// An item is expired once its last relevant day has passed. Items with no
	// dates (typical for Good to Know / Get Involved) never expire.
	public static bool IsExpired(SiteContentItem item, DateOnly today)
	{
		var lastDay = item.EndDate ?? item.StartDate;
		return lastDay is DateOnly d && d < today;
	}

	public static string? Validate(SiteContentEdit edit)
	{
		if (string.IsNullOrWhiteSpace(edit.Title))
			return "Please enter a title.";
		if (edit.Title.Trim().Length > 200)
			return "Title must be 200 characters or fewer.";
		if (edit.Summary?.Length > 4000)
			return "Summary must be 4,000 characters or fewer.";
		if (edit.LinkLabel?.Length > 100 || edit.Location?.Length > 200 || edit.GroupName?.Length > 100)
			return "One of the fields is too long.";

		if (!string.IsNullOrWhiteSpace(edit.LinkUrl))
		{
			var url = edit.LinkUrl.Trim();
			if (url.Length > 1024
				|| !Uri.TryCreate(url, UriKind.Absolute, out var uri)
				|| (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
				return "The link must be a full web address starting with https://";

			// Email-tracking links are personal to whoever received the email
			// and expire — open it once, then paste the address it lands on.
			if (IsEmailTrackingHost(uri.Host))
				return "That looks like a link from an email (it's tied to your inbox). Open it, then copy the address of the page it opens and paste that instead.";
		}

		if (edit.StartDate is DateOnly s && edit.EndDate is DateOnly e && e < s)
			return "The end date can't be before the start date.";

		return edit.Kind switch
		{
			ContentKind.Newsletter when edit.StartDate is null => "Please enter the newsletter's issue date.",
			ContentKind.Newsletter when string.IsNullOrWhiteSpace(edit.LinkUrl) => "Please paste the link to the newsletter.",
			ContentKind.Event when edit.StartDate is null => "Please enter the event date.",
			ContentKind.ActionNeeded when edit.EndDate is null => "Please enter the due date.",
			_ => null,
		};
	}

	private static bool IsEmailTrackingHost(string host)
	{
		host = host.ToLowerInvariant();
		return host.StartsWith("email-link.")
			|| host.StartsWith("click.")
			|| host.Contains("safelinks.protection.outlook.com")
			|| host.EndsWith("list-manage.com");
	}

	private static int GroupRank(string name)
	{
		for (var i = 0; i < SuggestedGroups.Count; i++)
			if (string.Equals(SuggestedGroups[i], name, StringComparison.OrdinalIgnoreCase))
				return i;
		return SuggestedGroups.Count;
	}

	private static string? Clean(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// "Today" at the school, for expiry. Launch simplification: every site is
// treated as US Eastern (the first sites are in Virginia). A per-site time
// zone can replace this when a site outside Eastern time arrives.
public static class SchoolClock
{
	private static readonly TimeZoneInfo Zone = ResolveZone();

	public static DateOnly Today() =>
		DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime);

	private static TimeZoneInfo ResolveZone()
	{
		foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
		{
			try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
			catch (TimeZoneNotFoundException) { }
			catch (InvalidTimeZoneException) { }
		}
		return TimeZoneInfo.Utc;
	}
}
