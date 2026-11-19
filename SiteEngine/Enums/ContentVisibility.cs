namespace SiteEngine.Enums;

// Public items show to everyone, including anonymous visitors. Members items
// show only to users holding Member (or higher) on the site — directory,
// minutes, finances, board contacts. Persisted as ints.
public enum ContentVisibility
{
	Public,
	Members
}
