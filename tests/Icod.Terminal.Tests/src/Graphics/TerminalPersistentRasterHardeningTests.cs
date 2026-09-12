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
/// Defines the C118 repeated ownership-cycle and generation-invalidation hardening contract.
/// </summary>
public sealed class TerminalPersistentRasterHardeningTests {
	private const int OwnershipCycleCount = 24;
	private const int GenerationInvalidationCount = 64;
	private const int RegistryChurnCount = 8192;

	[Fact]
	public async Task RepeatedCreatePlaceUpdateDeleteCyclesRemainUsableAndIdempotent() {
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		for ( int cycle = 0; cycle < OwnershipCycleCount; ++cycle ) {
			TerminalControlResult<TerminalRasterResource> resourceResult =
				await session.CreateRasterResourceAsync( CreateSmallImage() );
			Assert.Equal( TerminalControlStatus.Available, resourceResult.Status );
			TerminalRasterResource resource = Assert.IsType<TerminalRasterResource>(
				resourceResult.Value
			);

			TerminalControlResult<TerminalRasterPlacement> placementResult =
				await resource.CreatePlacementAsync(
					new TerminalRasterPlacementOptions {
						Columns = 3,
						Rows = 2
					}
				);
			Assert.Equal( TerminalControlStatus.Available, placementResult.Status );
			TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
				placementResult.Value
			);

			TerminalControlMutationResult update = await placement.UpdateAsync(
				new TerminalRasterPlacementOptions {
					Columns = 2,
					Rows = 1
				}
			);
			Assert.True( update.Succeeded );

			await placement.DisposeAsync();
			int afterPlacementDispose = transport.Writes.Count;
			await placement.DisposeAsync();
			Assert.Equal( afterPlacementDispose, transport.Writes.Count );

			await resource.DisposeAsync();
			int afterResourceDispose = transport.Writes.Count;
			await resource.DisposeAsync();
			Assert.Equal( afterResourceDispose, transport.Writes.Count );
		}

		Assert.Equal(
			OwnershipCycleCount * 5,
			transport.Writes.Count
		);
	}

	[Fact]
	public async Task RepeatedGenerationInvalidationNeverReplaysOrRevivesStaleHandles() {
		AcknowledgingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalControlResult<TerminalRasterResource> resourceResult =
			await session.CreateRasterResourceAsync( CreateSmallImage() );
		TerminalRasterResource resource = Assert.IsType<TerminalRasterResource>(
			resourceResult.Value
		);
		TerminalControlResult<TerminalRasterPlacement> placementResult =
			await resource.CreatePlacementAsync();
		TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
			placementResult.Value
		);
		int baselineWrites = transport.Writes.Count;

		for ( int iteration = 0; iteration < GenerationInvalidationCount; ++iteration ) {
			session.InvalidateState();

			Assert.Equal(
				TerminalControlStatus.Unavailable,
				( await placement.UpdateAsync() ).Status
			);
			Assert.Equal(
				TerminalControlStatus.Unavailable,
				( await resource.CreatePlacementAsync() ).Status
			);
			Assert.Equal( baselineWrites, transport.Writes.Count );
		}

		await placement.DisposeAsync();
		await resource.DisposeAsync();
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public void RegistryChurnReleasesEveryPlacementAndAvoidsLiveIdentityReuse() {
		TerminalPersistentRasterRegistry registry = new();
		Assert.True(
			registry.TryReserveResource(
				out TerminalPersistentRasterResourceState? resource
			)
		);
		Assert.NotNull( resource );
		HashSet<uint> identities = [];

		for ( int iteration = 0; iteration < RegistryChurnCount; ++iteration ) {
			Assert.True(
				registry.TryReservePlacement(
					resource,
					out TerminalPersistentRasterPlacementState? placement
				)
			);
			Assert.NotNull( placement );
			Assert.True( identities.Add( placement.PlacementId ) );
			Assert.True( registry.TryReleasePlacement( placement ) );
			Assert.Equal( 0, registry.LivePlacementCount );
		}

		Assert.Equal( RegistryChurnCount, identities.Count );
		Assert.True( registry.TryReleaseResource( resource ) );
		Assert.Equal( 0, registry.LiveResourceCount );
		Assert.Equal( 0, registry.LivePlacementCount );
	}

	private static TerminalRasterImage CreateSmallImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
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
			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.PublishAcknowledgement( buffer.Span );
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
					out value
				)
			;
		}
	}
}
