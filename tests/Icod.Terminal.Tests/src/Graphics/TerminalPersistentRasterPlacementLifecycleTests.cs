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

using System.Reflection;
using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Defines the C116 placement-update and deterministic-cleanup contract.
/// </summary>
public sealed class TerminalPersistentRasterPlacementLifecycleTests {
	[Fact]
	public void PublicPlacementUpdateContractUsesExistingMutationResult() {
		MethodInfo? update = typeof( TerminalRasterPlacement ).GetMethod(
			"UpdateAsync",
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			types: [
				typeof( TerminalRasterPlacementOptions ),
				typeof( CancellationToken )
			],
			modifiers: null
		);

		Assert.NotNull( update );
		Assert.Equal(
			typeof( ValueTask<TerminalControlMutationResult> ),
			update.ReturnType
		);
	}

	[Fact]
	public async Task UpdateReusesPlacementIdentityAtCurrentCursor() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );

		TerminalControlMutationResult result = await placement.UpdateAsync(
			new TerminalRasterPlacementOptions {
				Columns = 4,
				Rows = 3
			}
		);
		await transport.WaitForWriteCountAsync( 3 );

		Assert.True( result.Succeeded );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=1,C=1,c=4,r=3\u001b\\"
			),
			transport.Writes[ 2 ]
		);
	}

	[Theory]
	[InlineData( 0, null )]
	[InlineData( 16385, null )]
	[InlineData( null, 0 )]
	[InlineData( null, 16385 )]
	public async Task InvalidUpdateExtentsAreRejectedBeforeOutput(
		int? columns,
		int? rows
	) {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			async () => await placement.UpdateAsync(
				new TerminalRasterPlacementOptions {
					Columns = columns,
					Rows = rows
				}
			)
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task DisposedPlacementRejectsUpdateBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		await placement.DisposeAsync();
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ObjectDisposedException>(
			async () => await placement.UpdateAsync()
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task PlacementDisposeSendsOneQuietSoftDelete() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );

		await placement.DisposeAsync();
		await transport.WaitForWriteCountAsync( 3 );
		await placement.DisposeAsync();

		Assert.Equal( 3, transport.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=d,d=i,i=77,p=1,q=2\u001b\\"
			),
			transport.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task ResourceDisposeDeletesChildrenBeforeResourceData() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 91
		);
		TerminalRasterPlacement first = await CreatePlacementAsync( resource );
		TerminalRasterPlacement second = await CreatePlacementAsync( resource );

		await resource.DisposeAsync();
		await transport.WaitForWriteCountAsync( 6 );

		byte[] firstDelete = Encoding.ASCII.GetBytes(
			"\u001b_Ga=d,d=i,i=91,p=1,q=2\u001b\\"
		);
		byte[] secondDelete = Encoding.ASCII.GetBytes(
			"\u001b_Ga=d,d=i,i=91,p=2,q=2\u001b\\"
		);
		Assert.Contains(
			transport.Writes.Skip( 3 ).Take( 2 ),
			value => value.SequenceEqual( firstDelete )
		);
		Assert.Contains(
			transport.Writes.Skip( 3 ).Take( 2 ),
			value => value.SequenceEqual( secondDelete )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=d,d=I,i=91,q=2\u001b\\"
			),
			transport.Writes[ 5 ]
		);

		int baselineWrites = transport.Writes.Count;
		await first.DisposeAsync();
		await second.DisposeAsync();
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task ParentDisposalMakesChildUpdateUnavailableWithoutOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		await resource.DisposeAsync();
		int baselineWrites = transport.Writes.Count;

		TerminalControlMutationResult result = await placement.UpdateAsync();

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task FailedPlacementCleanupIsNotRetried() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		transport.FailOnWriteNumber = 3;

		await Assert.ThrowsAsync<IOException>(
			async () => await placement.DisposeAsync()
		);
		Assert.Equal( 3, transport.Writes.Count );

		await placement.DisposeAsync();
		Assert.Equal( 3, transport.Writes.Count );
	}

	[Fact]
	public async Task ResourceCleanupContinuesAfterOneChildDeleteFailure() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 91
		);
		_ = await CreatePlacementAsync( resource );
		_ = await CreatePlacementAsync( resource );
		transport.FailOnWriteNumber = 4;

		await Assert.ThrowsAnyAsync<Exception>(
			async () => await resource.DisposeAsync()
		);
		await transport.WaitForWriteCountAsync( 6 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=d,d=I,i=91,q=2\u001b\\"
			),
			transport.Writes[ 5 ]
		);
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

	private static TerminalRasterImage CreateSmallImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I=1;OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
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
		private int writeNumber;

		internal int? FailOnWriteNumber {
			get;
			set;
		}

		internal IReadOnlyList<byte[]> Writes {
			get {
				lock ( this.synchronization ) {
					return this.writes.Select(
						static item => item.ToArray()
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
			int current = Interlocked.Increment( ref this.writeNumber );
			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			if ( this.FailOnWriteNumber == current ) {
				throw new IOException( "Synthetic persistent raster cleanup failure." );
			}
			this.PublishPlacementAcknowledgement( buffer.Span );
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

			while ( true ) {
				lock ( this.synchronization ) {
					if ( count <= this.writes.Count ) {
						return;
					}
				}
				await this.writeSignal.WaitAsync().ConfigureAwait( false );
			}
		}

		private void PublishPlacementAcknowledgement(
			ReadOnlySpan<byte> frame
		) {
			string text = Encoding.ASCII.GetString( frame );
			if ( !text.StartsWith(
				"\u001b_Ga=p,",
				StringComparison.Ordinal
			) || !TryReadIdentityField(
				text,
				",i=",
				out uint imageId
			) || !TryReadIdentityField(
				text,
				",p=",
				out uint placementId
			) ) {
				return;
			}

			this.Publish(
				Encoding.ASCII.GetBytes(
					$"\u001b_Gi={imageId},p={placementId};OK\u001b\\"
				)
			);
		}

		private static bool TryReadIdentityField(
			string text,
			string marker,
			out uint value
		) {
			ArgumentNullException.ThrowIfNull( text );
			ArgumentException.ThrowIfNullOrEmpty( marker );

			int start = text.IndexOf(
				marker,
				StringComparison.Ordinal
			);
			if ( 0 > start ) {
				value = 0u;
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
