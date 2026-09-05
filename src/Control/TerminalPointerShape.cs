namespace Icod.Terminal;

/// <summary>
/// Identifies one semantic terminal mouse-pointer shape supported by the 0.11 contract.
/// </summary>
public enum TerminalPointerShape {
	/// <summary>The CSS <c>alias</c> pointer shape.</summary>
	Alias,

	/// <summary>The CSS <c>cell</c> pointer shape.</summary>
	Cell,

	/// <summary>The CSS <c>copy</c> pointer shape.</summary>
	Copy,

	/// <summary>The CSS <c>crosshair</c> pointer shape.</summary>
	Crosshair,

	/// <summary>The CSS <c>default</c> pointer shape.</summary>
	Default,

	/// <summary>The CSS east-resize pointer shape.</summary>
	EastResize,

	/// <summary>The CSS east-west-resize pointer shape.</summary>
	EastWestResize,

	/// <summary>The CSS <c>grab</c> pointer shape.</summary>
	Grab,

	/// <summary>The CSS <c>grabbing</c> pointer shape.</summary>
	Grabbing,

	/// <summary>The CSS <c>help</c> pointer shape.</summary>
	Help,

	/// <summary>The CSS <c>move</c> pointer shape.</summary>
	Move,

	/// <summary>The CSS north-resize pointer shape.</summary>
	NorthResize,

	/// <summary>The CSS north-east-resize pointer shape.</summary>
	NorthEastResize,

	/// <summary>The CSS north-east/south-west-resize pointer shape.</summary>
	NorthEastSouthWestResize,

	/// <summary>The CSS <c>no-drop</c> pointer shape.</summary>
	NoDrop,

	/// <summary>The CSS <c>not-allowed</c> pointer shape.</summary>
	NotAllowed,

	/// <summary>The CSS north-south-resize pointer shape.</summary>
	NorthSouthResize,

	/// <summary>The CSS north-west-resize pointer shape.</summary>
	NorthWestResize,

	/// <summary>The CSS north-west/south-east-resize pointer shape.</summary>
	NorthWestSouthEastResize,

	/// <summary>The CSS <c>pointer</c> pointer shape.</summary>
	Pointer,

	/// <summary>The CSS <c>progress</c> pointer shape.</summary>
	Progress,

	/// <summary>The CSS south-resize pointer shape.</summary>
	SouthResize,

	/// <summary>The CSS south-east-resize pointer shape.</summary>
	SouthEastResize,

	/// <summary>The CSS south-west-resize pointer shape.</summary>
	SouthWestResize,

	/// <summary>The CSS <c>text</c> pointer shape.</summary>
	Text,

	/// <summary>The CSS <c>vertical-text</c> pointer shape.</summary>
	VerticalText,

	/// <summary>The CSS west-resize pointer shape.</summary>
	WestResize,

	/// <summary>The CSS <c>wait</c> pointer shape.</summary>
	Wait,

	/// <summary>The CSS <c>zoom-in</c> pointer shape.</summary>
	ZoomIn,

	/// <summary>The CSS <c>zoom-out</c> pointer shape.</summary>
	ZoomOut
}

/// <summary>
/// Maps the frozen pointer-shape semantic model to and from canonical OSC 22 names.
/// </summary>
internal static class TerminalPointerShapeCodec {
	internal static string GetWireName(
		TerminalPointerShape shape
	) {
		if ( !Enum.IsDefined( shape ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( shape ),
				shape,
				"The pointer shape is not defined by the frozen 0.11 contract."
			);
		}

		return shape switch {
			TerminalPointerShape.Alias => "alias",
			TerminalPointerShape.Cell => "cell",
			TerminalPointerShape.Copy => "copy",
			TerminalPointerShape.Crosshair => "crosshair",
			TerminalPointerShape.Default => "default",
			TerminalPointerShape.EastResize => "e-resize",
			TerminalPointerShape.EastWestResize => "ew-resize",
			TerminalPointerShape.Grab => "grab",
			TerminalPointerShape.Grabbing => "grabbing",
			TerminalPointerShape.Help => "help",
			TerminalPointerShape.Move => "move",
			TerminalPointerShape.NorthResize => "n-resize",
			TerminalPointerShape.NorthEastResize => "ne-resize",
			TerminalPointerShape.NorthEastSouthWestResize => "nesw-resize",
			TerminalPointerShape.NoDrop => "no-drop",
			TerminalPointerShape.NotAllowed => "not-allowed",
			TerminalPointerShape.NorthSouthResize => "ns-resize",
			TerminalPointerShape.NorthWestResize => "nw-resize",
			TerminalPointerShape.NorthWestSouthEastResize => "nwse-resize",
			TerminalPointerShape.Pointer => "pointer",
			TerminalPointerShape.Progress => "progress",
			TerminalPointerShape.SouthResize => "s-resize",
			TerminalPointerShape.SouthEastResize => "se-resize",
			TerminalPointerShape.SouthWestResize => "sw-resize",
			TerminalPointerShape.Text => "text",
			TerminalPointerShape.VerticalText => "vertical-text",
			TerminalPointerShape.WestResize => "w-resize",
			TerminalPointerShape.Wait => "wait",
			TerminalPointerShape.ZoomIn => "zoom-in",
			TerminalPointerShape.ZoomOut => "zoom-out",
			_ => throw new ArgumentOutOfRangeException(
				nameof( shape ),
				shape,
				"The pointer shape is not defined by the frozen 0.11 contract."
			)
		};
	}

	internal static TerminalPointerShape ParseWireName(
		string wireName
	) {
		ArgumentNullException.ThrowIfNull( wireName );

		return wireName switch {
			"alias" => TerminalPointerShape.Alias,
			"cell" => TerminalPointerShape.Cell,
			"copy" => TerminalPointerShape.Copy,
			"crosshair" => TerminalPointerShape.Crosshair,
			"default" => TerminalPointerShape.Default,
			"e-resize" => TerminalPointerShape.EastResize,
			"ew-resize" => TerminalPointerShape.EastWestResize,
			"grab" => TerminalPointerShape.Grab,
			"grabbing" => TerminalPointerShape.Grabbing,
			"help" => TerminalPointerShape.Help,
			"move" => TerminalPointerShape.Move,
			"n-resize" => TerminalPointerShape.NorthResize,
			"ne-resize" => TerminalPointerShape.NorthEastResize,
			"nesw-resize" => TerminalPointerShape.NorthEastSouthWestResize,
			"no-drop" => TerminalPointerShape.NoDrop,
			"not-allowed" => TerminalPointerShape.NotAllowed,
			"ns-resize" => TerminalPointerShape.NorthSouthResize,
			"nw-resize" => TerminalPointerShape.NorthWestResize,
			"nwse-resize" => TerminalPointerShape.NorthWestSouthEastResize,
			"pointer" => TerminalPointerShape.Pointer,
			"progress" => TerminalPointerShape.Progress,
			"s-resize" => TerminalPointerShape.SouthResize,
			"se-resize" => TerminalPointerShape.SouthEastResize,
			"sw-resize" => TerminalPointerShape.SouthWestResize,
			"text" => TerminalPointerShape.Text,
			"vertical-text" => TerminalPointerShape.VerticalText,
			"w-resize" => TerminalPointerShape.WestResize,
			"wait" => TerminalPointerShape.Wait,
			"zoom-in" => TerminalPointerShape.ZoomIn,
			"zoom-out" => TerminalPointerShape.ZoomOut,
			_ => throw new FormatException(
				$"OSC 22 pointer shape name '{wireName}' is not part of the frozen 0.11 semantic vocabulary."
			)
		};
	}
}
