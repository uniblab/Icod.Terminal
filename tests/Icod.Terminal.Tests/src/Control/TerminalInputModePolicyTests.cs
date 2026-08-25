namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies platform-neutral semantic input-mode transformations without
/// mutating the process terminal.
/// </summary>
public sealed class TerminalInputModePolicyTests {
	private const ulong LinuxInputRawMask =
		0x0001UL
		| 0x0002UL
		| 0x0008UL
		| 0x0010UL
		| 0x0020UL
		| 0x0040UL
		| 0x0080UL
		| 0x0100UL
		| 0x0400UL
		| 0x1000UL;

	/// <summary>
	/// Verifies that canonical POSIX input enables line and signal processing
	/// while preserving unrelated baseline state and applying noecho.
	/// </summary>
	[Fact]
	public void ConfiguresLinuxCanonicalInputRelativeToBaseline() {
		const ulong unrelatedLocal = 0x40000000UL;
		TerminalModeSnapshot baseline = CreateLinuxMode(
			inputFlags: 0x80000000UL,
			outputFlags: 0x20000000UL,
			controlFlags: 0x10000000UL,
			localFlags: unrelatedLocal | 0x0008UL | 0x0040UL
		);

		TerminalModeSnapshot configured = TerminalInputModePolicy.Configure(
			baseline,
			TerminalInputMode.Canonical,
			echoInput: false
		);

		Assert.Equal( baseline.InputFlags, configured.InputFlags );
		Assert.Equal( baseline.OutputFlags, configured.OutputFlags );
		Assert.Equal( baseline.ControlFlags, configured.ControlFlags );
		Assert.NotEqual( 0UL, configured.LocalFlags & 0x0001UL );
		Assert.NotEqual( 0UL, configured.LocalFlags & 0x0002UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0008UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0040UL );
		Assert.NotEqual( 0UL, configured.LocalFlags & unrelatedLocal );
	}

	/// <summary>
	/// Verifies that cbreak disables canonical buffering, retains signals, and
	/// requests one-byte noncanonical reads.
	/// </summary>
	[Fact]
	public void ConfiguresLinuxCbreakInput() {
		byte[] characters = Enumerable.Repeat( (byte)0x5a, 32 ).ToArray();
		TerminalModeSnapshot baseline = CreateLinuxMode(
			inputFlags: 0x1234UL,
			outputFlags: 0x2345UL,
			controlFlags: 0x3456UL,
			localFlags: 0x0002UL,
			controlCharacters: characters
		);

		TerminalModeSnapshot configured = TerminalInputModePolicy.Configure(
			baseline,
			TerminalInputMode.CBreak,
			echoInput: false
		);

		Assert.Equal( baseline.InputFlags, configured.InputFlags );
		Assert.Equal( baseline.OutputFlags, configured.OutputFlags );
		Assert.Equal( baseline.ControlFlags, configured.ControlFlags );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0002UL );
		Assert.NotEqual( 0UL, configured.LocalFlags & 0x0001UL );
		Assert.Equal( (byte)1, configured.ControlCharacters[ 6 ] );
		Assert.Equal( (byte)0, configured.ControlCharacters[ 5 ] );
		Assert.Equal( (byte)0x5a, configured.ControlCharacters[ 7 ] );
	}

	/// <summary>
	/// Verifies the Linux raw transformation, including eight-bit character
	/// mode, disabled processing, and preservation of unrelated bits.
	/// </summary>
	[Fact]
	public void ConfiguresLinuxRawInput() {
		const ulong unrelatedInput = 0x80000000UL;
		const ulong unrelatedOutput = 0x40000000UL;
		const ulong unrelatedControl = 0x20000000UL;
		const ulong unrelatedLocal = 0x10000000UL;
		TerminalModeSnapshot baseline = CreateLinuxMode(
			inputFlags: LinuxInputRawMask | unrelatedInput,
			outputFlags: 0x0001UL | unrelatedOutput,
			controlFlags: 0x0020UL | 0x0100UL | unrelatedControl,
			localFlags: 0x0001UL | 0x0002UL | 0x0008UL | 0x0040UL | 0x8000UL | unrelatedLocal
		);

		TerminalModeSnapshot configured = TerminalInputModePolicy.Configure(
			baseline,
			TerminalInputMode.Raw,
			echoInput: false
		);

		Assert.Equal( 0UL, configured.InputFlags & LinuxInputRawMask );
		Assert.NotEqual( 0UL, configured.InputFlags & unrelatedInput );
		Assert.Equal( 0UL, configured.OutputFlags & 0x0001UL );
		Assert.NotEqual( 0UL, configured.OutputFlags & unrelatedOutput );
		Assert.Equal( 0x0030UL, configured.ControlFlags & 0x0030UL );
		Assert.Equal( 0UL, configured.ControlFlags & 0x0100UL );
		Assert.NotEqual( 0UL, configured.ControlFlags & unrelatedControl );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0001UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0002UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0008UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0040UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x8000UL );
		Assert.NotEqual( 0UL, configured.LocalFlags & unrelatedLocal );
		Assert.Equal( (byte)1, configured.ControlCharacters[ 6 ] );
		Assert.Equal( (byte)0, configured.ControlCharacters[ 5 ] );
	}

	/// <summary>
	/// Verifies that the macOS raw transformation uses Darwin flow-control bit
	/// values rather than Linux values.
	/// </summary>
	[Fact]
	public void ConfiguresMacOsRawFlowControlBits() {
		const ulong macIxOn = 0x0200UL;
		const ulong macIxOff = 0x0400UL;
		const ulong linuxIxOff = 0x1000UL;
		TerminalModeSnapshot baseline = CreateMacOsMode(
			inputFlags: macIxOn | macIxOff | linuxIxOff,
			outputFlags: 0x0001UL,
			controlFlags: 0x0200UL | 0x1000UL,
			localFlags: 0x0080UL | 0x0100UL | 0x0400UL | 0x0008UL | 0x0010UL
		);

		TerminalModeSnapshot configured = TerminalInputModePolicy.Configure(
			baseline,
			TerminalInputMode.Raw,
			echoInput: false
		);

		Assert.Equal( 0UL, configured.InputFlags & macIxOn );
		Assert.Equal( 0UL, configured.InputFlags & macIxOff );
		Assert.NotEqual( 0UL, configured.InputFlags & linuxIxOff );
		Assert.Equal( 0x0300UL, configured.ControlFlags & 0x0300UL );
		Assert.Equal( 0UL, configured.ControlFlags & 0x1000UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0080UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0100UL );
		Assert.Equal( 0UL, configured.LocalFlags & 0x0400UL );
		Assert.Equal( (byte)1, configured.ControlCharacters[ 16 ] );
		Assert.Equal( (byte)0, configured.ControlCharacters[ 17 ] );
	}

	/// <summary>
	/// Verifies canonical, cbreak, and raw Windows console-input mappings while
	/// preserving unrelated console flags.
	/// </summary>
	[Theory]
	[InlineData( TerminalInputMode.Canonical, 0x0003U, 0x0200U )]
	[InlineData( TerminalInputMode.CBreak, 0x0201U, 0x0002U )]
	[InlineData( TerminalInputMode.Raw, 0x0200U, 0x0003U )]
	public void ConfiguresWindowsInputModes(
		TerminalInputMode inputMode,
		uint requiredBits,
		uint clearedBits
	) {
		const uint unrelated = 0x80000000U;
		TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			unrelated | 0x0007U
		);

		TerminalModeSnapshot configured = TerminalInputModePolicy.Configure(
			baseline,
			inputMode,
			echoInput: false
		);
		uint mode = configured.ConsoleMode!.Value;

		Assert.Equal( requiredBits, mode & requiredBits );
		Assert.Equal( 0U, mode & clearedBits );
		Assert.Equal( 0U, mode & 0x0004U );
		Assert.NotEqual( 0U, mode & unrelated );
	}

	/// <summary>Verifies that echo can be requested independently of canonical mode on Windows.</summary>
	[Fact]
	public void RequestsWindowsEchoWhenEnabled() {
		TerminalModeSnapshot configured = TerminalInputModePolicy.Configure(
			TerminalModeSnapshot.CreateWindowsConsole(
				TerminalConsoleDirection.Input,
				0
			),
			TerminalInputMode.Canonical,
			echoInput: true
		);

		Assert.NotEqual( 0U, configured.ConsoleMode!.Value & 0x0004U );
	}

	/// <summary>
	/// Verifies that semantic apply hides platform-specific application timing
	/// from a higher-level consumer.
	/// </summary>
	[Fact]
	public void AppliesPlatformAppropriateTiming() {
		RecordingTerminalControlProvider provider = new();

		TerminalControlMutationResult posixResult = TerminalInputModePolicy.Apply(
			provider,
			TerminalEndpoint.StandardInput,
			CreateLinuxMode( 0, 0, 0, 0 ),
			TerminalInputMode.CBreak,
			echoInput: false
		);
		Assert.True( posixResult.Succeeded );
		Assert.Equal(
			TerminalModeApplyTiming.AfterOutputDrained,
			provider.LastTiming!.Value
		);
		Assert.Equal( TerminalPlatformKind.PosixTermios, provider.LastMode!.Platform );

		TerminalControlMutationResult windowsResult = TerminalInputModePolicy.Apply(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalModeSnapshot.CreateWindowsConsole(
				TerminalConsoleDirection.Input,
				0
			),
			TerminalInputMode.CBreak,
			echoInput: false
		);
		Assert.True( windowsResult.Succeeded );
		Assert.Equal( TerminalModeApplyTiming.Immediately, provider.LastTiming!.Value );
		Assert.Equal( TerminalPlatformKind.WindowsConsole, provider.LastMode!.Platform );
	}

	/// <summary>Verifies that undefined semantic input modes are rejected.</summary>
	[Fact]
	public void RejectsUndefinedInputMode() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalInputModePolicy.Configure(
				CreateLinuxMode( 0, 0, 0, 0 ),
				(TerminalInputMode)int.MaxValue,
				echoInput: false
			)
		);
	}

	/// <summary>Verifies that a Windows output snapshot cannot receive input policy.</summary>
	[Fact]
	public void RejectsWindowsOutputMode() {
		Assert.Throws<InvalidOperationException>(
			() => TerminalInputModePolicy.Configure(
				TerminalModeSnapshot.CreateWindowsConsole(
					TerminalConsoleDirection.Output,
					0
				),
				TerminalInputMode.CBreak,
				echoInput: false
			)
		);
	}

	/// <summary>Verifies that noncanonical POSIX modes require VMIN and VTIME slots.</summary>
	[Fact]
	public void RejectsIncompletePosixControlCharacterArray() {
		TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0,
			new byte[ 4 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);

		Assert.Throws<InvalidOperationException>(
			() => TerminalInputModePolicy.Configure(
				baseline,
				TerminalInputMode.CBreak,
				echoInput: false
			)
		);
	}

	private static TerminalModeSnapshot CreateLinuxMode(
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags,
		IEnumerable<byte>? controlCharacters = null
	) {
		return TerminalModeSnapshot.CreatePosix(
			inputFlags,
			outputFlags,
			controlFlags,
			localFlags,
			controlCharacters ?? new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);
	}

	private static TerminalModeSnapshot CreateMacOsMode(
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags
	) {
		return TerminalModeSnapshot.CreatePosix(
			inputFlags,
			outputFlags,
			controlFlags,
			localFlags,
			new byte[ 20 ],
			0xff,
			64,
			null,
			new TerminalSpeed( 9600, 9600 ),
			new TerminalSpeed( 9600, 9600 )
		);
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		internal TerminalModeSnapshot? LastMode {
			get;
			private set;
		}

		internal TerminalModeApplyTiming? LastTiming {
			get;
			private set;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Unsupported(
				"Observation is not used by this test provider."
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by this test provider."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Unsupported(
				"Mode capture is not used by this test provider."
			);
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}

			this.LastMode = mode;
			this.LastTiming = timing;
			return TerminalControlMutationResult.Success();
		}
	}
}
