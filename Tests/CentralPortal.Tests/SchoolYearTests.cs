using SiteEngine;

namespace CentralPortal.Tests;

public class SchoolYearTests
{
	[Theory]
	[InlineData(2026, 6, 30, "2025-2026")]
	[InlineData(2026, 7, 1, "2026-2027")]
	[InlineData(2026, 11, 19, "2026-2027")]
	[InlineData(2027, 1, 15, "2026-2027")]
	public void School_year_starts_July_1(int year, int month, int day, string expected) =>
		Assert.Equal(expected, SchoolYear.Current(new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero)));
}
