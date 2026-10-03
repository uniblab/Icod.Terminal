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

	private static void Verify( TerminalSession session, TerminalProtocolBackend backend ) {
		session.RecordSemanticBackendEvidence(
			backend,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync( RecordingTransport transport ) =>
		TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = new TerminalDescriptionBuilder( "screen-raster-test" ).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte> bytes = [];
		internal byte[] Bytes => bytes.ToArray();
		internal int FlushCount { get; private set; }

		public async ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
			byte[] value = await input.Reader.ReadAsync( cancellationToken );
			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
		}

		public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync( CancellationToken cancellationToken = default ) {
			cancellationToken.ThrowIfCancellationRequested();
			FlushCount++;
			return ValueTask.CompletedTask;
		}
	}
}
