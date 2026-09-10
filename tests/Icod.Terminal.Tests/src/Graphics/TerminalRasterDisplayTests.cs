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
namespace Icod.Terminal.Tests.Graphics;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the D178 backend-neutral public raster display operation.
/// </summary>
public sealed class TerminalRasterDisplayTests {
	private static readonly byte[] PrimaryDeviceAttributesRequest =
		Encoding.ASCII.GetBytes( "\u001b[c" );

	[Fact]
	public async Task VerifiedSixelDisplayEmitsCanonicalFrameAndReturnsSuccess() {
		RasterTransport transport = new( "\u001b[?64;4c" );
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			2,
			1,
			[
				0, 0, 0,
				0, 0, 0
			]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.Equal( 1, transport.PrimaryDeviceAttributesRequestCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001bP0;1;0q\"1;1;2;1#0;2;0;0;0#0@@\u001b\\"
			),
			transport.GetGraphicsBytes()
		);
	}

	[Fact]
	public async Task MissingSixelAttributeReturnsUnavailableWithoutGraphicsOutput() {
		RasterTransport transport = new( "\u001b[?64;1;2c" );
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 0, 0, 0 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.False( result.Succeeded );
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal( 1, transport.PrimaryDeviceAttributesRequestCount );
		Assert.Empty( transport.GetGraphicsBytes() );
	}

	[Fact]
	public async Task FractionalAlphaReturnsUnsupportedBeforeGraphicsCommit() {
		RasterTransport transport = new( "\u001b[?64;4c" );
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgba32(
			1,
			1,
			[ 1, 2, 3, 128 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.False( result.Succeeded );
		Assert.Equal( TerminalControlStatus.Unsupported, result.Status );
		Assert.Equal( 1, transport.PrimaryDeviceAttributesRequestCount );
		Assert.Empty( transport.GetGraphicsBytes() );
	}

	[Fact]
	public async Task PreCancelledDisplayDoesNotProbeOrWriteGraphics() {
		RasterTransport transport = new( "\u001b[?64;4c" );
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 0, 0, 0 ]
		);
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.DisplayRasterAsync(
				image,
				cancellation.Token
			).AsTask()
		);

		Assert.Equal( 0, transport.PrimaryDeviceAttributesRequestCount );
		Assert.Empty( transport.GetGraphicsBytes() );
	}

	[Fact]
	public async Task ExistingVerifiedEvidenceAvoidsDuplicateProbe() {
		RasterTransport transport = new( "\u001b[?64;4c" );
		await using TerminalSession session = await OpenSessionAsync( transport );
		_ = await session.QueryPrimaryDeviceAttributesAsync(
			TimeSpan.FromSeconds( 30 )
		);
		TerminalRasterImage image = TerminalRasterImage.CreateIndexed8(
			1,
			1,
			[ 0 ],
			[ new TerminalRasterColor( 255, 0, 0 ) ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal( 1, transport.PrimaryDeviceAttributesRequestCount );
		Assert.NotEmpty( transport.GetGraphicsBytes() );
	}

	[Fact]
	public void PublicRasterFactoriesSnapshotCallerStorage() {
		byte[] pixels = [ 10, 20, 30, 40, 50, 60 ];
		TerminalRasterColor[] palette = [
			new TerminalRasterColor( 1, 2, 3 ),
			new TerminalRasterColor( 4, 5, 6, 7 )
		];
		byte[] indices = [ 1 ];

		TerminalRasterImage rgb = TerminalRasterImage.CreateRgb24(
			2,
			1,
			pixels
		);
		TerminalRasterImage indexed = TerminalRasterImage.CreateIndexed8(
			1,
			1,
			indices,
			palette
		);

		pixels[ 0 ] = 255;
		indices[ 0 ] = 0;
		palette[ 1 ] = new TerminalRasterColor( 9, 9, 9 );

		Assert.Equal( TerminalRasterPixelFormat.Rgb24, rgb.PixelFormat );
		Assert.Equal( 2, rgb.Width );
		Assert.Equal( 1, rgb.Height );
		Assert.Equal( 2, rgb.PixelCount );
		Assert.Equal( new TerminalRasterColor( 10, 20, 30 ), rgb.GetPixelColor( 0, 0 ) );
		Assert.Equal( new TerminalRasterColor( 4, 5, 6, 7 ), indexed.GetPixelColor( 0, 0 ) );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RasterTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = new TerminalDescriptionBuilder( "raster-display-test" ).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class RasterTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly byte[] primaryDeviceAttributesResponse;
		private readonly List<byte[]> graphicsWrites = [];
		private int primaryDeviceAttributesRequestCount;

		internal RasterTransport(
			string primaryDeviceAttributesResponse
		) {
			ArgumentException.ThrowIfNullOrWhiteSpace( primaryDeviceAttributesResponse );
			this.primaryDeviceAttributesResponse = Encoding.ASCII.GetBytes(
				primaryDeviceAttributesResponse
			);
		}

		internal int PrimaryDeviceAttributesRequestCount {
			get {
				return Volatile.Read( ref this.primaryDeviceAttributesRequestCount );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] bytes = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( bytes.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted terminal response exceeds the decoder read buffer."
				);
			}

			bytes.AsSpan().CopyTo( buffer.Span );
			return bytes.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( buffer.Span.EndsWith( PrimaryDeviceAttributesRequest ) ) {
				Interlocked.Increment( ref this.primaryDeviceAttributesRequestCount );
				if ( !this.input.Writer.TryWrite(
					this.primaryDeviceAttributesResponse.ToArray()
				) ) {
					throw new InvalidOperationException(
						"The scripted terminal input channel is closed."
					);
				}
				return ValueTask.CompletedTask;
			}

			lock ( this.sync ) {
				this.graphicsWrites.Add( buffer.ToArray() );
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		internal byte[] GetGraphicsBytes() {
			lock ( this.sync ) {
				return this.graphicsWrites
					.SelectMany( static write => write )
					.ToArray();
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
				"Live size is not required by this test."
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
