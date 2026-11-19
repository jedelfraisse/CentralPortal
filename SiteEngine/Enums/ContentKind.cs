namespace SiteEngine.Enums;

// Which unit-site section a SiteContentItem appears in — each kind answers
// one parent question (see md/LaunchFocus.md, "Unit site questions"):
//   Newsletter   — "What's happening this week?"  (latest issue featured)
//   ActionNeeded — "What do I need to do?"        (deadlines, forms, money)
//   Event        — "What's coming up?"
//   GetInvolved  — "How can I help?"              (membership, volunteering)
//   GoodToKnow   — "How does this school work?"   (standing reference)
// Values are persisted as ints — append new members, never reorder.
public enum ContentKind
{
	Newsletter,
	ActionNeeded,
	Event,
	GetInvolved,
	GoodToKnow
}
