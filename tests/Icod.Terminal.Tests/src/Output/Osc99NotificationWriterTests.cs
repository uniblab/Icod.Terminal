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
namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

public sealed class Osc99NotificationWriterTests {
	[Fact]
	public void EncodesCanonicalSingleTitleFrame() {
		byte[][] frames = OscWriter.EncodeOsc99NotificationFrames(
			"Build",
			string.Empty,
			null
		);

		Assert.Single( frames );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;p=title:e=1:d=1;QnVpbGQ=\u001b\\"
			),
			frames[ 0 ]
		);
	}

	[Fact]
	public void EncodesTypedMetadataAndBody() {
		KittyNotificationOptions options = new() {
			Identifier = "build.42",
			ApplicationName = "icod-terminal",
			NotificationTypes = [ "build", "ci" ],
			FocusOnActivation = false,
			Occasion = KittyNotificationOccasion.Unfocused,
			Urgency = KittyNotificationUrgency.Critical,
			Expiration = TimeSpan.FromMilliseconds( 1500 ),
			SoundName = "silent",
			IconNames = [ "info" ]
		};

		byte[][] frames = OscWriter.EncodeOsc99NotificationFrames(
			"Build",
			"Complete",
			options
		);

		Assert.Equal( 2, frames.Length );
		string first = Encoding.ASCII.GetString( frames[ 0 ] );
		Assert.Contains( "i=build.42", first, StringComparison.Ordinal );
		Assert.Contains( "f=aWNvZC10ZXJtaW5hbA==", first, StringComparison.Ordinal );
		Assert.Contains( "t=YnVpbGQ=", first, StringComparison.Ordinal );
		Assert.Contains( "t=Y2k=", first, StringComparison.Ordinal );
		Assert.Contains( "a=-focus", first, StringComparison.Ordinal );
		Assert.Contains( "o=unfocused", first, StringComparison.Ordinal );
		Assert.Contains( "u=2", first, StringComparison.Ordinal );
		Assert.Contains( "w=1500", first, StringComparison.Ordinal );
		Assert.Contains( "s=c2lsZW50", first, StringComparison.Ordinal );
		Assert.Contains( "n=aW5mbw==", first, StringComparison.Ordinal );
		Assert.Contains( "p=title:e=1:d=0;QnVpbGQ=", first, StringComparison.Ordinal );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=build.42:p=body:e=1:d=1;Q29tcGxldGU=\u001b\\"
			),
			frames[ 1 ]
		);
	}

	[Fact]
	public void ChunksEncodedPayloadAtProtocolBoundary() {
		string title = new( 'x', 3073 );
		KittyNotificationOptions options = new() {
			Identifier = "chunk.test"
		};

		byte[][] frames = OscWriter.EncodeOsc99NotificationFrames(
			title,
			string.Empty,
			options
		);

		Assert.Equal( 2, frames.Length );
		string first = Encoding.ASCII.GetString( frames[ 0 ] );
		string second = Encoding.ASCII.GetString( frames[ 1 ] );
		Assert.Contains( "i=chunk.test:p=title:e=1:d=0;", first, StringComparison.Ordinal );
		Assert.Contains( "i=chunk.test:p=title:e=1:d=1;", second, StringComparison.Ordinal );
		string firstPayload = GetPayload( first );
		string secondPayload = GetPayload( second );
		Assert.Equal( 4096, firstPayload.Length );
		Assert.Equal( 4, secondPayload.Length );
	}

	[Fact]
	public void EncodesCloseRequest() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b]99;i=build.42:p=close;\u001b\\"
			),
			OscWriter.EncodeOsc99CloseFrame( "build.42" )
		);
	}

	[Fact]
	public void RejectsInvalidIdentifiersAndIconData() {
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc99NotificationFrames(
				"title",
				string.Empty,
				new KittyNotificationOptions {
					Identifier = "bad:id"
				}
			)
		);
		Assert.Throws<ArgumentException>(
			() => OscWriter.EncodeOsc99NotificationFrames(
				"title",
				string.Empty,
				new KittyNotificationOptions {
					IconData = [ 1, 2, 3 ],
					IconDataIdentifier = "icon1"
				}
			)
		);
	}

	[Fact]
	public async Task WriterUsesNonCancellableWritesAndNoFlush() {
		RecordingOutput output = new();

		await OscWriter.WriteOsc99NotificationAsync(
			output,
			"Build",
			"Complete",
			new KittyNotificationOptions {
				Identifier = "build.42"
			}
		);

		Assert.Equal( 2, output.Writes.Count );
		Assert.All(
			output.CancellationTokens,
			token => Assert.False( token.CanBeCanceled )
		);
		Assert.Equal( 0, output.FlushCount );
	}

	private static string GetPayload(
		string frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		int separator = frame.IndexOf( ';', frame.IndexOf( ';' ) + 1 );
		Assert.True( 0 <= separator );
		return frame.Substring(
			separator + 1,
			frame.Length - separator - 3
		);
	}

	private sealed class RecordingOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> CancellationTokens {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			this.Writes.Add( buffer.ToArray() );
			this.CancellationTokens.Add( cancellationToken );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}
}
