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
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Freezes bounded environment-report routing on the authoritative input path.
/// </summary>
public sealed class TerminalEnvironmentRoutingTests {
	[Theory]
	[InlineData( "\u001b[?997;1n", TerminalSemanticEventKind.Appearance )]
	[InlineData( "\u001b[48;24;80;0;0t", TerminalSemanticEventKind.InBandResize )]
	public async Task EverySplitPointPreservesOneSemanticEvent(
		string text,
		TerminalSemanticEventKind expectedKind
	) {
		byte[] frame = Encoding.ASCII.GetBytes( text );
		for ( int split = 1; split < frame.Length; ++split ) {
			TerminalInputDecoder decoder = CreateDecoder(
				new ScriptedTerminalInput(
					frame[..split],
					frame[split..]
				)
			);

			TerminalSemanticEvent semantic = await ReadSemanticAsync( decoder );

			Assert.Equal( expectedKind, semantic.Kind );
		}
	}

	[Fact]
	public async Task ConcatenatedReportsAndInputRemainOrderedAndIndependent() {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				Encoding.ASCII.GetBytes(
					"\u001b[?997;1n\u001b[48;24;80;768;1280tx"
				)
			)
		);

		TerminalSemanticEvent appearance = await ReadSemanticAsync( decoder );
		TerminalSemanticEvent resize = await ReadSemanticAsync( decoder );
		TerminalInputDecodeResult input = await ReadApplicationAsync( decoder );

		Assert.Equal( TerminalAppearance.Dark, appearance.Appearance?.Appearance );
		Assert.Equal( new TerminalDimensions( 80, 24 ), resize.InBandResize?.Dimensions );
		Assert.Equal(
			new TerminalPixelDimensions( 1280, 768 ),
			resize.InBandResize?.PixelDimensions
		);
		Assert.Equal( new Rune( 'x' ), input.InputEvent?.Character );
	}

	[Fact]
	public async Task ActiveAppearanceExpectationWinsBeforeSemanticRecognition() {
		byte[] response = Encoding.ASCII.GetBytes( "\u001b[?997;2n" );
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				response.Concat( Encoding.ASCII.GetBytes( "x" ) ).ToArray()
			)
		);
		TerminalResponseExpectation expectation = decoder.RegisterResponseExpectation(
			TerminalEnvironmentProtocol.AppearanceReportMatcher
		);

		TerminalInputDecodeResult routed = await decoder.ReadNextAsync();
		Assert.True( routed.ResponseRouted );
		Assert.Null( routed.ApplicationEvent );
		routed.CompleteRoutedResponse();
		Assert.Equal( response, ( await expectation.Response ).Bytes.ToArray() );

		TerminalInputDecodeResult input = await ReadApplicationAsync( decoder );
		Assert.Equal( new Rune( 'x' ), input.InputEvent?.Character );
	}

	[Theory]
	[InlineData( "\u001b[?2031;1$y" )]
	[InlineData( "\u001b[?2048;4$y" )]
	public async Task UnclaimedEnvironmentModeReportIsConsumedAsOrphan(
		string report
	) {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				Encoding.ASCII.GetBytes( report + "x" )
			)
		);

		TerminalInputDecodeResult result = await ReadApplicationAsync( decoder );

		Assert.Equal( new Rune( 'x' ), result.InputEvent?.Character );
	}

	[Theory]
	[InlineData( "\u001b[?997;9n" )]
	[InlineData( "\u001b[?997;1:2n" )]
	[InlineData( "\u001b[48;24;80;0;1t" )]
	[InlineData( "\u001b[48;24:;80;0;0t" )]
	[InlineData( "\u001b[?2031;9$y" )]
	public async Task MalformedOwnedFrameEndsAtItsBoundaryAndPreservesFollowingInput(
		string report
	) {
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput(
				Encoding.ASCII.GetBytes( report + "x" )
			)
		);

		TerminalInputDecodeResult result = await ReadApplicationAsync( decoder );

		Assert.Equal( new Rune( 'x' ), result.InputEvent?.Character );
	}

	[Fact]
	public async Task OversizedAppearanceReportDrainsToFrameBoundary() {
		byte[] bytes = Encoding.ASCII.GetBytes(
			"\u001b[?997;" + new string( '1', 96 ) + "nx"
		);
		TerminalInputDecoder decoder = CreateDecoder(
			new ScriptedTerminalInput( SplitEvery( bytes, 16 ) ),
			maximumBufferedBytes: 64
		);

		TerminalInputDecodeResult result = await ReadApplicationAsync( decoder );

		Assert.Equal( new Rune( 'x' ), result.InputEvent?.Character );
	}

	private static TerminalInputDecoder CreateDecoder(
		ITerminalInput input,
		int maximumBufferedBytes = TerminalResponseFramer.DefaultMaximumFrameBytes
	) {
		return new TerminalInputDecoder(
			input,
			new TerminalDescriptionBuilder( "terminal-environment-routing" ).Build(),
			SystemMonotonicClock.Instance,
			TimeSpan.FromSeconds( 1 ),
			maximumBufferedBytes
		);
	}

	private static async ValueTask<TerminalSemanticEvent> ReadSemanticAsync(
		TerminalInputDecoder decoder
	) {
		TerminalInputDecodeResult result = await ReadApplicationAsync( decoder );
		return Assert.IsType<TerminalSemanticEvent>(
			result.ApplicationEvent?.SemanticEvent
		);
	}

	private static async ValueTask<TerminalInputDecodeResult> ReadApplicationAsync(
		TerminalInputDecoder decoder
	) {
		while ( true ) {
			TerminalInputDecodeResult result = await decoder.ReadNextAsync();
			if ( result.RoutingRestartRequired ) {
				continue;
			}
			Assert.False( result.ResponseRouted );
			Assert.True( result.ApplicationEvent.HasValue );
			return result;
		}
	}

	private static byte[][] SplitEvery(
		byte[] bytes,
		int chunkSize
	) {
		List<byte[]> chunks = [];
		for ( int offset = 0; offset < bytes.Length; offset += chunkSize ) {
			chunks.Add(
				bytes.AsSpan(
					offset,
					Math.Min( chunkSize, bytes.Length - offset )
				).ToArray()
			);
		}
		return chunks.ToArray();
	}

	private sealed class ScriptedTerminalInput : ITerminalInput {
		private readonly Queue<byte[]> chunks;

		internal ScriptedTerminalInput(
			params byte[][] chunks
		) {
			this.chunks = new Queue<byte[]>(
				chunks.Select( static chunk => chunk.ToArray() )
			);
		}

		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( 0 == this.chunks.Count ) {
				return ValueTask.FromResult( 0 );
			}

			byte[] chunk = this.chunks.Dequeue();
			if ( chunk.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted chunk exceeds the decoder read buffer."
				);
			}
			chunk.AsSpan().CopyTo( buffer.Span );
			return ValueTask.FromResult( chunk.Length );
		}
	}
}
