namespace Icod.Terminal;

/// <summary>
/// Represents one indexed terminal-palette color.
/// </summary>
public readonly record struct TerminalPaletteColor {
	/// <summary>
	/// Initializes one indexed terminal-palette color.
	/// </summary>
	/// <param name="index">The palette index in the inclusive range 0 through 255.</param>
	/// <param name="color">The normalized terminal color.</param>
	public TerminalPaletteColor(
		byte index,
		TerminalColor color
	) {
		this.Index = index;
		this.Color = color;
	}

	/// <summary>
	/// Gets the palette index.
	/// </summary>
	public byte Index {
		get;
	}

	/// <summary>
	/// Gets the normalized palette color.
	/// </summary>
	public TerminalColor Color {
		get;
	}
}
