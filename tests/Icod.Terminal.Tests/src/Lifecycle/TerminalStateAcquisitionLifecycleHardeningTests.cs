/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies that new public state ownership cannot enter while lifecycle or teardown owns the session.
/// </summary>
public sealed class TerminalStateAcquisitionLifecycleHardeningTests {
	private const string EnablePaste = "<P+>";
	private const string DisablePaste = "<P->";
	private const string PasteStart = "<PS>";
	private const string PasteEnd = "<PE>";
	private const string EnterAlternateScreen = "<A+>";
	private const string ExitAlternateScreen = "<A->";

	[Fact]
	public async Task PublicStateAcquisitionIsRejectedDuringSuspendPreparation() {
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			lifecycle,
			transport
		);
		BlockingParticipant participant = new();
		using IDisposable registration = session.RegisterLifecycleParticipant(
			participant
		);
		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		await participant.WaitUntilPreparingAsync().WaitAsync( timeout.Token );
		Assert.False( session.IsStateValid );

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			).AsTask()
		);
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			).AsTask()
		);

		Assert.Empty( transport.Writes );

		participant.ReleasePreparation();
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.True( session.IsStateValid );
		Assert.Empty( transport.Writes );

		TerminalInputProtocolLease inputLease = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			)
		).GetRequiredValue();
		TerminalPresentationLease presentationLease = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				EnablePaste,
				EnterAlternateScreen
			},
			transport.Writes
		);

		await presentationLease.DisposeAsync();
		await inputLease.DisposeAsync();
		Assert.Equal(
			new[] {
				EnablePaste,
				EnterAlternateScreen,
				ExitAlternateScreen,
				DisablePaste
			},
			transport.Writes
		);
	}

	[Fact]
	public async Task PublicStateAcquisitionIsRejectedAfterDisposalStarts() {
		TestTerminalLifecycleSource lifecycle = new();
		RecordingTransport transport = new();
		TerminalSession session = await OpenSessionAsync(
			lifecycle,
			transport
		);

		Task disposal = session.DisposeAsync().AsTask();

		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			).AsTask()
		);
		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			).AsTask()
		);

		await disposal;
		Assert.Empty( transport.Writes );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TestTerminalLifecycleSource lifecycle,
		RecordingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( lifecycle );
		ArgumentNullException.ThrowIfNull( transport );

		TerminalDescription terminal = new TerminalDescriptionBuilder(
			"lifecycle-acquisition-hardening"
		)
			.SetString(
				StringCapability.EnterCursorAddressingMode,
				EnterAlternateScreen
			)
			.SetString(
				StringCapability.ExitCursorAddressingMode,
				ExitAlternateScreen
			)
			.SetExtendedString( "BE", EnablePaste )
			.SetExtendedString( "BD", DisablePaste )
			.SetExtendedString( "PS", PasteStart )
			.SetExtendedString( "PE", PasteEnd )
			.Build();

		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
			}
		);
	}

	private sealed class BlockingParticipant : ITerminalSessionLifecycleParticipant {
		private readonly TaskCompletionSource preparing = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource release = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		internal Task WaitUntilPreparingAsync() {
			return this.preparing.Task;
		}

		internal void ReleasePreparation() {
			this.release.TrySetResult();
		}

		public async ValueTask PrepareForTerminalSuspendAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.preparing.TrySetResult();
			await this.release.Task.WaitAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

		public ValueTask ResumeAfterTerminalSuspendAsync(
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

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		internal List<string> Writes {
			get;
		} = [];

		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			this.Writes.Add( Encoding.Latin1.GetString( buffer.Span ) );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
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
			return TerminalControlResult<TerminalSize>.Unavailable(
				"Live size is not required by lifecycle acquisition hardening."
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
