namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T41 cross-platform path and privacy acceptance for OSC 7 publication.
/// </summary>
public sealed class TerminalSessionLocationAcceptanceTests {
	[Theory]
	[InlineData( "/home/alice/My Project/#/%20/猫", 0, null, "1B5D373B66696C653A2F2F2F686F6D652F616C6963652F4D7925323050726F6A6563742F2532332F25323532302F2545372538432541421B5C" )]
	[InlineData( "c:\\Temp\\My Project\\猫", 1, null, "1B5D373B66696C653A2F2F2F433A2F54656D702F4D7925323050726F6A6563742F2545372538432541421B5C" )]
	[InlineData( "\\\\server\\share\\My Project\\猫", 2, null, "1B5D373B66696C653A2F2F7365727665722F73686172652F4D7925323050726F6A6563742F2545372538432541421B5C" )]
	[InlineData( "/srv/project", 0, "[2001:db8::1]", "1B5D373B66696C653A2F2F5B323030313A6462383A3A315D2F7372762F70726F6A6563741B5C" )]
	public async Task PublicLocationEncodingIsHostIndependent(
		string path,
		int pathStyleValue,
		string? authority,
		string expectedHex
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.PublishCurrentLocationAsync(
			path,
			(TerminalLocationPathStyle)pathStyleValue,
			authority
		);

		Assert.Equal(
			Convert.FromHexString( expectedHex ),
			output.Bytes.ToArray()
		);
		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task OpeningAndDisposingSessionDoesNotPublishLocation() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );

		Assert.Equal( 0, CountOscSevenFrames( output.Bytes ) );

		await session.DisposeAsync();

		Assert.Equal( 0, CountOscSevenFrames( output.Bytes ) );
	}

	[Fact]
	public async Task TitleOperationDoesNotPublishLocation() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.SetWindowTitleAsync( "location privacy" );

		Assert.Equal( 0, CountOscSevenFrames( output.Bytes ) );
		Assert.Equal( 1, output.WriteCount );
	}

	[Fact]
	public async Task OrdinaryApplicationOutputDoesNotPublishLocation() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.WriteTextAsync( "ordinary application text" );

		Assert.Equal( 0, CountOscSevenFrames( output.Bytes ) );
		Assert.Equal( 1, output.WriteCount );
	}

	[Fact]
	public async Task OnlyExplicitLocationCallPublishesOscSeven() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await session.WriteTextAsync( "before" );
		await session.SetTitleAsync( "title" );
		await session.PublishCurrentLocationAsync(
			"/usr/src",
			TerminalLocationPathStyle.Posix
		);
		await session.WriteTextAsync( "after" );

		Assert.Equal( 1, CountOscSevenFrames( output.Bytes ) );
		Assert.Equal( 4, output.WriteCount );
	}

	[Fact]
	public async Task DisposalAfterExplicitPublicationDoesNotRepublishLocation() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		await session.PublishCurrentLocationAsync(
			"/usr/src",
			TerminalLocationPathStyle.Posix
		);

		Assert.Equal( 1, CountOscSevenFrames( output.Bytes ) );

		await session.DisposeAsync();

		Assert.Equal( 1, CountOscSevenFrames( output.Bytes ) );
	}

	private static int CountOscSevenFrames(
		IReadOnlyList<byte> bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );

		int count = 0;
		for ( int index = 0; index + 3 < bytes.Count; ++index ) {
			if ( 0x1b == bytes[ index ]
				&& 0x5d == bytes[ index + 1 ]
				&& (byte)'7' == bytes[ index + 2 ]
				&& (byte)';' == bytes[ index + 3 ] ) {
				++count;
			}
		}

		return count;
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
			}
		);
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

		internal int WriteCount {
			get;
			private set;
		}

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
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
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
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

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
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
				"Size is not used by this acceptance provider."
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
