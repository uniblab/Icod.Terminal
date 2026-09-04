namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T48 persistent OSC 8 hyperlink ownership, strict-LIFO nesting, and cleanup.
/// </summary>
public sealed class TerminalSessionHyperlinkLeaseTests {
	[Fact]
	public async Task SingleLeaseBeginsAndClosesHyperlinkState() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalHyperlinkLease lease = await session.AcquireHyperlinkAsync(
			"https://example.com/",
			"outer"
		);
		await session.WriteTextAsync( "linked" );
		await lease.DisposeAsync();

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/",
				"outer"
			),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			System.Text.Encoding.UTF8.GetBytes( "linked" ),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task NestedLeaseRestoresOuterHyperlink() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
			"https://example.com/outer",
			"outer"
		);
		TerminalHyperlinkLease inner = await session.AcquireHyperlinkAsync(
			"https://example.com/inner",
			"inner"
		);
		await inner.DisposeAsync();
		await outer.DisposeAsync();

		Assert.Equal( 4, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/outer",
				"outer"
			),
			output.Writes[ 0 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/inner",
				"inner"
			),
			output.Writes[ 1 ]
		);
		Assert.Equal(
			output.Writes[ 0 ],
			output.Writes[ 2 ]
		);
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ 3 ]
		);
	}

	[Fact]
	public async Task NestedIdenticalStateStillEmitsExplicitBeginAndRestore() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
			"https://example.com/",
			"same"
		);
		TerminalHyperlinkLease inner = await session.AcquireHyperlinkAsync(
			"https://example.com/",
			"same"
		);
		await inner.DisposeAsync();
		await outer.DisposeAsync();

		Assert.Equal( 4, output.Writes.Count );
		Assert.Equal( output.Writes[ 0 ], output.Writes[ 1 ] );
		Assert.Equal( output.Writes[ 0 ], output.Writes[ 2 ] );
	}

	[Fact]
	public async Task OutOfOrderDisposalFailsWithoutChangingState() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
			"https://example.com/outer"
		);
		TerminalHyperlinkLease inner = await session.AcquireHyperlinkAsync(
			"https://example.com/inner"
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => outer.DisposeAsync().AsTask()
		);
		Assert.Equal( 2, output.Writes.Count );

		await inner.DisposeAsync();
		await outer.DisposeAsync();
		Assert.Equal( 4, output.Writes.Count );
	}

	[Fact]
	public async Task ReleaseFailureRetainsLeaseForRetry() {
		FailingTerminalOutput output = new( 2 );
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalHyperlinkLease lease = await session.AcquireHyperlinkAsync(
			"https://example.com/"
		);

		await Assert.ThrowsAsync<IOException>(
			() => lease.DisposeAsync().AsTask()
		);
		Assert.Equal( 2, output.WriteCount );

		await lease.DisposeAsync();
		Assert.Equal( 3, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.SuccessfulWrites[ ^1 ]
		);
	}

	[Fact]
	public async Task SessionDisposalClosesAllOutstandingHyperlinkScopesOnce() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
			"https://example.com/outer"
		);
		TerminalHyperlinkLease inner = await session.AcquireHyperlinkAsync(
			"https://example.com/inner"
		);

		await session.DisposeAsync();

		Assert.Equal( 3, output.Writes.Count );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.Writes[ ^1 ]
		);
		await inner.DisposeAsync();
		await outer.DisposeAsync();
		Assert.Equal( 3, output.Writes.Count );
	}

	[Fact]
	public async Task FailedBoundedCloseRemainsOwnedUntilSessionDisposal() {
		FailingTerminalOutput output = new( 3 );
		TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<IOException>(
			() => session.WriteHyperlinkAsync(
				"text",
				"https://example.com/"
			).AsTask()
		);
		Assert.Equal( 3, output.WriteCount );

		await session.DisposeAsync();
		Assert.Equal( 4, output.WriteCount );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			output.SuccessfulWrites[ ^1 ]
		);
	}

	[Fact]
	public async Task CancelledAcquireWritesNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		using CancellationTokenSource cancellationSource = new();
		cancellationSource.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.AcquireHyperlinkAsync(
				"https://example.com/",
				cancellationToken: cancellationSource.Token
			).AsTask()
		);
		Assert.Empty( output.Writes );
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
		internal List<byte[]> Writes {
			get;
		} = [];

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FailingTerminalOutput : ITerminalOutput {
		private readonly HashSet<int> failingWrites;

		internal FailingTerminalOutput(
			params int[] failingWrites
		) {
			ArgumentNullException.ThrowIfNull( failingWrites );
			this.failingWrites = [ .. failingWrites ];
		}

		internal int WriteCount {
			get;
			private set;
		}

		internal List<byte[]> SuccessfulWrites {
			get;
		} = [];

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			if ( this.failingWrites.Contains( this.WriteCount ) ) {
				return ValueTask.FromException(
					new IOException( $"Synthetic output failure {this.WriteCount}." )
				);
			}

			this.SuccessfulWrites.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
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
				"Size is not used by this test provider."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
