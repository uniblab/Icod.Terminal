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
/// Defines the C115 public placement-creation and multi-placement ownership contract.
/// </summary>
public sealed class TerminalPersistentRasterPlacementCreationTests {
	[Fact]
	public void PublicPlacementCreationContractIsOpaque() {
		Type optionsType = typeof( TerminalRasterPlacementOptions );
		Type placementType = typeof( TerminalRasterPlacement );

		Assert.True( optionsType.IsPublic );
		Assert.True( optionsType.IsSealed );
		Assert.Equal(
			typeof( int? ),
			optionsType.GetProperty( nameof( TerminalRasterPlacementOptions.Columns ) )?.PropertyType
		);
		Assert.Equal(
			typeof( int? ),
			optionsType.GetProperty( nameof( TerminalRasterPlacementOptions.Rows ) )?.PropertyType
		);

		Assert.True( placementType.IsPublic );
		Assert.True( placementType.IsSealed );
		Assert.Contains( typeof( IAsyncDisposable ), placementType.GetInterfaces() );
		Assert.DoesNotContain(
			placementType.GetMembers( BindingFlags.Instance | BindingFlags.Public ),
			static member => member.Name.Contains( "ImageId", StringComparison.Ordinal )
				|| member.Name.Contains( "ImageNumber", StringComparison.Ordinal )
				|| member.Name.Contains( "PlacementId", StringComparison.Ordinal )
				|| member.Name.Contains( "Backend", StringComparison.Ordinal )
				|| member.Name.Contains( "X", StringComparison.Ordinal )
				|| member.Name.Contains( "Y", StringComparison.Ordinal )
		);

		MethodInfo? create = typeof( TerminalRasterResource ).GetMethod(
			"CreatePlacementAsync",
			BindingFlags.Instance | BindingFlags.Public,
			binder: null,
			types: [
				typeof( TerminalRasterPlacementOptions ),
				typeof( CancellationToken )
			],
			modifiers: null
		);
		Assert.NotNull( create );
		Assert.Equal(
			typeof( ValueTask<TerminalControlResult<TerminalRasterPlacement>> ),
			create.ReturnType
		);
	}

	[Fact]
	public async Task IntrinsicPlacementUsesCurrentCursorWithoutMovingIt() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);

		TerminalControlResult<TerminalRasterPlacement> result =
			await resource.CreatePlacementAsync();
		await transport.WaitForWriteCountAsync( 2 );

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.NotNull( result.Value );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=1,C=1\u001b\\"
			),
			transport.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task PlacementCanSpecifyEitherOrBothCellExtents() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);

		TerminalControlResult<TerminalRasterPlacement> columns =
			await resource.CreatePlacementAsync(
				new TerminalRasterPlacementOptions {
					Columns = 3
				}
			);
		TerminalControlResult<TerminalRasterPlacement> rows =
			await resource.CreatePlacementAsync(
				new TerminalRasterPlacementOptions {
					Rows = 2
				}
			);
		TerminalControlResult<TerminalRasterPlacement> both =
			await resource.CreatePlacementAsync(
				new TerminalRasterPlacementOptions {
					Columns = 5,
					Rows = 4
				}
			);
		await transport.WaitForWriteCountAsync( 4 );

		Assert.Equal( TerminalControlStatus.Available, columns.Status );
		Assert.Equal( TerminalControlStatus.Available, rows.Status );
		Assert.Equal( TerminalControlStatus.Available, both.Status );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=1,C=1,c=3\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=2,C=1,r=2\u001b\\"
			),
			transport.Writes[ 2 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=3,C=1,c=5,r=4\u001b\\"
			),
			transport.Writes[ 3 ]
		);
	}

	[Theory]
	[InlineData( 0, null )]
	[InlineData( -1, null )]
	[InlineData( 16385, null )]
	[InlineData( null, 0 )]
	[InlineData( null, -1 )]
	[InlineData( null, 16385 )]
	public async Task InvalidPlacementExtentsAreRejectedBeforeOutput(
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
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			async () => await resource.CreatePlacementAsync(
				new TerminalRasterPlacementOptions {
					Columns = columns,
					Rows = rows
				}
			)
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task PlacementExtentsAcceptReviewedUpperBound() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);

		TerminalControlResult<TerminalRasterPlacement> result =
			await resource.CreatePlacementAsync(
				new TerminalRasterPlacementOptions {
					Columns = 16384,
					Rows = 16384
				}
			);
		await transport.WaitForWriteCountAsync( 2 );

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=1,C=1,c=16384,r=16384\u001b\\"
			),
			transport.Writes[ 1 ]
		);
	}

	[Fact]
	public async Task MultiplePlacementsUseDistinctPrivatePlacementIdentities() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 91
		);

		TerminalControlResult<TerminalRasterPlacement> first =
			await resource.CreatePlacementAsync();
		TerminalControlResult<TerminalRasterPlacement> second =
			await resource.CreatePlacementAsync();
		await transport.WaitForWriteCountAsync( 3 );

		Assert.NotNull( first.Value );
		Assert.NotNull( second.Value );
		Assert.NotSame( first.Value, second.Value );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=91,p=1,C=1\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=91,p=2,C=1\u001b\\"
			),
			transport.Writes[ 2 ]
		);
	}

	[Fact]
	public async Task PreCanceledPlacementCreationEmitsNoPlacementOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		int baselineWrites = transport.Writes.Count;
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			async () => await resource.CreatePlacementAsync(
				options: null,
				cancellation.Token
			)
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task DisposedResourceRejectsNewPlacementBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77
		);
		await resource.DisposeAsync();
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ObjectDisposedException>(
			async () => await resource.CreatePlacementAsync()
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
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

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return OpenSessionCoreAsync( transport );
	}

	private static async ValueTask<TerminalSession> OpenSessionCoreAsync(
		ScriptedTransport transport
	) {
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
			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
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
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal async Task WaitForWriteCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
			while ( this.Writes.Count < expected ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
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
			value = 0u;
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
