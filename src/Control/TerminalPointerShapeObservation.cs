namespace Icod.Terminal;

/// <summary>
/// Represents one explicit OSC 22 pointer-shape observation.
/// </summary>
/// <remarks>
/// <see cref="HasShape"/> is false when the terminal explicitly reports that no
/// application pointer shape is currently set. This is distinct from the semantic
/// CSS <see cref="TerminalPointerShape.Default"/> shape.
/// </remarks>
public sealed class TerminalPointerShapeObservation {
	internal TerminalPointerShapeObservation(
		TerminalPointerShape? shape
	) {
		this.Shape = shape;
	}

	/// <summary>
	/// Gets whether the terminal explicitly reported a semantic pointer shape.
	/// </summary>
	public bool HasShape {
		get {
			return this.Shape.HasValue;
		}
	}

	/// <summary>
	/// Gets the reported semantic pointer shape, or <see langword="null"/> when the
	/// terminal explicitly reports that no application pointer shape is set.
	/// </summary>
	public TerminalPointerShape? Shape {
		get;
	}
}
