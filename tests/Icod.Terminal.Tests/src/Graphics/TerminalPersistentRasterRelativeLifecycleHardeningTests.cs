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
/// Verifies T137 repeated relative-placement lifecycle and serialized concurrency hardening.
/// </summary>
public sealed class TerminalPersistentRasterRelativeLifecycleHardeningTests {
	private const int OwnershipCycleCount = 24;

	[Fact]
	public async Task RepeatedRelativeCreateUpdateDeleteCyclesRemainUsableWithSignedExtrema() {
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource parentResource = await CreateResourceAsync( session );
		await using ( parentResource ) {
			TerminalRasterPlacement parent = await CreatePlacementAsync( parentResource );
			await using ( parent ) {
				for ( int cycle = 0; cycle < OwnershipCycleCount; ++cycle ) {
					TerminalRasterResource childResource = await CreateResourceAsync( session );
					int createColumn = 0 == cycle % 2
						? int.MinValue
						: int.MaxValue
					;
					int createRow = 0 == cycle % 2
						? int.MaxValue
						: int.MinValue
					;
					TerminalControlResult<TerminalRasterPlacement> creation =
						await childResource.CreateRelativePlacementAsync(
							parent,
							createColumn,
							createRow,
							new TerminalRasterPlacementOptions {
								SourceRectangle = new TerminalRasterSourceRectangle(
									0,
									0,
									1,
									1
								),
								Columns = 2,
								Rows = 1,
								ZIndex = -cycle - 1
							}
						);
					Assert.Equal( TerminalControlStatus.Available, creation.Status );
					TerminalRasterPlacement child = Assert.IsType<TerminalRasterPlacement>(
						creation.Value
					);
					int updateColumn = createRow;
					int updateRow = createColumn;
					TerminalControlMutationResult update = await child.UpdateRelativeAsync(
						updateColumn,
						updateRow,
						new TerminalRasterPlacementOptions {
							SourceRectangle = new TerminalRasterSourceRectangle(
								0,
								0,
								1,
								1
							),
							Columns = 1,
							Rows = 2,
							ZIndex = cycle + 1
						}
					);
					Assert.True( update.Succeeded );
					Assert.Equal( updateColumn, child.State.ColumnOffset );
					Assert.Equal( updateRow, child.State.RowOffset );
					Assert.Same( parent.State, child.State.Parent );

					await child.DisposeAsync();
					int afterChildDispose = transport.Writes.Count;
					await child.DisposeAsync();
					Assert.Equal( afterChildDispose, transport.Writes.Count );

					await childResource.DisposeAsync();
					int afterResourceDispose = transport.Writes.Count;
					await childResource.DisposeAsync();
					Assert.Equal( afterResourceDispose, transport.Writes.Count );
				}
			}
		}
	}

	[Fact]
	public async Task ConcurrentRelativeCreateUpdateDisposeUsesSerializedAcknowledgedOwnership() {
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource parentResource = await CreateResourceAsync( session );
		await using ( parentResource ) {
			TerminalRasterPlacement parent = await CreatePlacementAsync( parentResource );
			await using ( parent ) {
				TerminalRasterResource childResource = await CreateResourceAsync( session );
				await using ( childResource ) {
					Task<TerminalControlResult<TerminalRasterPlacement>>[] creates = Enumerable.Range(
						1,
						16
					).Select(
						index => childResource.CreateRelativePlacementAsync(
							parent,
							index,
							-index
						).AsTask()
					).ToArray();
					TerminalControlResult<TerminalRasterPlacement>[] createResults = await Task.WhenAll(
						creates
					);
					TerminalRasterPlacement[] children = createResults.Select(
						static result => {
							Assert.Equal( TerminalControlStatus.Available, result.Status );
							return Assert.IsType<TerminalRasterPlacement>( result.Value );
						}
					).ToArray();

					Task<TerminalControlMutationResult>[] updates = children.Select(
						( child, index ) => child.UpdateRelativeAsync(
							-int.MaxValue + index,
							int.MaxValue - index
						).AsTask()
					).ToArray();
					TerminalControlMutationResult[] updateResults = await Task.WhenAll( updates );
					Assert.All(
						updateResults,
						static result => Assert.True( result.Succeeded )
					);

					await Task.WhenAll(
						children.Select(
							static child => child.DisposeAsync().AsTask()
						)
					);
					Assert.All(
						children,
						static child => Assert.False( child.State.IsClosed is false )
					);
				}
			}
		}
	}

	private static TerminalRasterImage CreateSmallImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		TerminalControlResult<TerminalRasterResource> result =
			await session.CreateRasterResourceAsync( CreateSmallImage() );
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async Task<TerminalRasterPlacement> CreatePlacementAsync(
		TerminalRasterResource resource
	) {
		ArgumentNullException.ThrowIfNull( resource );
		TerminalControlResult<TerminalRasterPlacement> result =
			await resource.CreatePlacementAsync();
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlacement>( result.Value );
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
		private readonly List<byte[]> writes = [];

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
			byte[] frame = buffer.ToArray();
			lock ( this.synchronization ) {
				this.writes.Add( frame );
			}
			this.PublishAcknowledgement( frame );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		private void PublishAcknowledgement(
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

			if ( text.StartsWith(
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

		private void Publish(
			byte[] value
		) {
			ArgumentNullException.ThrowIfNull( value );
			if ( !this.input.Writer.TryWrite( value.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel rejected a response."
				);
			}
		}

		private static bool TryReadIdentityField(
			string text,
			string marker,
			out uint value
		) {
			int markerIndex = text.IndexOf(
				marker,
				StringComparison.Ordinal
			);
			if ( 0 > markerIndex ) {
				value = 0;
				return false;
			}
			int valueStart = markerIndex + marker.Length;
			int valueEnd = valueStart;
			while ( valueEnd < text.Length && char.IsAsciiDigit( text[ valueEnd ] ) ) {
				++valueEnd;
			}
			return uint.TryParse(
				text.AsSpan( valueStart, valueEnd - valueStart ),
				out value
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
				"No scripted live size."
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
