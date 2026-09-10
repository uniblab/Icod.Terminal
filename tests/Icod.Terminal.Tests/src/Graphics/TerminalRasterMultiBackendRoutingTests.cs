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

using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies A186 evidence-driven routing between Kitty Graphics and Sixel.
/// </summary>
public sealed class TerminalRasterMultiBackendRoutingTests {
	private static readonly byte[] PrimaryDeviceAttributesRequest =
		Encoding.ASCII.GetBytes( "\u001b[c" );

	[Fact]
	public async Task VerifiedKittyIsPreferredWhenBothBackendsAreVerified() {
		RoutingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		RecordVerified( session, TerminalProtocolBackend.DcsSixel );
		RecordVerified( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal( 0, transport.KittyProbeRequestCount );
		Assert.Equal( 0, transport.DirectPrimaryDaRequestCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=T,f=24,s=1,v=1,t=d,m=0,q=2;AQID\u001b\\"
			),
			Assert.Single( transport.GetGraphicsWrites() )
		);
	}

	[Fact]
	public async Task VerifiedSixelDoesNotProbeUnknownKittyBackend() {
		RoutingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		RecordVerified( session, TerminalProtocolBackend.DcsSixel );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 0, 0, 0 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal( 0, transport.KittyProbeRequestCount );
		Assert.Equal( 0, transport.DirectPrimaryDaRequestCount );
		AssertSixelTransfer( transport.GetGraphicsWrites() );
	}

	[Fact]
	public async Task UnknownBackendsProbeKittyFirstAndUseVerifiedKitty() {
		RoutingTransport transport = new(
			replyToKittyProbe: true,
			compoundPrimaryDaResponse: "\u001b[?64;1c"
		);
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal( 1, transport.KittyProbeRequestCount );
		Assert.Equal( 0, transport.DirectPrimaryDaRequestCount );
		Assert.Equal(
			TerminalCapabilitySupportState.Verified,
			ResolveEvidence(
				session,
				TerminalProtocolBackend.ApcKittyGraphics
			).State
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=T,f=24,s=1,v=1,t=d,m=0,q=2;AQID\u001b\\"
			),
			Assert.Single( transport.GetGraphicsWrites() )
		);
	}

	[Fact]
	public async Task DaFirstCanVerifySixelAndFallbackWithoutSecondProbe() {
		RoutingTransport transport = new(
			compoundPrimaryDaResponse: "\u001b[?64;4c"
		);
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 0, 0, 0 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal( 1, transport.KittyProbeRequestCount );
		Assert.Equal( 0, transport.DirectPrimaryDaRequestCount );
		Assert.Equal(
			TerminalCapabilitySupportState.Unsupported,
			ResolveEvidence(
				session,
				TerminalProtocolBackend.ApcKittyGraphics
			).State
		);
		Assert.Equal(
			TerminalCapabilitySupportState.Verified,
			ResolveEvidence(
				session,
				TerminalProtocolBackend.DcsSixel
			).State
		);
		AssertSixelTransfer( transport.GetGraphicsWrites() );
	}

	[Fact]
	public async Task DaFirstWithoutSixelEvidenceReturnsUnavailableWithoutSecondProbe() {
		RoutingTransport transport = new(
			compoundPrimaryDaResponse: "\u001b[?64;1;2c"
		);
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 0, 0, 0 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.False( result.Succeeded );
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal( 1, transport.KittyProbeRequestCount );
		Assert.Equal( 0, transport.DirectPrimaryDaRequestCount );
		Assert.Empty( transport.GetGraphicsWrites() );
	}

	[Fact]
	public async Task KnownKittyUnsupportedStateProbesSixelDirectly() {
		RoutingTransport transport = new(
			directPrimaryDaResponse: "\u001b[?64;4c"
		);
		await using TerminalSession session = await OpenSessionAsync( transport );
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 0, 0, 0 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal( 0, transport.KittyProbeRequestCount );
		Assert.Equal( 1, transport.DirectPrimaryDaRequestCount );
		AssertSixelTransfer( transport.GetGraphicsWrites() );
	}

	[Fact]
	public async Task FractionalAlphaIsPreservedWhenKittyIsVerified() {
		RoutingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		RecordVerified( session, TerminalProtocolBackend.DcsSixel );
		RecordVerified( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalRasterImage image = TerminalRasterImage.CreateRgba32(
			1,
			1,
			[ 1, 2, 3, 128 ]
		);

		TerminalControlMutationResult result = await session.DisplayRasterAsync( image );

		Assert.True( result.Succeeded );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=T,f=32,s=1,v=1,t=d,m=0,q=2;AQIDgA==\u001b\\"
			),
			Assert.Single( transport.GetGraphicsWrites() )
		);
	}

	[Fact]
	public async Task KittyTransportFailureDoesNotFallbackToVerifiedSixel() {
		RoutingTransport transport = new(
			failFirstGraphicsWrite: true
		);
		await using TerminalSession session = await OpenSessionAsync( transport );
		RecordVerified( session, TerminalProtocolBackend.DcsSixel );
		RecordVerified( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.DisplayRasterAsync( image ).AsTask()
		);

		IReadOnlyList<byte[]> writes = transport.GetGraphicsWrites();
		Assert.Single( writes );
		AssertKittyFrame( writes[ 0 ] );
		Assert.DoesNotContain(
			writes,
			static write => 2 <= write.Length
				&& 0x1B == write[ 0 ]
				&& (byte)'P' == write[ 1 ]
		);
	}

	private static void RecordVerified(
		TerminalSession session,
		TerminalProtocolBackend backend
	) {
		ArgumentNullException.ThrowIfNull( session );
		session.RecordSemanticBackendEvidence(
			backend,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
	}

	private static TerminalCapabilityResolution ResolveEvidence(
		TerminalSession session,
		TerminalProtocolBackend backend
	) {
		ArgumentNullException.ThrowIfNull( session );
		return session.GetSemanticCapabilityEvidence().Resolve(
			TerminalCapabilitySubject.ForProtocolBackend( backend )
		);
	}

	private static void AssertKittyFrame(
		byte[] bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		Assert.True( 4 <= bytes.Length );
		Assert.Equal( 0x1B, bytes[ 0 ] );
		Assert.Equal( (byte)'_', bytes[ 1 ] );
		Assert.Equal( (byte)'G', bytes[ 2 ] );
		Assert.Equal( 0x1B, bytes[ ^2 ] );
		Assert.Equal( (byte)'\\', bytes[ ^1 ] );
	}

	private static void AssertSixelTransfer(
		IReadOnlyList<byte[]> writes
	) {
		ArgumentNullException.ThrowIfNull( writes );
		Assert.NotEmpty( writes );
		AssertSixelFrame(
			writes
				.SelectMany( static write => write )
				.ToArray()
		);
	}

	private static void AssertSixelFrame(
		byte[] bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		Assert.True( 4 <= bytes.Length );
		Assert.Equal( 0x1B, bytes[ 0 ] );
		Assert.Equal( (byte)'P', bytes[ 1 ] );
		Assert.Equal( 0x1B, bytes[ ^2 ] );
		Assert.Equal( (byte)'\\', bytes[ ^1 ] );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RoutingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = new TerminalDescriptionBuilder(
					"raster-multi-backend-test"
				).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class RoutingTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly bool replyToKittyProbe;
		private readonly byte[]? compoundPrimaryDaResponse;
		private readonly byte[]? directPrimaryDaResponse;
		private readonly bool failFirstGraphicsWrite;
		private readonly List<byte[]> graphicsWrites = [];
		private int kittyProbeRequestCount;
		private int directPrimaryDaRequestCount;

		internal RoutingTransport(
			bool replyToKittyProbe = false,
			string? compoundPrimaryDaResponse = null,
			string? directPrimaryDaResponse = null,
			bool failFirstGraphicsWrite = false
		) {
			this.replyToKittyProbe = replyToKittyProbe;
			this.compoundPrimaryDaResponse = compoundPrimaryDaResponse is null
				? null
				: Encoding.ASCII.GetBytes( compoundPrimaryDaResponse )
			;
			this.directPrimaryDaResponse = directPrimaryDaResponse is null
				? null
				: Encoding.ASCII.GetBytes( directPrimaryDaResponse )
			;
			this.failFirstGraphicsWrite = failFirstGraphicsWrite;
		}

		internal int KittyProbeRequestCount {
			get {
				return Volatile.Read( ref this.kittyProbeRequestCount );
			}
		}

		internal int DirectPrimaryDaRequestCount {
			get {
				return Volatile.Read( ref this.directPrimaryDaRequestCount );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] value = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( value.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted terminal response exceeds the decoder read buffer."
				);
			}

			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			ReadOnlySpan<byte> bytes = buffer.Span;
			if ( IsKittyProbeRequest( bytes ) ) {
				Interlocked.Increment( ref this.kittyProbeRequestCount );
				if ( this.replyToKittyProbe ) {
					uint imageId = GetKittyProbeImageId( bytes );
					this.Publish(
						Encoding.ASCII.GetBytes(
							"\u001b_Gi="
								+ imageId.ToString( CultureInfo.InvariantCulture )
								+ ";OK\u001b\\"
						)
					);
				}
				if ( this.compoundPrimaryDaResponse is not null ) {
					this.Publish( this.compoundPrimaryDaResponse );
				}
				return ValueTask.CompletedTask;
			}
			if ( bytes.SequenceEqual( PrimaryDeviceAttributesRequest ) ) {
				Interlocked.Increment( ref this.directPrimaryDaRequestCount );
				if ( this.directPrimaryDaResponse is not null ) {
					this.Publish( this.directPrimaryDaResponse );
				}
				return ValueTask.CompletedTask;
			}

			byte[] write = buffer.ToArray();
			bool fail;
			lock ( this.sync ) {
				this.graphicsWrites.Add( write );
				fail = this.failFirstGraphicsWrite
					&& 1 == this.graphicsWrites.Count;
			}
			if ( fail ) {
				throw new InvalidOperationException(
					"Synthetic Kitty Graphics transport failure."
				);
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		internal IReadOnlyList<byte[]> GetGraphicsWrites() {
			lock ( this.sync ) {
				return this.graphicsWrites
					.Select( static write => write.ToArray() )
					.ToArray();
			}
		}

		private void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		private static bool IsKittyProbeRequest(
			ReadOnlySpan<byte> bytes
		) {
			return 4 <= bytes.Length
				&& 0x1B == bytes[ 0 ]
				&& (byte)'_' == bytes[ 1 ]
				&& (byte)'G' == bytes[ 2 ]
				&& bytes.EndsWith( PrimaryDeviceAttributesRequest );
		}

		private static uint GetKittyProbeImageId(
			ReadOnlySpan<byte> bytes
		) {
			string request = Encoding.ASCII.GetString( bytes );
			const string prefix = "\u001b_Gi=";
			if ( !request.StartsWith( prefix, StringComparison.Ordinal ) ) {
				throw new InvalidOperationException(
					"The scripted Kitty Graphics request does not contain the expected image id prefix."
				);
			}
			int end = request.IndexOf( ',', prefix.Length );
			if ( 0 > end ) {
				throw new InvalidOperationException(
					"The scripted Kitty Graphics request does not terminate its image id."
				);
			}
			return uint.Parse(
				request.AsSpan(
					prefix.Length,
					end - prefix.Length
				),
				NumberStyles.None,
				CultureInfo.InvariantCulture
			);
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
