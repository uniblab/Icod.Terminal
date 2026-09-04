namespace Icod.Terminal;

/// <summary>
/// Internal application-text encoding helpers shared by session-owned output components.
/// </summary>
public sealed partial class TerminalSession {
	internal byte[] EncodeApplicationText(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		return this.applicationEncoding.GetBytes( value );
	}
}
