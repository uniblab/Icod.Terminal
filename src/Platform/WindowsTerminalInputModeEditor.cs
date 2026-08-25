namespace Icod.Terminal;

/// <summary>
/// Maps semantic input disciplines onto Windows console input-mode flags.
/// </summary>
internal static class WindowsTerminalInputModeEditor {
	private const uint EnableProcessedInput = 0x0001U;
	private const uint EnableLineInput = 0x0002U;
	private const uint EnableEchoInput = 0x0004U;
	private const uint EnableVirtualTerminalInput = 0x0200U;

	/// <summary>
	/// Creates a Windows console-input mode for one semantic input discipline.
	/// </summary>
	/// <param name="baseline">The captured Windows console-input baseline.</param>
	/// <param name="inputMode">The requested input discipline.</param>
	/// <param name="echoInput">Whether host echo is requested.</param>
	/// <returns>The configured Windows console-input snapshot.</returns>
	internal static TerminalModeSnapshot Configure(
		TerminalModeSnapshot baseline,
		TerminalInputMode inputMode,
		bool echoInput
	) {
		ArgumentNullException.ThrowIfNull( baseline );

		if ( TerminalPlatformKind.WindowsConsole != baseline.Platform ) {
			throw new ArgumentException(
				"A Windows input mode requires a Windows console-mode snapshot.",
				nameof( baseline )
			);
		}
		if ( TerminalConsoleDirection.Input != baseline.ConsoleDirection ) {
			throw new InvalidOperationException(
				"A semantic input mode requires a Windows console input snapshot."
			);
		}
		if ( !Enum.IsDefined( inputMode ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( inputMode ),
				inputMode,
				"The terminal input mode is not recognized."
			);
		}

		uint mode = baseline.ConsoleMode!.Value;

		switch ( inputMode ) {
			case TerminalInputMode.Canonical:
				mode |= EnableProcessedInput | EnableLineInput;
				break;

			case TerminalInputMode.CBreak:
				mode |= EnableProcessedInput | EnableVirtualTerminalInput;
				mode &= ~EnableLineInput;
				break;

			case TerminalInputMode.Raw:
				mode |= EnableVirtualTerminalInput;
				mode &= ~( EnableProcessedInput | EnableLineInput );
				break;

			default:
				throw new ArgumentOutOfRangeException(
					nameof( inputMode ),
					inputMode,
					"The terminal input mode is not recognized."
				);
		}

		if ( echoInput ) {
			mode |= EnableEchoInput;
		} else {
			mode &= ~EnableEchoInput;
		}

		return TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			mode
		);
	}
}
