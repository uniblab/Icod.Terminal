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
/// Qualifies the complete CSI grammar at resource boundaries and hostile fragmentation points.
/// </summary>
public sealed class TerminalCsiHardeningTests {
	[Fact]
	public void ExactSyntaxResourceBoundsRemainValid() {
		TerminalCsiSyntax raw = ParseSyntax(
			"\u001b["
				+ new string( '1', TerminalCsiSyntax.MaximumRawParameterBytes )
				+ "m"
		);
		Assert.Equal(
			TerminalCsiSyntax.MaximumRawParameterBytes,
			raw.RawParameterBytes.Length
		);

		string parameterData = string.Join(
			';',
			Enumerable.Repeat(
				"1",
				TerminalCsiSyntax.MaximumParameterCount
			)
		);
		TerminalCsiSyntax parameters = ParseSyntax(
			$"\u001b[{parameterData}m"
		);
		Assert.Equal(
			TerminalCsiSyntax.MaximumParameterCount,
			parameters.Parameters.Length
		);

		string subparameterData = string.Join(
			':',
			Enumerable.Repeat(
				"1",
				TerminalCsiSyntax.MaximumSubparameterCount
			)
		);
		TerminalCsiSyntax subparameters = ParseSyntax(
			$"\u001b[{subparameterData}m"
		);
		Assert.Equal(
			TerminalCsiSyntax.MaximumSubparameterCount,
			subparameters.Parameters.Span[ 0 ].Subparameters.Length
		);
	}

	[Fact]
	public void NumericBoundaryAcceptsMaximumAndRejectsMaximumPlusOne() {
		TerminalCsiSyntax maximum = ParseSyntax(
			$"\u001b[{TerminalCsiParameterSemantics.DefaultMaximumNumericValue}m"
		);
		TerminalCsiSyntax oversized = ParseSyntax(
			$"\u001b[{TerminalCsiParameterSemantics.DefaultMaximumNumericValue + 1}m"
		);

		Assert.Equal(
			TerminalCsiParameterSemantics.DefaultMaximumNumericValue,
			TerminalCsiParameterSemantics.GetNumericParameter(
				maximum,
				0
			).Value
		);
		Assert.Throws<FormatException>(
			() => TerminalCsiParameterSemantics.GetNumericParameter(
				oversized,
				0
			)
		);
	}

	[Fact]
	public void PrivateSemicolonColonAndEmptyComponentsRemainLosslessTogether() {
		TerminalCsiSyntax syntax = ParseSyntax(
			"\u001b[?><;:;1::2;0$q"
		);

		Assert.Equal( "?><", AsAscii( syntax.PrivateParameterBytes ) );
		Assert.Equal( ";:;1::2;0", AsAscii( syntax.ParameterDataBytes ) );
		Assert.Equal( "$", AsAscii( syntax.IntermediateBytes ) );
		Assert.Equal( (byte)'q', syntax.FinalByte );

		ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
		Assert.Equal( 4, parameters.Length );
		Assert.True( parameters[ 0 ].IsEmpty );
		Assert.Equal( 2, parameters[ 1 ].Subparameters.Length );
		Assert.True( parameters[ 1 ].Subparameters.Span[ 0 ].IsEmpty );
		Assert.True( parameters[ 1 ].Subparameters.Span[ 1 ].IsEmpty );
		Assert.Equal( 3, parameters[ 2 ].Subparameters.Length );
		Assert.Equal( "1", AsAscii( parameters[ 2 ].Subparameters.Span[ 0 ].RawBytes ) );
		Assert.True( parameters[ 2 ].Subparameters.Span[ 1 ].IsEmpty );
		Assert.Equal( "2", AsAscii( parameters[ 2 ].Subparameters.Span[ 2 ].RawBytes ) );
		Assert.Equal( "0", AsAscii( parameters[ 3 ].RawBytes ) );
	}

	[Theory]
	[InlineData( 0x18 )]
	[InlineData( 0x1A )]
	public void CancellationBytesInvalidateCsiFraming(
		int cancellationByte
	) {
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);

		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, scanner.Feed( 0x1B ) );
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, scanner.Feed( (byte)'[' ) );
		Assert.Equal( TerminalResponseFrameParseStatus.Incomplete, scanner.Feed( (byte)'1' ) );
		Assert.Equal(
			TerminalResponseFrameParseStatus.Invalid,
			scanner.Feed( checked( (byte)cancellationByte ) )
		);
	}

	[Fact]
	public async Task EveryGeometryResponseSplitPointCompletesThroughSessionQueryPath() {
		byte[] response = Encoding.ASCII.GetBytes( "\u001b[4;800;1200t" );
		HardeningTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		for ( int split = 1; split < response.Length; ++split ) {
			Task<TerminalPixelSize> query = session.QueryTerminalPixelSizeAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
			await WaitForWriteCountAsync(
				transport,
				split
			);

			transport.Publish( response[ ..split ] );
			Assert.False( query.IsCompleted );
			transport.Publish( response[ split.. ] );

			TerminalPixelSize result = await query;
			Assert.Equal( 1200, result.Width );
			Assert.Equal( 800, result.Height );
		}

		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	[Fact]
	public async Task MalformedCorrelatedGeometryResponseDoesNotPoisonNextQuery() {
		HardeningTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPixelSize> malformed = session.QueryTerminalPixelSizeAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[4;0;1200t" )
		);
		await Assert.ThrowsAsync<FormatException>( () => malformed );

		Task<TerminalPixelSize> valid = session.QueryCellPixelSizeAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[6;20;10t" )
		);

		TerminalPixelSize result = await valid;
		Assert.Equal( 10, result.Width );
		Assert.Equal( 20, result.Height );
	}

	private static TerminalCsiSyntax ParseSyntax(
		string wire
	) {
		ArgumentNullException.ThrowIfNull( wire );
		return TerminalCsiSyntax.Parse(
			new TerminalResponseFrame(
				TerminalResponseFrameKind.Csi,
				Encoding.ASCII.GetBytes( wire )
			)
		);
	}

	private static string AsAscii(
		ReadOnlyMemory<byte> bytes
	) {
		return Encoding.ASCII.GetString( bytes.Span );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		HardeningTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			new HardeningTerminalControlProvider(),
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
		HardeningTransport transport,
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

	private sealed class HardeningTerminalControlProvider : ITerminalControlProvider {
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
			return TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 120, 40 )
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

	private sealed class HardeningTransport : ITerminalInput, ITerminalOutput {
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

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( buffer.IsEmpty ) {
				return 0;
			}

			int active = Interlocked.Increment( ref this.activeReads );
			RecordMaximum(
				ref this.maximumConcurrentReads,
				active
			);
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

		private static void RecordMaximum(
			ref int target,
			int candidate
		) {
			while ( true ) {
				int observed = Volatile.Read( ref target );
				if ( candidate <= observed ) {
					return;
				}
				if ( observed == Interlocked.CompareExchange(
					ref target,
					candidate,
					observed
				) ) {
					return;
				}
			}
		}
	}
}
