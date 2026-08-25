namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Icod.TermInfo;

using Xunit;

/// <summary>
/// Verifies that native providers convert host and redirection state into
/// controlled results without mutating the active terminal during ordinary
/// test execution.
/// </summary>
public sealed class SystemTerminalControlProviderTests {
	/// <summary>
	/// Verifies that standard-input attachment inspection always returns one of
	/// the public controlled states.
	/// </summary>
	[Fact]
	public void StandardInputObservationIsControlled() {
		TerminalControlResult<TerminalEndpointObservation> result =
			SystemTerminalControlProvider.Instance.Observe(
				TerminalEndpoint.StandardInput
			);

		AssertControlledStatus( result.Status );

		if ( result.IsAvailable
			&& result.GetRequiredValue().IsTerminal ) {
			Assert.True(
				result.GetRequiredValue().Capabilities.HasFlag(
					TerminalControlCapabilities.Attachment
				)
			);
		}
	}

	/// <summary>
	/// Verifies that a regular file is observed as a nonterminal on the
	/// supported desktop hosts.
	/// </summary>
	[Fact]
	public void RegularFileIsNotATerminal() {
		string path = System.IO.Path.GetTempFileName();

		try {
			TerminalControlResult<TerminalEndpointObservation> result =
				SystemTerminalControlProvider.Instance.Observe(
					TerminalEndpoint.ForPath( path )
				);

			if ( OperatingSystem.IsLinux()
				|| OperatingSystem.IsMacOS()
				|| OperatingSystem.IsWindows() ) {
				Assert.True( result.IsAvailable );
				Assert.False(
					result.GetRequiredValue().IsTerminal
				);
			} else {
				Assert.Equal(
					TerminalControlStatus.Unsupported,
					result.Status
				);
			}
		} finally {
			File.Delete( path );
		}
	}

	/// <summary>
	/// Verifies that mode retrieval for active standard input is controlled and
	/// available snapshots identify the current native model.
	/// </summary>
	[Fact]
	public void StandardInputModeRetrievalIsControlled() {
		TerminalControlResult<TerminalModeSnapshot> result =
			SystemTerminalControlProvider.Instance.GetMode(
				TerminalEndpoint.StandardInput
			);

		AssertControlledStatus( result.Status );

		if ( result.IsAvailable ) {
			TerminalModeSnapshot mode =
				result.GetRequiredValue();

			if ( OperatingSystem.IsWindows() ) {
				Assert.Equal(
					TerminalPlatformKind.WindowsConsole,
					mode.Platform
				);
				Assert.Empty(
					mode.ControlCharacters
				);
				Assert.Null(
					mode.InputSpeed
				);
				Assert.Null(
					mode.OutputSpeed
				);
			} else {
				Assert.Equal(
					TerminalPlatformKind.PosixTermios,
					mode.Platform
				);
				Assert.NotEmpty(
					mode.ControlCharacters
				);
				Assert.NotNull(
					mode.InputSpeed
				);
				Assert.NotNull(
					mode.OutputSpeed
				);
			}
		}
	}

	/// <summary>
	/// Verifies that live size observation for standard output is controlled and
	/// available results are positive.
	/// </summary>
	[Fact]
	public void StandardOutputSizeObservationIsControlled() {
		TerminalControlResult<TerminalSize> result =
			SystemTerminalControlProvider.Instance.GetSize(
				TerminalEndpoint.StandardOutput
			);

		AssertControlledStatus( result.Status );

		if ( result.IsAvailable ) {
			TerminalSize size = result.GetRequiredValue();

			Assert.True( 0 < size.Columns );
			Assert.True( 0 < size.Rows );
		}
	}

	/// <summary>
	/// Verifies that a regular file does not fabricate live terminal dimensions.
	/// </summary>
	[Fact]
	public void RegularFileSizeIsUnavailable() {
		string path = System.IO.Path.GetTempFileName();

		try {
			TerminalControlResult<TerminalSize> result =
				SystemTerminalControlProvider.Instance.GetSize(
					TerminalEndpoint.ForPath( path )
				);

			if ( OperatingSystem.IsLinux()
				|| OperatingSystem.IsMacOS()
				|| OperatingSystem.IsWindows() ) {
				Assert.Equal(
					TerminalControlStatus.Unavailable,
					result.Status
				);
			} else {
				Assert.Equal(
					TerminalControlStatus.Unsupported,
					result.Status
				);
			}
		} finally {
			File.Delete( path );
		}
	}

	/// <summary>
	/// Exercises native no-op mode application and exact capture/restoration only
	/// when live terminal mutation has been explicitly enabled by the developer.
	/// </summary>
	[Fact]
	public void LiveModeRoundTripIsExactWhenExplicitlyEnabled() {
		if ( !"1".Equals(
			Environment.GetEnvironmentVariable(
				"ICOD_TERMINAL_RUN_LIVE_TESTS"
			),
			StringComparison.Ordinal
		) ) {
			return;
		}

		ITerminalControlProvider provider =
			SystemTerminalControlProvider.Instance;
		TerminalEndpoint endpoint =
			TerminalEndpoint.StandardInput;
		TerminalControlResult<TerminalEndpointObservation> observation =
			provider.Observe( endpoint );

		Assert.True(
			observation.IsAvailable,
			observation.Message
		);
		Assert.True(
			observation.GetRequiredValue().IsTerminal,
			"ICOD_TERMINAL_RUN_LIVE_TESTS requires interactive standard input."
		);

		TerminalControlResult<TerminalModeSnapshot> capture =
			provider.GetMode( endpoint );

		Assert.True(
			capture.IsAvailable,
			capture.Message
		);

		TerminalModeSnapshot baseline =
			capture.GetRequiredValue();
		string expected =
			TerminalModeCodec.Serialize( baseline );
		TerminalModeApplyTiming timing =
			TerminalPlatformKind.WindowsConsole
				== baseline.Platform
				? TerminalModeApplyTiming.Immediately
				: TerminalModeApplyTiming.AfterOutputDrained;

		try {
			TerminalControlMutationResult apply =
				provider.SetMode(
					endpoint,
					baseline,
					timing
				);

			Assert.True(
				apply.Succeeded,
				apply.Message
			);

			TerminalControlResult<TerminalModeSnapshot> recapture =
				provider.GetMode( endpoint );

			Assert.True(
				recapture.IsAvailable,
				recapture.Message
			);
			Assert.Equal(
				expected,
				TerminalModeCodec.Serialize(
					recapture.GetRequiredValue()
				)
			);
		} finally {
			TerminalControlMutationResult restore =
				provider.SetMode(
					endpoint,
					baseline,
					timing
				);

			Assert.True(
				restore.Succeeded,
				restore.Message
			);
		}
	}

	private static void AssertControlledStatus(
		TerminalControlStatus status
	) {
		Assert.Contains(
			status,
			new[] {
				TerminalControlStatus.Available,
				TerminalControlStatus.Unavailable,
				TerminalControlStatus.Unsupported,
				TerminalControlStatus.Failed
			}
		);
	}
}