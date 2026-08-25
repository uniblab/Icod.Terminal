namespace Icod.Terminal;

/// <summary>
/// Identifies the semantic input discipline requested from a terminal.
/// </summary>
public enum TerminalInputMode {
	/// <summary>
	/// Use canonical, line-oriented input with host signal/processed-input handling enabled.
	/// </summary>
	Canonical,

	/// <summary>
	/// Disable canonical line buffering while retaining host signal/processed-input handling.
	/// </summary>
	CBreak,

	/// <summary>
	/// Disable canonical buffering, host signal handling, and ordinary raw-mode translations.
	/// </summary>
	Raw
}

/// <summary>
/// Applies platform-neutral input-discipline and echo policy to complete native
/// terminal-mode snapshots.
/// </summary>
/// <remarks>
/// This policy is relative to a captured baseline. It changes only the native
/// fields required by the requested semantic mode and preserves unrelated state.
/// </remarks>
public static class TerminalInputModePolicy {
	/// <summary>
	/// Creates a configured terminal-mode snapshot from a captured baseline.
	/// </summary>
	/// <param name="baseline">The complete baseline terminal mode.</param>
	/// <param name="inputMode">The requested semantic input discipline.</param>
	/// <param name="echoInput">Whether host input echo is requested.</param>
	/// <returns>A configured terminal-mode snapshot.</returns>
	public static TerminalModeSnapshot Configure(
		TerminalModeSnapshot baseline,
		TerminalInputMode inputMode,
		bool echoInput
	) {
		ArgumentNullException.ThrowIfNull( baseline );
		ValidateInputMode( inputMode );

		return baseline.Platform switch {
			TerminalPlatformKind.PosixTermios =>
				PosixTerminalInputModeEditor.Configure(
					baseline,
					inputMode,
					echoInput
				),
			TerminalPlatformKind.WindowsConsole =>
				WindowsTerminalInputModeEditor.Configure(
					baseline,
					inputMode,
					echoInput
				),
			_ => throw new ArgumentOutOfRangeException(
				nameof( baseline ),
				baseline.Platform,
				"The terminal platform is not recognized."
			)
		};
	}

	/// <summary>
	/// Configures and applies a semantic input mode through a low-level terminal provider.
	/// </summary>
	/// <param name="provider">The terminal-control provider.</param>
	/// <param name="endpoint">The input endpoint to mutate.</param>
	/// <param name="baseline">The complete baseline terminal mode.</param>
	/// <param name="inputMode">The requested semantic input discipline.</param>
	/// <param name="echoInput">Whether host input echo is requested.</param>
	/// <returns>The controlled terminal mutation result.</returns>
	/// <remarks>
	/// POSIX mode changes are applied after pending output drains. Windows console
	/// mode changes are applied immediately because Windows does not expose POSIX
	/// drain-before-apply semantics.
	/// </remarks>
	public static TerminalControlMutationResult Apply(
		ITerminalControlProvider provider,
		TerminalEndpoint endpoint,
		TerminalModeSnapshot baseline,
		TerminalInputMode inputMode,
		bool echoInput
	) {
		ArgumentNullException.ThrowIfNull( provider );
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( baseline );
		ValidateInputMode( inputMode );

		TerminalModeSnapshot configured = Configure(
			baseline,
			inputMode,
			echoInput
		);
		TerminalModeApplyTiming timing = configured.Platform switch {
			TerminalPlatformKind.PosixTermios =>
				TerminalModeApplyTiming.AfterOutputDrained,
			TerminalPlatformKind.WindowsConsole =>
				TerminalModeApplyTiming.Immediately,
			_ => throw new ArgumentOutOfRangeException(
				nameof( baseline ),
				configured.Platform,
				"The terminal platform is not recognized."
			)
		};

		return provider.SetMode(
			endpoint,
			configured,
			timing
		);
	}

	private static void ValidateInputMode(
		TerminalInputMode inputMode
	) {
		if ( !Enum.IsDefined( inputMode ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( inputMode ),
				inputMode,
				"The terminal input mode is not recognized."
			);
		}
	}
}
