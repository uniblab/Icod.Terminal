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
using System.Text;

namespace Icod.Terminal.Tests.Input;
using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;

public sealed class TerminalClipboardAllocationTests( ITestOutputHelper output ) {
	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 4 )]
	[InlineData( 2, 4 )]
	[InlineData( 3, 4 )]
	[InlineData( 65535, 87380 )]
	[InlineData( 65536, 87384 )]
	[InlineData( 65537, 87384 )]
	[InlineData( 1048576, 1398104 )]
	[InlineData( 16777216, 22369624 )]
	public void GeometryIsExact( int decoded, int encoded ) {
		Assert.Equal( encoded, TerminalClipboardLimits.GetEncodedLength( decoded ) );
		if ( decoded > 0 ) Assert.Equal( encoded + 16, TerminalClipboardLimits.GetResponseFrameLimit( decoded ) );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 16777217 )]
	[InlineData( int.MaxValue )]
	public void UnsupportedGeometryIsRejectedBeforeOverflow( int decoded ) {
		Assert.Throws<ArgumentOutOfRangeException>( () => TerminalClipboardLimits.GetEncodedLength( decoded ) );
	}

	[Fact]
	public void IdleOptionsAndAccumulatorDoNotReserveTheCeiling() {
		using ( var warmup = new TerminalClipboardResponseAccumulator( 16777216 ) ) { }
		long before = GC.GetAllocatedBytesForCurrentThread();
		var options = new TerminalClipboardOptions { MaximumReadBytes = 16777216 };
		using var accumulator = new TerminalClipboardResponseAccumulator( options.MaximumReadBytes );
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.InRange( allocated, 1, 4096 );
	}

	[Theory]
	[InlineData( 65536 )]
	[InlineData( 1048576 )]
	[InlineData( 16777216 )]
	public void EncodingAndIncrementalReadHaveBoundedAllocations( int size ) {
		byte[] payload = new byte[ size ];
		byte[] input = Encoding.ASCII.GetBytes( "\u001b]52;c;" + Convert.ToBase64String( payload ) + "\u001b\\" );
		_ = OscWriter.EncodeOsc52WriteFrame( TerminalOsc52Selection.Clipboard, [], size );
		var clock = Stopwatch.StartNew();
		long before = GC.GetAllocatedBytesForCurrentThread();
		byte[] frame = OscWriter.EncodeOsc52WriteFrame( TerminalOsc52Selection.Clipboard, payload, size );
		long writeAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
		double writeMs = clock.Elapsed.TotalMilliseconds;
		Assert.Equal( input, frame );
		// Fixed slack accommodates CLR allocation accounting/JIT bookkeeping across TFMs.
		// It still rejects a second payload-sized array or an intermediate base64 string.
		Assert.InRange( writeAllocated, frame.Length, frame.Length + 32768L );

		clock.Restart();
		before = GC.GetAllocatedBytesForCurrentThread();
		byte[] decoded;
		using ( var accumulator = new TerminalClipboardResponseAccumulator( size ) ) {
			// One Feed call per byte, regardless of transport fragment sizes.
			foreach ( byte value in input ) accumulator.Feed( value );
			TerminalResponseFrame response = accumulator.ToFrame();
			decoded = TerminalOsc52Protocol.ParsePayload( response, TerminalOsc52Selection.Clipboard, size );
		}
		long readAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
		double readMs = clock.Elapsed.TotalMilliseconds;
		Assert.Equal( payload, decoded );
		int limit = TerminalClipboardLimits.GetResponseFrameLimit( size );
		// Geometric growth sum < 3F; one exact frame copy F; one decoded result N.
		Assert.InRange( readAllocated, size, 4L * limit + size + 8192 );
		output.WriteLine( $"Bytes={size}; write allocated={writeAllocated}, ms={writeMs:F3}; incremental read allocated={readAllocated}, ms={readMs:F3}; live response storage upper bound={2L * limit + size}" );
	}

	[Fact]
	public void RejectedWriteDoesNotAllocateAFrame() {
		byte[] payload = new byte[ 1_048_576 ];
		Assert.Throws<ArgumentOutOfRangeException>( () => OscWriter.EncodeOsc52WriteFrame( TerminalOsc52Selection.Clipboard, payload ) );
		long before = GC.GetAllocatedBytesForCurrentThread();
		Assert.Throws<ArgumentOutOfRangeException>( () => OscWriter.EncodeOsc52WriteFrame( TerminalOsc52Selection.Clipboard, payload ) );
		Assert.InRange( GC.GetAllocatedBytesForCurrentThread() - before, 0, 8192 );
	}
}
