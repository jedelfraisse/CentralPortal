using SiteEngine.Enums;
using WebApp.Services;

namespace CentralPortal.Tests.Services;

// Pure validation rules — no database needed.
public class SiteContentValidationTests
{
	private static SiteContentEdit Valid(ContentKind kind = ContentKind.GoodToKnow) => new() { Kind = kind, Title = "Dress code" };

	[Fact]
	public void A_minimal_good_to_know_item_is_valid() =>
		Assert.Null(SiteContentService.Validate(Valid()));

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Title_is_required(string title) =>
		Assert.NotNull(SiteContentService.Validate(Valid() with { Title = title }));

	[Fact]
	public void Title_over_200_characters_is_rejected() =>
		Assert.NotNull(SiteContentService.Validate(Valid() with { Title = new string('x', 201) }));

	[Theory]
	[InlineData("https://app.smore.com/n/w5em3")]
	[InlineData("http://school.example.org/dress-code.pdf")]
	public void Clean_web_links_are_accepted(string url) =>
		Assert.Null(SiteContentService.Validate(Valid() with { LinkUrl = url }));

	[Theory]
	[InlineData("app.smore.com/n/w5em3")]          // no scheme
	[InlineData("javascript:alert(1)")]
	[InlineData("ftp://files.example.org/a.pdf")]
	[InlineData("not a url")]
	public void Non_web_links_are_rejected(string url) =>
		Assert.NotNull(SiteContentService.Validate(Valid() with { LinkUrl = url }));

	[Theory]
	[InlineData("https://email-link.parentsquare.com/ls/click?upn=abc")]
	[InlineData("https://nam12.safelinks.protection.outlook.com/?url=x")]
	[InlineData("https://school.us1.list-manage.com/track/click?u=1")]
	public void Email_tracking_links_are_rejected_with_a_helpful_message(string url)
	{
		var error = SiteContentService.Validate(Valid() with { LinkUrl = url });

		Assert.NotNull(error);
		Assert.Contains("email", error, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void End_date_before_start_date_is_rejected() =>
		Assert.NotNull(SiteContentService.Validate(Valid(ContentKind.Event) with { StartDate = new(2026, 11, 20), EndDate = new(2026, 11, 19) }));

	[Fact]
	public void Event_requires_a_date() =>
		Assert.NotNull(SiteContentService.Validate(Valid(ContentKind.Event)));

	[Fact]
	public void Action_needed_requires_a_due_date() =>
		Assert.NotNull(SiteContentService.Validate(Valid(ContentKind.ActionNeeded) with { StartDate = new(2026, 11, 19) }));

	[Fact]
	public void Newsletter_requires_issue_date_and_link()
	{
		var dated = Valid(ContentKind.Newsletter) with { StartDate = new(2026, 11, 13) };

		Assert.NotNull(SiteContentService.Validate(Valid(ContentKind.Newsletter) with { LinkUrl = "https://app.smore.com/n/x" }));
		Assert.NotNull(SiteContentService.Validate(dated));
		Assert.Null(SiteContentService.Validate(dated with { LinkUrl = "https://app.smore.com/n/x" }));
	}
}
