using CentralPortal.Tests.Support;
using SiteEngine.Enums;
using WebApp.Services;

namespace CentralPortal.Tests.Services;

// Role resolution decides who sees members-only content and who can edit a
// site, so its rules are pinned down here.
public class SiteRoleResolverTests : IDisposable
{
	private readonly TestDb _db = new();
	private readonly SiteRoleResolver _resolver;

	public SiteRoleResolverTests() => _resolver = new SiteRoleResolver(_db.Factory);

	public void Dispose() => _db.Dispose();

	[Fact]
	public async Task User_with_no_roles_is_a_viewer()
	{
		var unit = _db.AddSite();
		var user = _db.AddUser();

		Assert.Equal(SiteRole.Viewer, await _resolver.ResolveAsync(user, unit.Id));
	}

	[Fact]
	public async Task Unknown_user_is_a_viewer()
	{
		var unit = _db.AddSite();

		Assert.Equal(SiteRole.Viewer, await _resolver.ResolveAsync("no-such-user", unit.Id));
	}

	[Fact]
	public async Task Highest_role_on_the_site_wins()
	{
		var unit = _db.AddSite();
		var user = _db.AddUser();
		_db.GrantRole(user, unit.Id, SiteRole.Member);
		_db.GrantRole(user, unit.Id, SiteRole.Officer);

		Assert.Equal(SiteRole.Officer, await _resolver.ResolveAsync(user, unit.Id));
	}

	[Fact]
	public async Task Role_on_one_unit_does_not_apply_to_another()
	{
		var unitA = _db.AddSite("A");
		var unitB = _db.AddSite("B");
		var user = _db.AddUser();
		_db.GrantRole(user, unitA.Id, SiteRole.SiteAdmin);

		Assert.Equal(SiteRole.Viewer, await _resolver.ResolveAsync(user, unitB.Id));
	}

	[Fact]
	public async Task Division_admin_on_the_parent_division_applies_to_its_units()
	{
		var division = _db.AddSite("Division", SiteType.Division);
		var unit = _db.AddSite("Unit", parentSiteId: division.Id);
		var user = _db.AddUser();
		_db.GrantRole(user, division.Id, SiteRole.DivisionAdmin);

		Assert.Equal(SiteRole.DivisionAdmin, await _resolver.ResolveAsync(user, unit.Id));
	}

	[Theory]
	[InlineData(SiteRole.Member)]
	[InlineData(SiteRole.Officer)]
	[InlineData(SiteRole.SiteAdmin)]
	public async Task Lower_roles_on_the_parent_division_do_not_cascade(SiteRole role)
	{
		var division = _db.AddSite("Division", SiteType.Division);
		var unit = _db.AddSite("Unit", parentSiteId: division.Id);
		var user = _db.AddUser();
		_db.GrantRole(user, division.Id, role);

		Assert.Equal(SiteRole.Viewer, await _resolver.ResolveAsync(user, unit.Id));
	}

	// Deliberate (md/LaunchFocus.md, "Membership expiry"): roles do NOT lapse
	// at a school-year boundary, because not every group runs on a school year.
	// Expiry will become a per-Organization-Type option driven by its
	// OperationalCycles. If this test fails, that change should be intentional.
	[Fact]
	public async Task Roles_from_a_previous_school_year_still_count()
	{
		var unit = _db.AddSite();
		var user = _db.AddUser();
		_db.GrantRole(user, unit.Id, SiteRole.Member, schoolYear: "2020-2021");

		Assert.Equal(SiteRole.Member, await _resolver.ResolveAsync(user, unit.Id));
	}

	[Fact]
	public void HighestRole_combines_portal_and_membership_roles() =>
		Assert.Equal(SiteRole.Officer, SiteRoleResolver.HighestRole(SiteRole.Viewer, [SiteRole.Member, null, SiteRole.Officer]));
}
