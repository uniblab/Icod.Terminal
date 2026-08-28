namespace Icod.Terminal;

/// <summary>
/// Identifies one fixed control-function setting which can be requested through
/// DEC Request Status String (DECRQSS).
/// </summary>
public enum TerminalStatusStringKind {
	/// <summary>Select Graphic Rendition (SGR), identifier <c>m</c>.</summary>
	SelectGraphicRendition,

	/// <summary>Set Conformance Level (DECSCL), identifier <c>"p</c>.</summary>
	ConformanceLevel,

	/// <summary>Set Cursor Style (DECSCUSR), identifier <c>SP q</c>.</summary>
	CursorStyle,

	/// <summary>Set Character Attribute (DECSCA), identifier <c>"q</c>.</summary>
	CharacterProtection,

	/// <summary>Set Top and Bottom Margins (DECSTBM), identifier <c>r</c>.</summary>
	ScrollingRegion,

	/// <summary>Set Left and Right Margins (DECSLRM), identifier <c>s</c>.</summary>
	LeftRightMargins,

	/// <summary>Set Lines Per Page (DECSLPP), identifier <c>t</c>.</summary>
	LinesPerPage,

	/// <summary>Set Columns Per Page (DECSCPP), identifier <c>$|</c>.</summary>
	ColumnsPerPage,

	/// <summary>Select Active Status Display (DECSASD), identifier <c>$}</c>.</summary>
	ActiveStatusDisplay,

	/// <summary>Set Status Line Type (DECSSDT), identifier <c>$~</c>.</summary>
	StatusLineType,

	/// <summary>Select Attribute Change Extent (DECSACE), identifier <c>*x</c>.</summary>
	AttributeChangeExtent,

	/// <summary>Set Number of Lines Per Screen (DECSNLS), identifier <c>*|</c>.</summary>
	LinesPerScreen
}

/// <summary>
/// Represents one DEC Report Status String (DECRPSS) response.
/// </summary>
public sealed class TerminalStatusStringResponse {
	internal TerminalStatusStringResponse(
		TerminalStatusStringKind kind,
		bool isSupported,
		string? statusString
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		if ( isSupported && string.IsNullOrEmpty( statusString ) ) {
			throw new ArgumentException(
				"A supported status-string response must contain returned status data.",
				nameof( statusString )
			);
		}
		if ( !isSupported && statusString is not null ) {
			throw new ArgumentException(
				"An unsupported status-string response cannot contain returned status data.",
				nameof( statusString )
			);
		}

		this.Kind = kind;
		this.IsSupported = isSupported;
		this.StatusString = statusString;
	}

	/// <summary>
	/// Gets the control-function setting which was requested.
	/// </summary>
	public TerminalStatusStringKind Kind {
		get;
	}

	/// <summary>
	/// Gets whether the terminal accepted the DECRQSS request.
	/// </summary>
	public bool IsSupported {
		get;
	}

	/// <summary>
	/// Gets the returned control-function status string for a supported request,
	/// excluding the DCS/DECRPSS wrapper, or <see langword="null"/> when the
	/// terminal reports that the request is unsupported.
	/// </summary>
	public string? StatusString {
		get;
	}
}
