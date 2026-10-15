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
namespace Icod.Terminal.Tests.Input;
using System.Text;
using Xunit;

public sealed class WindowsTerminalInputTests {
	[Fact]
	public async Task PreservesControlZAndSplitSurrogatesAcrossSmallReads() {
		Queue<string> chunks = new( [ "\u001a", "\ud83d", "\ude42界", "" ] );
		using WindowsTerminalInput input = new( buffer => { string next = chunks.Dequeue(); next.CopyTo( 0, buffer, 0, next.Length ); return next.Length; }, () => { } );
		List<byte> received = []; byte[] one = new byte[ 1 ]; int count;
		while ( ( count = await input.ReadAsync( one ) ) > 0 ) received.AddRange( one.Take( count ) );
		Assert.Equal( "\u001a🙂界", Encoding.UTF8.GetString( received.ToArray() ) );
	}

	[Fact]
	public async Task CancellationWaitsForNativeReadAndAllowsAnotherRead() {
		using ManualResetEventSlim started = new(); using ManualResetEventSlim canceled = new();
		int reads = 0;
		using WindowsTerminalInput input = new( buffer => {
			if ( Interlocked.Increment( ref reads ) == 1 ) { started.Set(); canceled.Wait(); throw new IOException( "Canceled native read" ); }
			buffer[ 0 ] = 'x'; return 1;
		}, () => canceled.Set() );
		using CancellationTokenSource stop = new(); byte[] buffer = new byte[ 4 ];
		Task<int> read = input.ReadAsync( buffer, stop.Token ).AsTask();
		Assert.True( started.Wait( TimeSpan.FromSeconds( 5 ) ) ); stop.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => read.WaitAsync( TimeSpan.FromSeconds( 5 ) ) );
		Assert.Equal( 1, await input.ReadAsync( buffer ) ); Assert.Equal( (byte)'x', buffer[ 0 ] );
	}

	[Fact]
	public async Task DisposalCancelsOutstandingReadBeforeClosingOwner() {
		using ManualResetEventSlim started = new(); using ManualResetEventSlim canceled = new();
		bool finished = false; Owner owner = new( () => Assert.True( finished ) );
		WindowsTerminalInput input = new( _ => { started.Set(); canceled.Wait(); finished = true; throw new IOException(); }, () => canceled.Set(), owner );
		Task<int> read = input.ReadAsync( new byte[ 4 ] ).AsTask();
		Assert.True( started.Wait( TimeSpan.FromSeconds( 5 ) ) ); input.Dispose(); input.Dispose();
		await Assert.ThrowsAnyAsync<OperationCanceledException>( () => read ); Assert.Equal( 1, owner.Disposals );
	}
	private sealed class Owner( Action onDispose ) : IDisposable {
		internal int Disposals { get; private set; }
		public void Dispose() { onDispose(); this.Disposals++; }
	}
}
