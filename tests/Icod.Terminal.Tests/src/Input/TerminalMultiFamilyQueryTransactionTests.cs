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
namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies N154 multi-family query transactions without requiring Kitty Graphics support.
/// </summary>
public sealed class TerminalMultiFamilyQueryTransactionTests {
	private static readonly byte[] Request = Encoding.ASCII.GetBytes(
		"\u001b_Ga=q,i=1;AAAA\u001b\\\u001b[c"
	);
	private static readonly byte[] ApcCompletion = Encoding.ASCII.GetBytes(
		"\u001b_Gi=1;OK\u001b\\"
	);
	private static readonly byte[] CsiBarrier = Encoding.ASCII.GetBytes(
		"\u001b[?1;2c"
	);

	[Fact]
	public async Task ApcCompletionWinsSyntheticCompoundQuery() {
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalQueryResponsePlan responsePlan = CreateResponsePlan();

		Task<TerminalQueryResponseResult> query = session.ExecuteQueryTransactionAsync(
			Request,
			responsePlan,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal( Request, transport.GetWrite( 0 ) );

		transport.Publish( ApcCompletion );
		TerminalQueryResponseResult result = await query;

		Assert.Equal( TerminalQueryResponseDisposition.Completion, result.Disposition );
		Assert.Equal( TerminalResponseFrameKind.Apc, result.Frame.Kind );
		Assert.Equal( ApcCompletion, result.Frame.Bytes.ToArray() );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task CsiBarrierWinsSyntheticCompoundQuery() {
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalQueryResponsePlan responsePlan = CreateResponsePlan();

		Task<TerminalQueryResponseResult> query = session.ExecuteQueryTransactionAsync(
			Request,
			responsePlan,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		transport.Publish( CsiBarrier );
		TerminalQueryResponseResult result = await query;

		Assert.Equal( TerminalQueryResponseDisposition.Barrier, result.Disposition );
		Assert.Equal( TerminalResponseFrameKind.Csi, result.Frame.Kind );
		Assert.Equal( CsiBarrier, result.Frame.Bytes.ToArray() );
	}

	[Fact]
	public async Task ApplicationInputRemainsLiveDuringCompoundQuery() {
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalQueryResponsePlan responsePlan = CreateResponsePlan();

		Task<TerminalQueryResponseResult> query = session.ExecuteQueryTransactionAsync(
			Request,
			responsePlan,
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );

		Task<TerminalEvent> applicationRead = session.ReadEventAsync().AsTask();
		transport.Publish( Encoding.UTF8.GetBytes( "x" ) );
		TerminalEvent terminalEvent = await applicationRead;

		Assert.False( query.IsCompleted );
		Assert.Equal( TerminalEventKind.Input, terminalEvent.Kind );
		Assert.Equal(
			new Rune( 'x' ),
			Assert.IsType<TerminalInputEvent>( terminalEvent.Input ).Character
		);

		transport.Publish( ApcCompletion );
		Assert.Equal(
			TerminalQueryResponseDisposition.Completion,
			( await query ).Disposition
		);
	}

	[Fact]
	public void ResponsePlanRejectsDuplicateFamiliesAndBarrierOnlyPlans() {
		ExactResponseMatcher firstApc = new(
			TerminalResponseFrameKind.Apc,
			ApcCompletion
		);
		ExactResponseMatcher secondApc = new(
			TerminalResponseFrameKind.Apc,
			Encoding.ASCII.GetBytes( "\u001b_Gi=2;OK\u001b\\" )
		);
		ExactResponseMatcher barrier = new(
			TerminalResponseFrameKind.Csi,
			CsiBarrier
		);

		Assert.Throws<ArgumentException>(
			() => new TerminalQueryResponsePlan(
				new TerminalQueryResponseRule(
					firstApc,
					TerminalQueryResponseDisposition.Completion
				),
				new TerminalQueryResponseRule(
					secondApc,
					TerminalQueryResponseDisposition.Barrier
				)
			)
		);
		Assert.Throws<ArgumentException>(
			() => new TerminalQueryResponsePlan(
				new TerminalQueryResponseRule(
					barrier,
					TerminalQueryResponseDisposition.Barrier
				)
			)
		);
	}

	private static TerminalQueryResponsePlan CreateResponsePlan() {
		return new TerminalQueryResponsePlan(
			new TerminalQueryResponseRule(
				new ExactResponseMatcher(
					TerminalResponseFrameKind.Apc,
					ApcCompletion
				),
				TerminalQueryResponseDisposition.Completion
			),
			new TerminalQueryResponseRule(
				new ExactResponseMatcher(
					TerminalResponseFrameKind.Csi,
					CsiBarrier
				),
				TerminalQueryResponseDisposition.Barrier
			)
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		QueryTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		QueryTransport transport,
		int expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > expected ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}

		using CancellationTokenSource timeout = new();
		timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
		await transport.WaitForWriteCountAsync(
			expected,
			timeout.Token
		);
	}

	private sealed class ExactResponseMatcher : ITerminalResponseMatcher {
		private readonly byte[] expected;

		internal ExactResponseMatcher(
			TerminalResponseFrameKind frameKind,
			byte[] expected
		) {
			if ( !Enum.IsDefined( frameKind ) ) {
				throw new ArgumentOutOfRangeException( nameof( frameKind ) );
			}
			ArgumentNullException.ThrowIfNull( expected );
			this.FrameKind = frameKind;
			this.expected = expected.ToArray();
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		}

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return this.FrameKind == frame.Kind
				&& frame.Bytes.Span.SequenceEqual( this.expected )
			;
		}
	}

	private sealed class QueryTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly List<byte[]> writes = [];
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private int activeReads;
		private int maximumConcurrentReads;

		internal int MaximumConcurrentReads {
			get {
				return Volatile.Read( ref this.maximumConcurrentReads );
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal async ValueTask WaitForWriteCountAsync(
			int expected,
			CancellationToken cancellationToken
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}
			cancellationToken.ThrowIfCancellationRequested();

			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}

				await this.writeSignal.WaitAsync(
					cancellationToken
				).ConfigureAwait( false );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			int active = Interlocked.Increment( ref this.activeReads );
			this.RecordMaximumConcurrentReads( active );
			try {
				byte[] bytes = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( bytes.Length > buffer.Length ) {
					throw new InvalidOperationException(
						"The scripted input chunk exceeds the decoder read buffer."
					);
				}

				bytes.AsSpan().CopyTo( buffer.Span );
				return bytes.Length;
			} finally {
				Interlocked.Decrement( ref this.activeReads );
			}
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		private void RecordMaximumConcurrentReads(
			int active
		) {
			while ( true ) {
				int observed = Volatile.Read( ref this.maximumConcurrentReads );
				if ( active <= observed ) {
					return;
				}
				if ( observed == Interlocked.CompareExchange(
					ref this.maximumConcurrentReads,
					active,
					observed
				) ) {
					return;
				}
			}
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
			return TerminalControlResult<TerminalSize>.Unavailable(
				"No scripted live size."
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
