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
/// Verifies that the default source-rectangle value cannot bypass intrinsic geometry validation.
/// </summary>
public sealed class TerminalRasterDefaultSourceRectangleTests {
	[Fact]
	public void ResourceAwareValidationRejectsDefaultRectangle() {
		TerminalRasterPlacementOptions options = new() {
			SourceRectangle = default( TerminalRasterSourceRectangle )
		};

		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => options.Validate(
				4,
				3
			)
		);
	}

	[Fact]
	public void EncoderRejectsDefaultRectangle() {
		TerminalRasterPlacementOptions options = new() {
			SourceRectangle = default( TerminalRasterSourceRectangle )
		};

		_ = Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentEncoder.EncodePlacementPayload(
				imageId: 99,
				placementId: 7,
				options
			)
		);
	}

	[Fact]
	public async Task PublicCreateAndUpdateRejectDefaultRectangleBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24(
			2,
			2,
			new byte[ 2 * 2 * 3 ]
		);

		Task<TerminalControlResult<TerminalRasterResource>> resourceCreation =
			session.CreateRasterResourceAsync( image ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;OK\u001b\\" )
		);
		TerminalRasterResource resource = Assert.IsType<TerminalRasterResource>(
			( await resourceCreation ).Value
		);
		await using ( resource ) {
			int beforeInvalidCreate = transport.Writes.Count;
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
				async () => await resource.CreatePlacementAsync(
					new TerminalRasterPlacementOptions {
						SourceRectangle = default( TerminalRasterSourceRectangle )
					}
				)
			);
			Assert.Equal( beforeInvalidCreate, transport.Writes.Count );

			Task<TerminalControlResult<TerminalRasterPlacement>> placementCreation =
				resource.CreatePlacementAsync(
					new TerminalRasterPlacementOptions {
						SourceRectangle = new TerminalRasterSourceRectangle(
							0,
							0,
							1,
							1
						)
					}
				).AsTask();
			await transport.WaitForWriteCountAsync( 2 );
			transport.Publish(
				Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=1;OK\u001b\\" )
			);
			TerminalRasterPlacement placement = Assert.IsType<TerminalRasterPlacement>(
				( await placementCreation ).Value
			);
			await using ( placement ) {
				int beforeInvalidUpdate = transport.Writes.Count;
				await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
					async () => await placement.UpdateAsync(
						new TerminalRasterPlacementOptions {
							SourceRectangle = default( TerminalRasterSourceRectangle )
						}
					)
				);
				Assert.Equal( beforeInvalidUpdate, transport.Writes.Count );
			}
		}
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
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

	private sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
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
	}
}
