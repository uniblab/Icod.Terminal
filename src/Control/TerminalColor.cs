namespace Icod.Terminal;

/// <summary>
/// Represents one normalized terminal RGB color using 16-bit channels.
/// </summary>
public readonly struct TerminalColor : IEquatable<TerminalColor> {
	/// <summary>
	/// Initializes a terminal color from normalized 16-bit RGB channels.
	/// </summary>
	/// <param name="red">The red channel.</param>
	/// <param name="green">The green channel.</param>
	/// <param name="blue">The blue channel.</param>
	public TerminalColor(
		ushort red,
		ushort green,
		ushort blue
	) {
		this.Red = red;
		this.Green = green;
		this.Blue = blue;
	}

	/// <summary>
	/// Gets the normalized 16-bit red channel.
	/// </summary>
	public ushort Red {
		get;
	}

	/// <summary>
	/// Gets the normalized 16-bit green channel.
	/// </summary>
	public ushort Green {
		get;
	}

	/// <summary>
	/// Gets the normalized 16-bit blue channel.
	/// </summary>
	public ushort Blue {
		get;
	}

	/// <summary>
	/// Creates a terminal color from conventional 8-bit RGB channels.
	/// </summary>
	/// <param name="red">The red channel.</param>
	/// <param name="green">The green channel.</param>
	/// <param name="blue">The blue channel.</param>
	/// <returns>The corresponding normalized 16-bit terminal color.</returns>
	public static TerminalColor FromRgb8(
		byte red,
		byte green,
		byte blue
	) {
		return new TerminalColor(
			ExpandByte( red ),
			ExpandByte( green ),
			ExpandByte( blue )
		);
	}

	/// <inheritdoc />
	public bool Equals(
		TerminalColor other
	) {
		return ( this.Red == other.Red )
			&& ( this.Green == other.Green )
			&& ( this.Blue == other.Blue );
	}

	/// <inheritdoc />
	public override bool Equals(
		object? obj
	) {
		return ( obj is TerminalColor other )
			&& this.Equals( other );
	}

	/// <inheritdoc />
	public override int GetHashCode() {
		return HashCode.Combine(
			this.Red,
			this.Green,
			this.Blue
		);
	}

	/// <summary>
	/// Determines whether two terminal colors have equal normalized channels.
	/// </summary>
	public static bool operator ==(
		TerminalColor left,
		TerminalColor right
	) {
		return left.Equals( right );
	}

	/// <summary>
	/// Determines whether two terminal colors have different normalized channels.
	/// </summary>
	public static bool operator !=(
		TerminalColor left,
		TerminalColor right
	) {
		return !left.Equals( right );
	}

	private static ushort ExpandByte(
		byte value
	) {
		return (ushort)( value * 257 );
	}
}
