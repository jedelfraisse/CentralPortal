namespace SiteEngine.Enums;

// Which of the two generic masthead arrangements SiteShellLayout renders for
// this site — see WebApp/Components/Layout/SiteShellLayout.razor. Kept to
// exactly two options on purpose: enough flexibility for a simple site to
// look right without turning the masthead into a free-form layout builder.
public enum HeaderLayoutStyle
{
	// Logo(s) + site name on the left, nav on the right, one row. Best for
	// most simple sites — the default.
	Inline = 0,

	// Logo(s) + site name centered on their own row, nav in a full-width
	// band below. Matches the older Division masthead shape.
	Stacked = 1
}
