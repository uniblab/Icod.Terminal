namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T06 terminal identity, encoding, and capability-output integration
/// without touching process terminal state.
/// </summary>
public sealed class TerminalSessionIdentityOutputTests {
	/// <summary>Verifies explicit named resolution through the configured terminal database.</summary>
	[Fact]
	public async Task ResolvesNamedBuiltInTerminal() {
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			new RecordingTerminalOutput(),
			new TerminalSessionOptions {
				TerminalName = "xterm",
				TerminalDatabase = TerminalDatabase.BuiltIn
			}
		);

		Assert.Equal( TerminalIdentitySource.NamedProfile, session.Identity.Source );
		Assert.Equal( "xterm", session.Identity.RequestedName );
		Assert.Equal( TerminalProfiles.Xterm.Name, session.Terminal.Name );
	}

	/// <summary>
	/// Verifies an explicit terminal-description override wins without consulting
	/// a supplied database.
	/// </summary>
	[Fact]
	public async Task ExplicitTerminalOverrideSkipsDatabaseLookup() {
		TerminalDatabase database = new(
			new ITerminalDescriptionProvider[] {
				new ThrowingTerminalDescriptionProvider()
			}
		);

		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			new RecordingTerminalOutput(),
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Vt100,
				TerminalDatabase = database
			}
		);

		Assert.Equal( TerminalIdentitySource.ExplicitOverride, session.Identity.Source );
		Assert.Null( session.Identity.RequestedName );
		Assert.Same( TerminalProfiles.Vt100, session.Terminal );
	}

	/// <summary>
	/// Verifies an unknown POSIX terminal name falls back to dumb rather than
	/// silently becoming xterm.
	/// </summary>
	[Fact]
	public async Task UnknownPosixTerminalFallsBackToDumb() {
		const string unknownName = "icod-terminal-does-not-exist";

		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			new RecordingTerminalOutput(),
			new TerminalSessionOptions {
				TerminalName = unknownName,
				TerminalDatabase = TerminalDatabase.BuiltIn
			}
		);

		Assert.Equal( TerminalIdentitySource.PlatformFallback, session.Identity.Source );
		Assert.Equal( unknownName, session.Identity.RequestedName );
		Assert.Same( TerminalProfiles.Dumb, session.Terminal );
		Assert.NotSame( TerminalProfiles.Xterm, session.Terminal );
	}

	/// <summary>
	/// Verifies Windows fallback uses the published winconsole or Windows Terminal
	/// profile instead of inventing an xterm identity.
	/// </summary>
	[Fact]
	public async Task WindowsFallbackUsesPublishedWindowsProfile() {
		RecordingTerminalControlProvider provider = new(
			TerminalPlatformKind.WindowsConsole
		);

		await using TerminalSession session = await OpenSessionAsync(
			provider,
			new RecordingTerminalOutput(),
			new TerminalSessionOptions {
				TerminalName = "icod-terminal-does-not-exist",
				TerminalDatabase = TerminalDatabase.BuiltIn
			}
		);

		TerminalDescription expected = OperatingSystem.IsWindows()
			&& !string.IsNullOrWhiteSpace(
				Environment.GetEnvironmentVariable( "WT_SESSION" )
			)
			? TerminalProfiles.MsTerminalDirect
			: TerminalProfiles.WinConsole;

		Assert.Equal( TerminalIdentitySource.PlatformFallback, session.Identity.Source );
		Assert.Same( expected, session.Terminal );
		Assert.NotSame( TerminalProfiles.Xterm, session.Terminal );
	}

	/// <summary>Verifies application text uses strict UTF-8 by default.</summary>
	[Fact]
	public async Task WritesApplicationTextAsUtf8ByDefault() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
			}
		);

		await session.WriteTextAsync( "é" );

		Assert.Equal( "utf-8", session.ApplicationEncoding.WebName );
		Assert.Equal(
			new byte[] { 0xc3, 0xa9 },
			output.Bytes.ToArray()
		);
	}

	/// <summary>
	/// Verifies terminfo terminal strings retain their one-byte representation
	/// independently of application text encoding.
	/// </summary>
	[Fact]
	public async Task WritesTerminalStringsWithLatin1ByteFidelity() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ApplicationEncoding = Encoding.Unicode
			}
		);

		await session.WriteTerminalStringAsync( "\u001b[\u00ff" );

		Assert.Equal(
			new byte[] { 0x1b, 0x5b, 0xff },
			output.Bytes.ToArray()
		);
	}

	/// <summary>Verifies a terminal capability can be emitted directly from the session profile.</summary>
	[Fact]
	public async Task WritesPresentTerminalCapability() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Xterm
			}
		);
		string expected = TerminalProfiles.Xterm.GetRequiredString(
			StringCapability.EnterCursorAddressingMode
		);

		bool written = await session.WriteCapabilityAsync(
			StringCapability.EnterCursorAddressingMode
		);

		Assert.True( written );
		Assert.Equal(
			Encoding.Latin1.GetBytes( expected ),
			output.Bytes.ToArray()
		);
	}

	/// <summary>Verifies a missing capability is reported without emitting bytes.</summary>
	[Fact]
	public async Task MissingTerminalCapabilityReturnsFalse() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			new RecordingTerminalControlProvider(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
			}
		);

		bool written = await session.WriteCapabilityAsync(
			StringCapability.EnterCursorAddressingMode
		);

		Assert.False( written );
		Assert.Empty( output.Bytes );
	}

	/// <summary>Verifies ambiguous or invalid T06 session options fail before native work.</summary>
	[Fact]
	public async Task RejectsInvalidIdentityAndOutputOptions() {
		RecordingTerminalControlProvider provider = new();

		await Assert.ThrowsAsync<ArgumentException>(
			() => OpenSessionAsync(
				provider,
				new RecordingTerminalOutput(),
				new TerminalSessionOptions {
					TerminalOverride = TerminalProfiles.Dumb,
					TerminalName = "dumb"
				}
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentException>(
			() => OpenSessionAsync(
				provider,
				new RecordingTerminalOutput(),
				new TerminalSessionOptions {
					TerminalName = " "
				}
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentNullException>(
			() => OpenSessionAsync(
				provider,
				new RecordingTerminalOutput(),
				new TerminalSessionOptions {
					ApplicationEncoding = null!
				}
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => OpenSessionAsync(
				provider,
				new RecordingTerminalOutput(),
				new TerminalSessionOptions {
					CapabilityPaddingMode = (PaddingMode)int.MaxValue
				}
			).AsTask()
		);

		Assert.Equal( 0, provider.ObserveCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalControlProvider provider,
		RecordingTerminalOutput output,
		TerminalSessionOptions options
	) {
		ArgumentNullException.ThrowIfNull( provider );
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( options );

		return TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			options
		);
	}

	private sealed class ThrowingTerminalDescriptionProvider : ITerminalDescriptionProvider {
		public bool TryLoad(
			string name,
			[System.Diagnostics.CodeAnalysis.NotNullWhen( true )] out TerminalDescription? terminal
		) {
			ArgumentException.ThrowIfNullOrWhiteSpace( name );
			terminal = null;
			throw new InvalidOperationException( "The database should not have been consulted." );
		}
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		internal List<byte> Bytes {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline;
		private readonly TerminalPlatformKind platform;

		internal RecordingTerminalControlProvider(
			TerminalPlatformKind platform = TerminalPlatformKind.PosixTermios
		) {
			this.platform = platform;
			this.baseline = TerminalPlatformKind.WindowsConsole == platform
				? TerminalModeSnapshot.CreateWindowsConsole(
					TerminalConsoleDirection.Input,
					0x0007U
				)
				: TerminalModeSnapshot.CreatePosix(
					0,
					0,
					0,
					0x0002UL,
					new byte[ 32 ],
					0,
					32,
					0,
					new TerminalSpeed( 13, 9600 ),
					new TerminalSpeed( 13, 9600 )
				);
		}

		internal int ObserveCount {
			get;
			private set;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			++this.ObserveCount;

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					this.platform,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
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
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.baseline
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

			return TerminalControlMutationResult.Success();
		}
	}
}
