namespace WebApp.Authentication;

// Thrown when a sign-in code was generated and stored successfully but the
// email carrying it couldn't be sent (broken SMTP config, unreachable host,
// etc.). Carries the code itself so a localhost caller — the only context
// where exposing it is safe — can still complete sign-in without a working
// mail server, e.g. while fixing SMTP settings that themselves live behind
// a login wall.
public class EmailSendFailedException(string code, Exception inner) : Exception(inner.Message, inner)
{
	public string Code { get; } = code;
}
