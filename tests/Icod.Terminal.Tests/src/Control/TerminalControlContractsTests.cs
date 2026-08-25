namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Icod.TermInfo;

using Xunit;

/// <summary>
/// Verifies the policy-neutral low-level terminal-control contracts.
/// </summary>
public sealed class TerminalControlContractsTests {
	/// <summary>Verifies endpoint construction and standard descriptor identities.</summary>
	[Fact]
	public void CreatesDescriptorAndPathEndpoints() {
		Assert.Equal(
			0,
			TerminalEndpoint.StandardInput.FileDescriptor
		);
		Assert.Equal(
			1,
			TerminalEndpoint.StandardOutput.FileDescriptor
		);
		Assert.Equal(
			2,
			TerminalEndpoint.StandardError.FileDescriptor
		);

		TerminalEndpoint named = TerminalEndpoint.ForPath( "/dev/tty" );

		Assert.Equal(
			TerminalEndpointKind.Path,
			named.Kind
		);
		Assert.Equal(
			"/dev/tty",
			named.Path
		);
		Assert.Equal(
			"/dev/tty",
			named.DisplayName
		);
	}

	/// <summary>Verifies that invalid endpoint values are rejected immediately.</summary>
	[Fact]
	public void RejectsInvalidEndpoints() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalEndpoint.ForFileDescriptor( -1 )
		);
		Assert.Throws<ArgumentException>(
			() => TerminalEndpoint.ForPath( " " )
		);
	}

	/// <summary>
	/// Verifies that attachment observations cannot fabricate platforms or
	/// capabilities for a nonterminal endpoint.
	/// </summary>
	[Fact]
	public void EnforcesObservationInvariants() {
		Assert.Throws<ArgumentException>(
			() => new TerminalEndpointObservation(
				false,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.None
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalEndpointObservation(
				true,
				"/dev/tty",
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.ModeRead
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.Pathname
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment
					| (TerminalControlCapabilities)( 1 << 20 )
			)
		);

		TerminalEndpointObservation sized =
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.LiveSize
			);

		Assert.True(
			sized.Capabilities.HasFlag(
				TerminalControlCapabilities.LiveSize
			)
		);
	}

	/// <summary>
	/// Verifies that controlled results retain distinct unavailable,
	/// unsupported, and failed states.
	/// </summary>
	[Fact]
	public void PreservesControlledResultStates() {
		TerminalControlResult<string> available =
			TerminalControlResult<string>.Available( "value" );

		Assert.True( available.IsAvailable );
		Assert.Equal(
			"value",
			available.GetRequiredValue()
		);

		TerminalControlResult<string> unavailable =
			TerminalControlResult<string>.Unavailable(
				null,
				25
			);

		Assert.Equal(
			TerminalControlStatus.Unavailable,
			unavailable.Status
		);
		Assert.Equal(
			25,
			unavailable.NativeErrorCode
		);
		Assert.False(
			string.IsNullOrWhiteSpace(
				unavailable.Message
			)
		);
		Assert.Throws<InvalidOperationException>(
			unavailable.GetRequiredValue
		);

		Assert.Equal(
			TerminalControlStatus.Unsupported,
			TerminalControlMutationResult.Unsupported(
				null
			).Status
		);
		Assert.Equal(
			TerminalControlStatus.Failed,
			TerminalControlMutationResult.Failed(
				"failed"
			).Status
		);
	}

	/// <summary>Verifies that Windows snapshots do not expose fabricated POSIX fields.</summary>
	[Fact]
	public void KeepsWindowsSnapshotPlatformFieldsExplicit() {
		TerminalModeSnapshot mode =
			TerminalModeSnapshot.CreateWindowsConsole(
				TerminalConsoleDirection.Input,
				0x1234
			);

		Assert.Equal(
			0,
			mode.NativeFlagWidth
		);
		Assert.Empty(
			mode.ControlCharacters
		);
		Assert.Null(
			mode.LineDiscipline
		);
		Assert.Null(
			mode.InputSpeed
		);
		Assert.Null(
			mode.OutputSpeed
		);
	}

	/// <summary>Verifies that POSIX snapshots defensively copy control characters.</summary>
	[Fact]
	public void PosixSnapshotOwnsControlCharacterState() {
		var characters = new byte[] {
			1,
			2,
			3,
			4
		};

		TerminalModeSnapshot mode = CreatePosixMode(
			characters
		);

		characters[ 0 ] = 99;

		Assert.Equal(
			1,
			mode.ControlCharacters[ 0 ]
		);
	}

	/// <summary>Verifies that undefined mutation timing values are rejected.</summary>
	[Fact]
	public void RejectsUndefinedMutationTiming() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => SystemTerminalControlProvider.Instance.SetMode(
				TerminalEndpoint.StandardInput,
				TerminalModeSnapshot.CreateWindowsConsole(
					TerminalConsoleDirection.Input,
					0
				),
				(TerminalModeApplyTiming)int.MaxValue
			)
		);
	}

	/// <summary>
	/// Verifies that tests can inject a complete provider without reaching
	/// process-global handles or native APIs.
	/// </summary>
	[Fact]
	public void SupportsDeterministicProviderInjection() {
		TerminalEndpointObservation expected =
			new TerminalEndpointObservation(
				true,
				"test-terminal",
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.Pathname
					| TerminalControlCapabilities.LiveSize
			);
		ITerminalControlProvider provider =
			new FakeTerminalControlProvider( expected );

		Assert.Same(
			expected,
			provider.Observe(
				TerminalEndpoint.StandardInput
			).GetRequiredValue()
		);
		Assert.Equal(
			new TerminalSize(
				100,
				40
			),
			provider.GetSize(
				TerminalEndpoint.StandardInput
			).GetRequiredValue()
		);
	}

	private static TerminalModeSnapshot CreatePosixMode(
		IEnumerable<byte> characters
	) {
		ArgumentNullException.ThrowIfNull( characters );

		return TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0,
			characters,
			0,
			32,
			7,
			new TerminalSpeed(
				13,
				9600
			),
			new TerminalSpeed(
				14,
				19200
			)
		);
	}

	private sealed class FakeTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalEndpointObservation observation;

		internal FakeTerminalControlProvider(
			TerminalEndpointObservation observation
		) {
			ArgumentNullException.ThrowIfNull( observation );

			this.observation = observation;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				this.observation
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );

			return TerminalControlResult<TerminalSize>.Available(
				new TerminalSize(
					100,
					40
				)
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );

			return TerminalControlResult<TerminalModeSnapshot>.Unavailable(
				"No mode was configured."
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

			return TerminalControlMutationResult.Unavailable(
				"No mutation was configured."
			);
		}
	}
}