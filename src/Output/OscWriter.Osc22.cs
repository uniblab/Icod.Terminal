using System.Text;

namespace Icod.Terminal;

internal static partial class OscWriter {
	private static readonly byte[] Osc22PointerPrefix = [
		0x1b,
		(byte)']',
		(byte)'2',
		(byte)'2',
		(byte)';'
	];

	private static readonly HashSet<string> Osc22PointerShapeNames = new(
		StringComparer.Ordinal
	) {
		"alias",
		"cell",
		"copy",
		"crosshair",
		"default",
		"e-resize",
		"ew-resize",
		"grab",
		"grabbing",
		"help",
		"move",
		"n-resize",
		"ne-resize",
		"nesw-resize",
		"no-drop",
		"not-allowed",
		"ns-resize",
		"nw-resize",
		"nwse-resize",
		"pointer",
		"progress",
		"s-resize",
		"se-resize",
		"sw-resize",
		"text",
		"vertical-text",
		"w-resize",
		"wait",
		"zoom-in",
		"zoom-out"
	};

	/// <summary>
	/// Encodes one complete canonical OSC 22 pointer-shape frame using ST termination.
	/// </summary>
	/// <param name="shapeName">
	/// A canonical CSS pointer-shape name, or <see langword="null"/> to reset to terminal policy.
	/// </param>
	internal static byte[] EncodeOsc22PointerShapeFrame(
		string? shapeName
	) {
		ValidateOsc22PointerShapeName( shapeName );

		byte[] shapeBytes = shapeName is null
			? []
			: Encoding.ASCII.GetBytes( shapeName )
		;
		byte[] frame = new byte[ Osc22PointerPrefix.Length + shapeBytes.Length + 2 ];
		Osc22PointerPrefix.CopyTo(
			frame,
			0
		);
		shapeBytes.CopyTo(
			frame,
			Osc22PointerPrefix.Length
		);
		frame[ ^2 ] = 0x1b;
		frame[ ^1 ] = (byte)'\\';
		return frame;
	}

	/// <summary>
	/// Emits one complete canonical OSC 22 pointer-shape frame through one output write.
	/// </summary>
	/// <remarks>
	/// Cancellation is observed before transmission is committed. Once transmission
	/// begins, the complete OSC frame is written non-cancellably. This operation does
	/// not flush the output service.
	/// </remarks>
	internal static ValueTask WriteOsc22PointerShapeAsync(
		ITerminalOutput output,
		string? shapeName,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] frame = EncodeOsc22PointerShapeFrame( shapeName );
		cancellationToken.ThrowIfCancellationRequested();

		return output.WriteAsync(
			frame,
			CancellationToken.None
		);
	}

	private static void ValidateOsc22PointerShapeName(
		string? shapeName
	) {
		if ( shapeName is null ) {
			return;
		}
		if ( !Osc22PointerShapeNames.Contains( shapeName ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( shapeName ),
				shapeName,
				"OSC 22 pointer shape must be one of the canonical CSS-compatible names."
			);
		}
	}
}
