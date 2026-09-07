namespace Icod.Terminal;

/// <summary>
/// Identifies the semantic keyboard-reporting intensity requested from a modern terminal protocol.
/// </summary>
public enum TerminalKeyboardReportingMode {
	/// <summary>
	/// Disambiguates keyboard escape/control sequences while preserving ordinary direct text delivery.
	/// </summary>
	Disambiguated = 0,

	/// <summary>
	/// Adds press/repeat/release information where the negotiated protocol reports it without forcing all
	/// text-producing keys into key-event escape sequences.
	/// </summary>
	EventTypes = 1,

	/// <summary>
	/// Requests uniform key-event reporting, including text-producing and modifier keys, with associated
	/// text retained when the negotiated protocol provides it.
	/// </summary>
	AllKeys = 2
}
