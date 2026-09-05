namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the T136 decision that 0.13 color mutation is unscoped and has no automatic restoration owner.
/// </summary>
public sealed class TerminalColorLifecycleSemanticsTests {
	[Fact]
	public async Task PaletteMutationIsNotAutomaticallyResetOrReplayedByDisposal() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			provider,
			output
		);

		await session.SetPaletteColorAsync(
			5,
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		Assert.Single( output.Writes );

		await session.DisposeAsync();
		await session.DisposeAsync();

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:1111/2222/3333\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task DynamicColorMutationIsNotAutomaticallyResetOrReplayedByDisposal() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			provider,
			output
		);

		await session.SetDynamicColorAsync(
			TerminalDynamicColor.DefaultForeground,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc )
		);
		Assert.Single( output.Writes );

		await session.DisposeAsync();

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]10;rgb:aaaa/bbbb/cccc\u001b\\" ),
			output.Writes[ 0 ]
		);
	}

	[Fact]
	public async Task SuspendResumeDoesNotResetOrReplayUnscopedColorMutation() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			provider,
			output,
			lifecycle
		);
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		await session.SetPaletteColorAsync(
			7,
			new TerminalColor( 0x0101, 0x0202, 0x0303 )
		);
		await session.SetDynamicColorAsync(
			TerminalDynamicColor.TextCursor,
			new TerminalColor( 0x0404, 0x0505, 0x0606 )
		);
		Assert.Equal( 2, output.Writes.Count );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( 2, output.Writes.Count );

		await session.DisposeAsync();
		Assert.Equal( 2, output.Writes.Count );
	}

	[Fact]
	public async Task InvalidateStateDoesNotEmitColorTraffic() {
		RecordingTerminalControlProvider provider = new();
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			provider,
			output
		);

		await session.SetDynamicColorAsync(
			TerminalDynamicColor.HighlightBackground,
			new TerminalColor( 1, 2, 3 )
		);
		Assert.Single( output.Writes );

		session.InvalidateState();

		Assert.Single( output.Writes );
		Assert.False( session.IsStateValid );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalControlProvider provider,
		RecordingTerminalOutput output,
		TestTerminalLifecycleSource? lifecycle = null
	) {
		ArgumentNullException.ThrowIfNull( provider );
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			provider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
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

	private sealed class TestTerminalLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}
			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
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
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by color lifecycle semantics tests."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			return TerminalControlMutationResult.Success();
		}
	}
}
