using CentralPortal.Tests.Support;
using SiteEngine.Enums;
using WebApp.Services;

namespace CentralPortal.Tests.Services;

public class SiteContentServiceTests : IDisposable
{
	private static readonly DateOnly Today = new(2026, 11, 19);

	private readonly TestDb _db = new();
	private readonly SiteContentService _service;
	private readonly Guid _siteId;

	public SiteContentServiceTests()
	{
		_service = new SiteContentService(_db.Factory);
		_siteId = _db.AddSite().Id;
	}

	public void Dispose() => _db.Dispose();

	private async Task<Guid> Add(SiteContentEdit edit, Guid? siteId = null)
	{
		var result = await _service.SaveAsync(siteId ?? _siteId, edit, identityUserId: null);
		Assert.True(result.Success, result.Error);
		return result.Item!.Id;
	}

	private static SiteContentEdit Event(string title, DateOnly date, ContentVisibility visibility = ContentVisibility.Public) =>
		new() { Kind = ContentKind.Event, Title = title, StartDate = date, Visibility = visibility };

	// ---- Expiry ----------------------------------------------------------

	[Fact]
	public async Task Past_events_are_hidden_and_today_and_future_are_shown()
	{
		await Add(Event("Yesterday", Today.AddDays(-1)));
		await Add(Event("Today", Today));
		await Add(Event("Next week", Today.AddDays(7)));

		var view = await _service.GetCurrentAsync(_siteId, includeMembersOnly: false, Today);

		Assert.Equal(["Today", "Next week"], view.ComingUp.Select(x => x.Title));
	}

	[Fact]
	public async Task Multi_day_event_stays_visible_until_its_end_date_passes()
	{
		await Add(Event("Art show", Today.AddDays(-10)) with { EndDate = Today.AddDays(3) });

		var view = await _service.GetCurrentAsync(_siteId, false, Today);

		Assert.Single(view.ComingUp);
	}

	[Fact]
	public async Task Good_to_know_items_without_dates_never_expire()
	{
		await Add(new SiteContentEdit { Kind = ContentKind.GoodToKnow, Title = "Dress code", GroupName = "Rules & expectations" });

		var view = await _service.GetCurrentAsync(_siteId, false, Today.AddYears(5));

		Assert.Equal("Dress code", Assert.Single(Assert.Single(view.GoodToKnow).Items).Title);
	}

	// ---- Visibility ------------------------------------------------------

	[Fact]
	public async Task Members_only_items_are_hidden_from_the_public()
	{
		await Add(Event("Open house", Today));
		await Add(Event("Board meeting", Today, ContentVisibility.Members));

		var publicView = await _service.GetCurrentAsync(_siteId, includeMembersOnly: false, Today);
		var memberView = await _service.GetCurrentAsync(_siteId, includeMembersOnly: true, Today);

		Assert.Equal(["Open house"], publicView.ComingUp.Select(x => x.Title));
		Assert.Equal(2, memberView.ComingUp.Count);
	}

	[Fact]
	public async Task Content_from_another_site_is_never_returned()
	{
		var otherSite = _db.AddSite("Other School").Id;
		await Add(Event("Other school event", Today), otherSite);

		var view = await _service.GetCurrentAsync(_siteId, true, Today);

		Assert.Empty(view.ComingUp);
	}

	// ---- Sections and ordering -------------------------------------------

	[Fact]
	public async Task Newest_newsletter_issue_is_featured_even_when_older_than_today()
	{
		await Add(new SiteContentEdit { Kind = ContentKind.Newsletter, Title = "Nov 6", StartDate = new(2026, 11, 6), LinkUrl = "https://app.smore.com/n/a" });
		await Add(new SiteContentEdit { Kind = ContentKind.Newsletter, Title = "Nov 13", StartDate = new(2026, 11, 13), LinkUrl = "https://app.smore.com/n/b" });

		var view = await _service.GetCurrentAsync(_siteId, false, Today);

		Assert.Equal("Nov 13", view.LatestNewsletter?.Title);
	}

	[Fact]
	public async Task No_newsletter_means_no_featured_issue()
	{
		var view = await _service.GetCurrentAsync(_siteId, false, Today);

		Assert.Null(view.LatestNewsletter);
	}

	[Fact]
	public async Task Action_needed_is_ordered_by_soonest_due_date()
	{
		await Add(new SiteContentEdit { Kind = ContentKind.ActionNeeded, Title = "Later", EndDate = Today.AddDays(10) });
		await Add(new SiteContentEdit { Kind = ContentKind.ActionNeeded, Title = "Sooner", EndDate = Today.AddDays(2) });

		var view = await _service.GetCurrentAsync(_siteId, false, Today);

		Assert.Equal(["Sooner", "Later"], view.ActionNeeded.Select(x => x.Title));
	}

	[Fact]
	public async Task Events_on_the_same_day_are_ordered_by_start_time()
	{
		await Add(Event("Evening", Today) with { StartTime = new(18, 0) });
		await Add(Event("Morning", Today) with { StartTime = new(8, 30) });

		var view = await _service.GetCurrentAsync(_siteId, false, Today);

		Assert.Equal(["Morning", "Evening"], view.ComingUp.Select(x => x.Title));
	}

	[Fact]
	public async Task Good_to_know_groups_follow_the_suggested_order_with_custom_groups_last()
	{
		await Add(new SiteContentEdit { Kind = ContentKind.GoodToKnow, Title = "Main office", GroupName = "Contact" });
		await Add(new SiteContentEdit { Kind = ContentKind.GoodToKnow, Title = "Bus routes", GroupName = "Zzz Custom" });
		await Add(new SiteContentEdit { Kind = ContentKind.GoodToKnow, Title = "Bell schedule", GroupName = "Every school day" });
		await Add(new SiteContentEdit { Kind = ContentKind.GoodToKnow, Title = "Ungrouped" });

		var view = await _service.GetCurrentAsync(_siteId, false, Today);

		Assert.Equal(["Every school day", "Contact", "Other", "Zzz Custom"], view.GoodToKnow.Select(g => g.Name));
	}

	// ---- Save / delete ---------------------------------------------------

	[Fact]
	public async Task Save_updates_an_existing_item_and_trims_text()
	{
		var id = await Add(Event("Paint Night", Today));

		var result = await _service.SaveAsync(_siteId, Event("  Paint Night (new date)  ", Today.AddDays(1)) with { Id = id, Summary = "   " }, "user-1");

		Assert.True(result.Success);
		var saved = await _service.GetAsync(_siteId, id);
		Assert.Equal("Paint Night (new date)", saved!.Title);
		Assert.Null(saved.Summary);
		Assert.Equal("user-1", saved.UpdatedByIdentityUserId);
	}

	[Fact]
	public async Task Save_cannot_edit_an_item_belonging_to_another_site()
	{
		var otherSite = _db.AddSite("Other School").Id;
		var id = await Add(Event("Theirs", Today), otherSite);

		var result = await _service.SaveAsync(_siteId, Event("Hijacked", Today) with { Id = id }, null);

		Assert.False(result.Success);
		Assert.Equal("Theirs", (await _service.GetAsync(otherSite, id))!.Title);
	}

	[Fact]
	public async Task Delete_only_removes_items_belonging_to_the_site()
	{
		var otherSite = _db.AddSite("Other School").Id;
		var theirs = await Add(Event("Theirs", Today), otherSite);
		var mine = await Add(Event("Mine", Today));

		Assert.False(await _service.DeleteAsync(_siteId, theirs));
		Assert.True(await _service.DeleteAsync(_siteId, mine));
		Assert.NotNull(await _service.GetAsync(otherSite, theirs));
		Assert.Null(await _service.GetAsync(_siteId, mine));
	}

	[Fact]
	public async Task Save_rejects_invalid_input_without_writing()
	{
		var result = await _service.SaveAsync(_siteId, new SiteContentEdit { Kind = ContentKind.Event, Title = "" }, null);

		Assert.False(result.Success);
		Assert.Empty(await _service.ListForEditingAsync(_siteId));
	}

	[Fact]
	public async Task Editor_list_includes_expired_items()
	{
		await Add(Event("Long ago", Today.AddYears(-1)));

		Assert.Single(await _service.ListForEditingAsync(_siteId));
	}
}
