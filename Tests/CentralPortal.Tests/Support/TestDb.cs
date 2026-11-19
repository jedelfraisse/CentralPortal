using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SiteEngine;
using SiteEngine.Data;
using SiteEngine.Entities;
using SiteEngine.Enums;
using SiteEngine.Identity;

namespace CentralPortal.Tests.Support;

// A fresh, isolated database per test: SQLite in-memory, kept alive by one
// open connection for the TestDb's lifetime. Exposes an IDbContextFactory so
// services are constructed exactly as Program.cs builds them.
//
// SQLite is not SQL Server — it's here for fast service-logic tests, not to
// prove SQL Server-specific behavior (filtered indexes, column types). Use it
// for "does the service return the right rows", not "does the migration run".
public sealed class TestDb : IDisposable
{
	private readonly SqliteConnection _connection;

	public IDbContextFactory<AppDbContext> Factory { get; }

	public TestDb()
	{
		_connection = new SqliteConnection("DataSource=:memory:");
		_connection.Open();

		var options = new DbContextOptionsBuilder<AppDbContext>()
			.UseSqlite(_connection)
			.Options;

		Factory = new TestDbContextFactory(options);

		using var db = Factory.CreateDbContext();
		db.Database.EnsureCreated();
	}

	public AppDbContext CreateContext() => Factory.CreateDbContext();

	public Site AddSite(string name = "Test Middle School", SiteType type = SiteType.LocalUnit, Guid? parentSiteId = null)
	{
		using var db = CreateContext();
		var site = new Site
		{
			SiteName = name,
			SiteType = type,
			ParentSiteId = parentSiteId,
			// PtaId is unique; give each site its own 8-digit value.
			PtaId = Random.Shared.Next(10_000_000, 99_999_999).ToString(),
		};
		db.Sites.Add(site);
		db.SaveChanges();
		return site;
	}

	// Creates an Identity user plus its SiteUser profile; returns the Identity
	// user id (what SiteRoleResolver takes).
	public string AddUser(string email = "parent@example.com")
	{
		using var db = CreateContext();
		var identity = new ApplicationUser
		{
			UserName = email,
			NormalizedUserName = email.ToUpperInvariant(),
			Email = email,
			NormalizedEmail = email.ToUpperInvariant(),
		};
		db.Users.Add(identity);
		db.SiteUsers.Add(new SiteUser { IdentityUser = identity, DisplayName = email });
		db.SaveChanges();
		return identity.Id;
	}

	public void GrantRole(string identityUserId, Guid siteId, SiteRole role, string? schoolYear = null)
	{
		using var db = CreateContext();
		var siteUserId = db.SiteUsers.Single(u => u.IdentityUserId == identityUserId).Id;
		db.SiteUserRoles.Add(new SiteUserRole
		{
			SiteId = siteId,
			SiteUserId = siteUserId,
			Role = role,
			SchoolYear = schoolYear ?? SchoolYear.Current(),
		});
		db.SaveChanges();
	}

	public void Dispose() => _connection.Dispose();

	private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext() => new(options);
	}
}
