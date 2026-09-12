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
/// Verifies T125 boundary, acknowledgement, and stale-lifecycle semantics for advanced placements.
/// </summary>
public sealed class TerminalPersistentRasterAdvancedHardeningTests {
	[Fact]
	public async Task ExactEdgeGeometryCarriesBothZIndexExtrema() {
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			4,
			3
		);
		await using ( resource ) {
			TerminalControlResult<TerminalRasterPlacement> creation =
				await resource.CreatePlacementAsync(
					new TerminalRasterPlacementOptions {
						SourceRectangle = new TerminalRasterSourceRectangle(
							1,
							1,
							3,
							2
						),
						Columns = 4,
						Rows = 3,
						ZIndex = int.MaxValue
					}
				);
			TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
				creation.Value
			);
			await using ( placement ) {
				Assert.Equal(
					Encoding.ASCII.GetBytes(
						"\u001b_Ga=p,i=1001,p=1,C=1,x=1,y=1,w=3,h=2,c=4,r=3,z=2147483647\u001b\\"
					),
					transport.Writes[ 1 ]
				);

				TerminalControlMutationResult update = await placement.UpdateAsync(
					new TerminalRasterPlacementOptions {
						SourceRectangle = new TerminalRasterSourceRectangle(
							0,
							0,
							4,
							3
						),
						Columns = 1,
						Rows = 1,
						ZIndex = int.MinValue
					}
				);

				Assert.True( update.Succeeded );
				Assert.Equal(
					Encoding.ASCII.GetBytes(
						"\u001b_Ga=p,i=1001,p=1,C=1,x=0,y=0,w=4,h=3,c=1,r=1,z=-2147483648\u001b\\"
					),
					transport.Writes[ 2 ]
				);
			}
		}
	}

	[Fact]
	public async Task InvalidCreateAndUpdateRectanglesProduceNoOutput() {
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			4,
			3
		);
		await using ( resource ) {
			int beforeInvalidCreate = transport.Writes.Count;
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
				async () => await resource.CreatePlacementAsync(
					new TerminalRasterPlacementOptions {
						SourceRectangle = new TerminalRasterSourceRectangle(
							2,
							0,
							3,
							3
						)
					}
				)
			);
			Assert.Equal( beforeInvalidCreate, transport.Writes.Count );

			TerminalControlResult<TerminalRasterPlacement> creation =
				await resource.CreatePlacementAsync(
					new TerminalRasterPlacementOptions {
						SourceRectangle = new TerminalRasterSourceRectangle(
							0,
							0,
							4,
							3
						),
						ZIndex = 0
					}
				);
			TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
				creation.Value
			);
			await using ( placement ) {
				int beforeInvalidUpdate = transport.Writes.Count;
				await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
					async () => await placement.UpdateAsync(
						new TerminalRasterPlacementOptions {
							SourceRectangle = new TerminalRasterSourceRectangle(
								0,
								2,
								4,
								2
							),
							ZIndex = 1
						}
					)
				);
				Assert.Equal( beforeInvalidUpdate, transport.Writes.Count );
			}
		}
	}

	[Fact]
	public async Task AdvancedGeometryPreservesAcknowledgementIdentityAndFailureSemantics() {
		AcknowledgingTransport transport = new() {
			AutoAcknowledgePlacements = false
		};
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			1,
			1
		);

		TerminalRasterPlacementOptions createOptions = new() {
			SourceRectangle = new TerminalRasterSourceRectangle(
				0,
				0,
				1,
				1
			),
			ZIndex = int.MaxValue
		};
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreatePlacementAsync( createOptions ).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.False( creation.IsCompleted );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=999,p=1;OK\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=1001,p=2;OK\u001b\\" )
		);
		Assert.False( creation.IsCompleted );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=1001,p=1;OK\u001b\\" )
		);
		TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
			( await creation ).Value
		);

		Task<TerminalControlMutationResult> malformedUpdate = placement.UpdateAsync(
			new TerminalRasterPlacementOptions {
				SourceRectangle = new TerminalRasterSourceRectangle(
					0,
					0,
					1,
					1
				),
				ZIndex = int.MinValue
			}
		).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=1001,p=1,p=1;OK\u001b\\" )
		);
		await Assert.ThrowsAsync<FormatException>( () => malformedUpdate );

		Task<TerminalControlMutationResult> missingUpdate = placement.UpdateAsync(
			new TerminalRasterPlacementOptions {
				SourceRectangle = new TerminalRasterSourceRectangle(
					0,
					0,
					1,
					1
				),
				ZIndex = -7
			}
		).AsTask();
		await transport.WaitForWriteCountAsync( 4 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=1001,p=1;ENOENT:synthetic missing image\u001b\\"
			)
		);
		TerminalControlMutationResult missingResult = await missingUpdate;
		Assert.Equal( TerminalControlStatus.Unavailable, missingResult.Status );

		int baselineWrites = transport.Writes.Count;
		Assert.Equal(
			TerminalControlStatus.Unavailable,
			( await placement.UpdateAsync( createOptions ) ).Status
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
		await placement.DisposeAsync();
		await resource.DisposeAsync();
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task AdvancedPlacementInvalidationAndStaleDisposalRemainLocalOnly() {
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			4,
			3
		);
		TerminalControlResult<TerminalRasterPlacement> creation =
			await resource.CreatePlacementAsync(
				new TerminalRasterPlacementOptions {
					SourceRectangle = new TerminalRasterSourceRectangle(
						1,
						1,
						3,
						2
					),
					Columns = 2,
					Rows = 2,
					ZIndex = 9
				}
			);
		TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
			creation.Value
		);
		int baselineWrites = transport.Writes.Count;

		session.InvalidateState();
		TerminalControlMutationResult update = await placement.UpdateAsync(
			new TerminalRasterPlacementOptions {
				SourceRectangle = new TerminalRasterSourceRectangle(
					0,
					0,
					4,
					3
				),
				ZIndex = -9
			}
		);
		Assert.Equal( TerminalControlStatus.Unavailable, update.Status );
		Assert.Equal( baselineWrites, transport.Writes.Count );

		await placement.DisposeAsync();
		await resource.DisposeAsync();
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	private static TerminalRasterImage CreateImage(
		int width,
		int height
	) {
		if ( 1 > width ) {
			throw new ArgumentOutOfRangeException( nameof( width ) );
		}
		if ( 1 > height ) {
			throw new ArgumentOutOfRangeException( nameof( height ) );
		}

		return TerminalRasterImage.CreateRgb24(
			width,
			height,
			new byte[ checked( width * height * 3 ) ]
		);
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		int width,
		int height
	) {
		ArgumentNullException.ThrowIfNull( session );
		TerminalControlResult<TerminalRasterResource> result =
			await session.CreateRasterResourceAsync(
				CreateImage(
					width,
					height
				)
			);
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		AcknowledgingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		TerminalSession session = await TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				MonotonicClock = new FrozenMonotonicClock(),
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		return session;
	}

	private sealed class AcknowledgingTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly object synchronization = new();
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly List<byte[]> writes = [];

		internal bool AutoAcknowledgePlacements {
			get;
			init;
		} = true;

		internal IReadOnlyList<byte[]> Writes {
			get {
				lock ( this.synchronization ) {
					return this.writes.Select(
						static value => value.ToArray()
					).ToArray();
				}
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
					"The scripted response exceeds the terminal input buffer."
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
			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			this.PublishAutomaticAcknowledgement( buffer.Span );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		internal void Publish(
			byte[] value
		) {
			ArgumentNullException.ThrowIfNull( value );
			if ( !this.input.Writer.TryWrite( value.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel rejected a response."
				);
			}
		}

		internal async Task WaitForWriteCountAsync(
			int count
		) {
			if ( 0 > count ) {
				throw new ArgumentOutOfRangeException( nameof( count ) );
			}

			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
			while ( this.Writes.Count < count ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}

		private void PublishAutomaticAcknowledgement(
			ReadOnlySpan<byte> frame
		) {
			string text = Encoding.ASCII.GetString( frame );
			if ( text.StartsWith(
				"\u001b_Ga=t,",
				StringComparison.Ordinal
			) && TryReadIdentityField(
				text,
				",I=",
				out uint imageNumber
			) ) {
				this.Publish(
					Encoding.ASCII.GetBytes(
						$"\u001b_Gi={1000u + imageNumber},I={imageNumber};OK\u001b\\"
					)
				);
				return;
			}

			if ( this.AutoAcknowledgePlacements
				&& text.StartsWith(
					"\u001b_Ga=p,",
					StringComparison.Ordinal
				) && TryReadIdentityField(
					text,
					",i=",
					out uint imageId
				) && TryReadIdentityField(
					text,
					",p=",
					out uint placementId
				) ) {
				this.Publish(
					Encoding.ASCII.GetBytes(
						$"\u001b_Gi={imageId},p={placementId};OK\u001b\\"
					)
				);
			}
		}

		private static bool TryReadIdentityField(
			string text,
			string marker,
			out uint value
		) {
			ArgumentNullException.ThrowIfNull( text );
			ArgumentException.ThrowIfNullOrEmpty( marker );
			value = 0u;

			int start = text.IndexOf(
				marker,
				StringComparison.Ordinal
			);
			if ( 0 > start ) {
				return false;
			}
			start += marker.Length;
			int end = start;
			while ( end < text.Length
				&& text[ end ] is >= '0' and <= '9' ) {
				++end;
			}
			return start < end
				&& uint.TryParse(
					text.AsSpan(
						start,
						end - start
					),
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out value
				)
			;
		}
	}
}
