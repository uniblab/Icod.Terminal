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
namespace Icod.Terminal.Tests.Screen;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

public sealed class TerminalScreenRasterTransactionTests {
	[Fact]
	public async Task VerifiedSixelOrdersTextAndOneCompleteRaster() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.DcsSixel );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteText( "before" );
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 0, 0, 0 ] ) );
		transaction.WriteText( "after" );

		await transaction.CommitAsync();

		Assert.Equal(
			"before\u001bP0;1;0q\"1;1;1;1#0;2;0;0;0#0@\u001b\\after",
			Encoding.ASCII.GetString( transport.Bytes )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	[Fact]
	public async Task VerifiedKittyIsPreferredWithoutPersistentIdentity() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.DcsSixel );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );

		await transaction.CommitAsync();

		Assert.Equal(
			"\u001b_Ga=T,f=24,s=1,v=1,t=d,m=0,q=2;AQID\u001b\\",
			Encoding.ASCII.GetString( transport.Bytes )
		);
	}

	[Fact]
	public async Task UnknownBackendRejectsEntireTransactionBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteText( "before" );
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );

		await Assert.ThrowsAsync<NotSupportedException>( () => transaction.CommitAsync().AsTask() );
		Assert.Empty( transport.Bytes );
	}

	[Fact]
	public async Task InvalidLaterSixelImageRejectsEarlierItemsBeforeOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.DcsSixel );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteText( "before" );
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 0, 0, 0 ] ) );
		transaction.WriteRaster( TerminalRasterImage.CreateRgba32( 1, 1, [ 1, 2, 3, 128 ] ) );

		await Assert.ThrowsAsync<NotSupportedException>( () => transaction.CommitAsync().AsTask() );
		Assert.Empty( transport.Bytes );
	}

	[Fact]
	public async Task OrdinaryKittyWorksWhenPersistentRasterIsUnsupported() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		session.RecordSemanticOperationEvidence(
			TerminalSemanticOperation.PersistentRasterGraphics,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );

		await transaction.CommitAsync();
		Assert.Equal( "\u001b_Ga=T,f=24,s=1,v=1,t=d,m=0,q=2;AQID\u001b\\", Encoding.ASCII.GetString( transport.Bytes ) );
	}

	[Fact]
	public async Task CursorPlansBracketRasterWithExplicitUnknownPosition() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, cursorAddress: true );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.Add( session.Screen.PlanCursorMove( null, new TerminalScreenPosition( 2, 3 ) )!.Value );
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );
		transaction.Add( session.Screen.PlanCursorMove( null, new TerminalScreenPosition( 5, 6 ) )!.Value );
		transaction.WriteText( "after" );

		await transaction.CommitAsync();
		Assert.Equal(
			"<cup:2,3>\u001b_Ga=T,f=24,s=1,v=1,t=d,m=0,q=2;AQID\u001b\\<cup:5,6>after",
			Encoding.ASCII.GetString( transport.Bytes )
		);
	}

	[Fact]
	public async Task ChangedEvidenceDuringGateWaitRejectsWithoutOutput() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		IDisposable blocker = await session.AcquireSessionOutputAsync( CancellationToken.None );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );
		Task commit = transaction.CommitAsync().AsTask();
		try {
			Assert.False( commit.IsCompleted );
			session.RecordSemanticBackendEvidence(
				TerminalProtocolBackend.ApcKittyGraphics,
				TerminalCapabilitySupportState.Unsupported,
				TerminalCapabilityEvidenceSource.ProtocolResponse
			);
		} finally {
			blocker.Dispose();
		}
		await Assert.ThrowsAsync<InvalidOperationException>( () => commit.WaitAsync( TimeSpan.FromSeconds( 5 ) ) );
		Assert.Empty( transport.Bytes );
	}

	[Fact]
	public async Task StaleEpochAndPrecommitCancellationDoNotEmit() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalScreenOutputTransaction stale = session.CreateScreenOutputTransaction();
		stale.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );
		await session.WriteTextAsync( "other" );
		await Assert.ThrowsAsync<InvalidOperationException>( () => stale.CommitAsync().AsTask() );
		Assert.Equal( "other", Encoding.ASCII.GetString( transport.Bytes ) );
		TerminalScreenOutputTransaction cancelled = session.CreateScreenOutputTransaction();
		cancelled.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );
		using CancellationTokenSource source = new();
		source.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => cancelled.CommitAsync( source.Token ).AsTask() );
		Assert.Equal( "other", Encoding.ASCII.GetString( transport.Bytes ) );
	}

	[Fact]
	public async Task EncodedCeilingRejectsBeforeAnyWriteAndNullImageRejectsAtAdd() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalRasterImage image = TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		Assert.Throws<ArgumentNullException>( () => transaction.WriteRaster( null! ) );
		transaction.WriteText( "before" );
		transaction.WriteRaster( image );
		Assert.Throws<InvalidOperationException>( () => TerminalPreparedRasterOutput.Prepare( session, image, 8 ) );
		Assert.Empty( transport.Bytes );
	}

	[Fact]
	public async Task CommittedRasterWriteFailureDoesNotRetryOrSwitchBackend() {
		RecordingTransport transport = new() { FailingWriteAttempt = 2 };
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.DcsSixel );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction();
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );
		transaction.WriteText( "after" );
		await Assert.ThrowsAsync<IOException>( () => transaction.CommitAsync().AsTask() );
		Assert.Equal( 2, transport.WriteAttemptCount );
		Assert.Equal( 1, transport.FlushCount );
		Assert.DoesNotContain( "after", Encoding.ASCII.GetString( transport.Bytes ) );
		await Assert.ThrowsAsync<InvalidOperationException>( () => transaction.CommitAsync().AsTask() );
	}

	[Fact]
	public async Task SynchronizedFrameContainsCompleteRasterInItemOrder() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Verify( session, TerminalProtocolBackend.ApcKittyGraphics );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "before" );
		transaction.WriteRaster( TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ) );
		transaction.WriteText( "after" );

		await transaction.CommitAsync();
		Assert.Equal(
			"\u001b[?2026hbefore\u001b_Ga=T,f=24,s=1,v=1,t=d,m=0,q=2;AQID\u001b\\after\u001b[?2026l",
			Encoding.ASCII.GetString( transport.Bytes )
		);
		Assert.Equal( 1, transport.FlushCount );
	}

	private static void Verify( TerminalSession session, TerminalProtocolBackend backend ) {
		session.RecordSemanticBackendEvidence(
			backend,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync( RecordingTransport transport, bool cursorAddress = false ) =>
		TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = cursorAddress
					? new TerminalDescriptionBuilder( "screen-raster-test" )
						.SetString( StringCapability.CursorAddress, "<cup:%p1%d,%p2%d>" ).Build()
					: new TerminalDescriptionBuilder( "screen-raster-test" ).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte> bytes = [];
		internal byte[] Bytes => bytes.ToArray();
		internal int FlushCount { get; private set; }
		internal int WriteAttemptCount { get; private set; }
		internal int FailingWriteAttempt { get; init; }

		public async ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			byte[] value = await input.Reader.ReadAsync( cancellationToken );
			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
		}

		public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			WriteAttemptCount++;
			if ( WriteAttemptCount == FailingWriteAttempt ) {
				throw new IOException( "Synthetic raster write failure." );
			}
			bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			FlushCount++;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0, 0, 0, 0x0002UL, new byte[ 32 ], 0, 32, 0,
			new TerminalSpeed( 13, 9600 ), new TerminalSpeed( 13, 9600 )
		);

		public TerminalControlResult<TerminalEndpointObservation> Observe( TerminalEndpoint endpoint ) =>
			TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true, null, TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment | TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite | TerminalControlCapabilities.LiveSize
				)
			);

		public TerminalControlResult<TerminalModeSnapshot> GetMode( TerminalEndpoint endpoint ) =>
			TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );

		public TerminalControlResult<TerminalSize> GetSize( TerminalEndpoint endpoint ) =>
			TerminalControlResult<TerminalSize>.Available( new TerminalSize( 80, 24 ) );

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint, TerminalModeSnapshot mode, TerminalModeApplyTiming timing
		) => TerminalControlMutationResult.Success();
	}
}
