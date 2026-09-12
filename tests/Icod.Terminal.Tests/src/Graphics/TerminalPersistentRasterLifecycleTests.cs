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
/// Defines C117 persistent-raster invalidation, managed lifecycle, and session-teardown semantics.
/// </summary>
public sealed class TerminalPersistentRasterLifecycleTests {
	[Fact]
	public async Task ExplicitInvalidationStalesResourceAndPlacementWithoutOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		int baselineWrites = transport.Writes.Count;

		session.InvalidateState();

		TerminalControlMutationResult update = await placement.UpdateAsync();
		TerminalControlResult<TerminalRasterPlacement> create =
			await resource.CreatePlacementAsync();

		Assert.Equal( TerminalControlStatus.Unavailable, update.Status );
		Assert.Equal( TerminalControlStatus.Unavailable, create.Status );
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task StaleDisposalPerformsLocalCleanupOnly() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		session.InvalidateState();
		int baselineWrites = transport.Writes.Count;

		await placement.DisposeAsync();
		await resource.DisposeAsync();

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task SuspendResumeStalesPersistentHandlesWithoutReplay() {
		ScriptedTransport transport = new();
		TestLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		int baselineWrites = transport.Writes.Count;
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.True( session.IsStateValid );
		Assert.Equal( baselineWrites, transport.Writes.Count );
		Assert.DoesNotContain(
			transport.Writes.Skip( baselineWrites ),
			static value => Encoding.ASCII.GetString( value ).Contains(
				"Ga=t",
				StringComparison.Ordinal
			)
		);

		TerminalControlMutationResult update = await placement.UpdateAsync();
		TerminalControlResult<TerminalRasterPlacement> create =
			await resource.CreatePlacementAsync();
		Assert.Equal( TerminalControlStatus.Unavailable, update.Status );
		Assert.Equal( TerminalControlStatus.Unavailable, create.Status );
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task ExternalResumeAlsoStalesPersistentHandlesWithoutReplay() {
		ScriptedTransport transport = new();
		TestLifecycleSource lifecycle = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync( resource );
		int baselineWrites = transport.Writes.Count;
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal( baselineWrites, transport.Writes.Count );
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

	[Fact]
	public async Task SessionDisposeDeletesAllPlacementsBeforeAnyResourceData() {
		ScriptedTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource firstResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 71,
			imageNumber: 1
		);
		_ = await CreatePlacementAsync( firstResource );
		TerminalRasterResource secondResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 72,
			imageNumber: 2
		);
		_ = await CreatePlacementAsync( secondResource );
		int baselineWrites = transport.Writes.Count;

		await session.DisposeAsync();

		string[] cleanup = transport.Writes
			.Skip( baselineWrites )
			.Select( static value => Encoding.ASCII.GetString( value ) )
			.Where( static value => value.Contains( "Ga=d", StringComparison.Ordinal ) )
			.ToArray();

		Assert.Equal( 4, cleanup.Length );
		Assert.Contains( "d=i", cleanup[ 0 ], StringComparison.Ordinal );
		Assert.Contains( "d=i", cleanup[ 1 ], StringComparison.Ordinal );
		Assert.Contains( "d=I", cleanup[ 2 ], StringComparison.Ordinal );
		Assert.Contains( "d=I", cleanup[ 3 ], StringComparison.Ordinal );
	}

	[Fact]
	public async Task SessionDisposeAfterInvalidationEmitsNoPersistentIdentifiers() {
		ScriptedTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		_ = await CreatePlacementAsync( resource );
		session.InvalidateState();
		int baselineWrites = transport.Writes.Count;

		await session.DisposeAsync();

		Assert.DoesNotContain(
			transport.Writes.Skip( baselineWrites ),
			static value => Encoding.ASCII.GetString( value ).Contains(
				"Ga=d",
				StringComparison.Ordinal
			)
		);
	}

	[Fact]
	public async Task SessionCleanupContinuesAfterPersistentDeleteFailure() {
		ScriptedTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 91,
			imageNumber: 1
		);
		_ = await CreatePlacementAsync( resource );
		int baselineWrites = transport.Writes.Count;
		transport.FailOnWriteNumber = baselineWrites + 1;

		await Assert.ThrowsAnyAsync<Exception>(
			async () => await session.DisposeAsync()
		);

		string[] cleanup = transport.Writes
			.Skip( baselineWrites )
			.Select( static value => Encoding.ASCII.GetString( value ) )
			.Where( static value => value.Contains( "Ga=d", StringComparison.Ordinal ) )
			.ToArray();
		Assert.Equal( 2, cleanup.Length );
		Assert.Contains( "d=i", cleanup[ 0 ], StringComparison.Ordinal );
		Assert.Contains( "d=I", cleanup[ 1 ], StringComparison.Ordinal );
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
		uint imageId,
		uint imageNumber
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}
		if ( 0u == imageNumber ) {
			throw new ArgumentOutOfRangeException( nameof( imageNumber ) );
		}

		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( transport.Writes.Count + 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I={imageNumber};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport,
		TestLifecycleSource? lifecycle = null
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
				LifecycleSource = lifecycle,
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

	private sealed class TestLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}
			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
		}
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
						static value => value.ToArray()
					).ToArray();
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
				throw new IOException( "Synthetic persistent raster lifecycle cleanup failure." );
			}
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
	}
}
