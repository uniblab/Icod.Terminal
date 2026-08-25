namespace Icod.Terminal;

/// <summary>
/// Maps semantic input disciplines onto the Linux and macOS POSIX termios
/// layouts captured by T03.
/// </summary>
internal static class PosixTerminalInputModeEditor {
	private const ulong InputIgnoreBreak = 0x0001UL;
	private const ulong InputBreakInterrupt = 0x0002UL;
	private const ulong InputParityMark = 0x0008UL;
	private const ulong InputParityCheck = 0x0010UL;
	private const ulong InputStrip = 0x0020UL;
	private const ulong InputMapNewLineToCarriageReturn = 0x0040UL;
	private const ulong InputIgnoreCarriageReturn = 0x0080UL;
	private const ulong InputMapCarriageReturnToNewLine = 0x0100UL;

	private const ulong OutputPostProcess = 0x0001UL;

	/// <summary>
	/// Creates a POSIX mode configured for one semantic input discipline.
	/// </summary>
	/// <param name="baseline">The captured POSIX baseline.</param>
	/// <param name="inputMode">The requested input discipline.</param>
	/// <param name="echoInput">Whether host echo is requested.</param>
	/// <returns>The configured POSIX snapshot.</returns>
	internal static TerminalModeSnapshot Configure(
		TerminalModeSnapshot baseline,
		TerminalInputMode inputMode,
		bool echoInput
	) {
		ArgumentNullException.ThrowIfNull( baseline );

		if ( TerminalPlatformKind.PosixTermios != baseline.Platform ) {
			throw new ArgumentException(
				"A POSIX input mode requires a POSIX terminal-mode snapshot.",
				nameof( baseline )
			);
		}
		if ( !Enum.IsDefined( inputMode ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( inputMode ),
				inputMode,
				"The terminal input mode is not recognized."
			);
		}

		PosixInputModeLayout layout = GetLayout( baseline.NativeFlagWidth );
		ulong inputFlags = baseline.InputFlags;
		ulong outputFlags = baseline.OutputFlags;
		ulong controlFlags = baseline.ControlFlags;
		ulong localFlags = baseline.LocalFlags;
		byte[] controlCharacters = baseline.ControlCharacters.ToArray();

		switch ( inputMode ) {
			case TerminalInputMode.Canonical:
				localFlags |= layout.Canonical | layout.Signal;
				break;

			case TerminalInputMode.CBreak:
				localFlags &= ~layout.Canonical;
				localFlags |= layout.Signal;
				SetMinimumRead(
					controlCharacters,
					layout
				);
				break;

			case TerminalInputMode.Raw:
				inputFlags &= ~GetRawInputClearMask( layout );
				outputFlags &= ~OutputPostProcess;
				controlFlags &= ~( layout.CharacterSize | layout.ParityEnable );
				controlFlags |= layout.EightBitCharacters;
				localFlags &= ~(
					layout.Canonical
					| layout.Signal
					| layout.Extended
				);
				SetMinimumRead(
					controlCharacters,
					layout
				);
				break;

			default:
				throw new ArgumentOutOfRangeException(
					nameof( inputMode ),
					inputMode,
					"The terminal input mode is not recognized."
				);
		}

		if ( echoInput ) {
			localFlags |= layout.Echo;
		} else {
			localFlags &= ~( layout.Echo | layout.EchoNewLine );
		}

		return baseline.WithPosixSerializedState(
			inputFlags,
			outputFlags,
			controlFlags,
			localFlags,
			controlCharacters
		);
	}

	private static ulong GetRawInputClearMask(
		PosixInputModeLayout layout
	) {
		return InputIgnoreBreak
			| InputBreakInterrupt
			| InputParityMark
			| InputParityCheck
			| InputStrip
			| InputMapNewLineToCarriageReturn
			| InputIgnoreCarriageReturn
			| InputMapCarriageReturnToNewLine
			| layout.SoftwareOutputFlowControl
			| layout.SoftwareInputFlowControl;
	}

	private static void SetMinimumRead(
		byte[] controlCharacters,
		PosixInputModeLayout layout
	) {
		ArgumentNullException.ThrowIfNull( controlCharacters );

		if ( ( controlCharacters.Length <= layout.MinimumIndex )
			|| ( controlCharacters.Length <= layout.TimeoutIndex ) ) {
			throw new InvalidOperationException(
				"The host terminal snapshot does not contain VMIN and VTIME control-character slots."
			);
		}

		controlCharacters[ layout.MinimumIndex ] = 1;
		controlCharacters[ layout.TimeoutIndex ] = 0;
	}

	private static PosixInputModeLayout GetLayout(
		int nativeFlagWidth
	) {
		return nativeFlagWidth switch {
			32 => new PosixInputModeLayout(
				SoftwareOutputFlowControl: 0x0400UL,
				SoftwareInputFlowControl: 0x1000UL,
				Echo: 0x0008UL,
				EchoNewLine: 0x0040UL,
				Signal: 0x0001UL,
				Canonical: 0x0002UL,
				Extended: 0x8000UL,
				CharacterSize: 0x0030UL,
				EightBitCharacters: 0x0030UL,
				ParityEnable: 0x0100UL,
				MinimumIndex: 6,
				TimeoutIndex: 5
			),
			64 => new PosixInputModeLayout(
				SoftwareOutputFlowControl: 0x0200UL,
				SoftwareInputFlowControl: 0x0400UL,
				Echo: 0x0008UL,
				EchoNewLine: 0x0010UL,
				Signal: 0x0080UL,
				Canonical: 0x0100UL,
				Extended: 0x0400UL,
				CharacterSize: 0x0300UL,
				EightBitCharacters: 0x0300UL,
				ParityEnable: 0x1000UL,
				MinimumIndex: 16,
				TimeoutIndex: 17
			),
			_ => throw new PlatformNotSupportedException(
				"The POSIX semantic input editor recognizes the Linux 32-bit and macOS 64-bit termios layouts."
			)
		};
	}

	private readonly record struct PosixInputModeLayout(
		ulong SoftwareOutputFlowControl,
		ulong SoftwareInputFlowControl,
		ulong Echo,
		ulong EchoNewLine,
		ulong Signal,
		ulong Canonical,
		ulong Extended,
		ulong CharacterSize,
		ulong EightBitCharacters,
		ulong ParityEnable,
		int MinimumIndex,
		int TimeoutIndex
	);
}
