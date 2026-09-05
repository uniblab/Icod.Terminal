namespace Icod.Terminal.Tests.Session;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Locks final 0.9 synchronized-output public and disposal-ordering invariants.
/// </summary>
public sealed class TerminalSynchronizedOutputPreMergeHardeningTests {
	[Fact]
	public async Task RedirectedOutputRejectsAcquisitionWithoutEmission() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			output,
			TerminalProfiles.Dumb,
			isTerminal: false
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.AcquireSynchronizedOutputAsync().AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task SessionDisposalRestoresPresentationBeforeFinalSynchronizedEnd() {
		RecordingOutput output = new();
		TerminalDescription terminal = new TerminalDescriptionBuilder(
			"synchronized-output-disposal-ordering"
		)
			.SetString(
				StringCapability.CursorInvisible,
				"<hide>"
			)
			.SetString(
				StringCapability.CursorNormal,
				"<show>"
			)
			.Build();
		TerminalSession session = await OpenSessionAsync(
			output,
			terminal,
			isTerminal: true
		);

		_ = await session.AcquireSynchronizedOutputAsync();
		TerminalPresentationLease presentation = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();
		_ = presentation;

		await session.DisposeAsync();

		Assert.Equal( 4, output.WriteCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputBeginFrame(),
			output.GetWrite( 0 )
		);
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<hide>" ),
			output.GetWrite( 1 )
		);
		Assert.Equal(
			Encoding.Latin1.GetBytes( "<show>" ),
			output.GetWrite( 2 )
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			output.GetWrite( 3 )
		);
		Assert.Equal( 4, output.FlushCount );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingOutput output,
		TerminalDescription terminal,
		bool isTerminal
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( terminal );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider( isTerminal ),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyInput(),
			output,
			new TerminalSessionOptions {
				RequireInteractiveOutput = isTerminal,
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class EmptyInput : ITerminalInput {
		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			_ = buffer;
			await Task.Delay(
				Timeout.InfiniteTimeSpan,
				cancellationToken
			).ConfigureAwait( false );
			return 0;
		}
	}

	private sealed class RecordingOutput : ITerminalOutput {
		private readonly List<byte[]> writes = [];
		private int flushCount;

		internal int WriteCount {
			get {
				return this.writes.Count;
			}
		}

		internal int FlushCount {
			get {
				return Volatile.Read( ref this.flushCount );
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			return this.writes[ index ].ToArray();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.writes.Add( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Increment( ref this.flushCount );
			return ValueTask.CompletedTask;
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
		private readonly bool outputIsTerminal;
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x00000007u
		);

		internal RecordingTerminalControlProvider(
			bool outputIsTerminal
		) {
			this.outputIsTerminal = outputIsTerminal;
		}

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isTerminal = this.outputIsTerminal
				|| 1 != endpoint.FileDescriptor;
			if ( !isTerminal ) {
				return TerminalControlResult<TerminalEndpointObservation>.Available(
					new TerminalEndpointObservation(
						false,
						null,
						null,
						TerminalControlCapabilities.None
					)
				);
			}

			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.WindowsConsole,
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
				"Size is not required by synchronized-output pre-merge hardening tests."
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
