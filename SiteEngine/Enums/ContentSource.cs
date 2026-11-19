namespace SiteEngine.Enums;

// Who an item comes from. Unit sites show one feed of school life rather than
// separate PTA/school sections — parents care what and when, not who — so
// the source is a tag on the item, not a separate list. Persisted as ints —
// append new members, never reorder.
public enum ContentSource
{
	School,
	PTA,
	District,
	Community
}
