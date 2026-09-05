namespace Icod.Terminal.Tests.Control;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the frozen T122 semantic OSC 133 marker model and its exhaustive T121 mapping.
/// </summary>
public sealed class TerminalSemanticPromptMarkerTests {
	[Fact]
	public void FrozenSemanticVocabularyContainsExactlyFiveMarkerKinds() {
		Assert.Equal(
			5,
			Enum.GetValues<TerminalSemanticPromptMarkerKind>().Length
		);
	}

	[Fact]
	public void EverySemanticMarkerMapsToItsCanonicalFrame() {
		(
			TerminalSemanticPromptMarker Marker,
			TerminalSemanticPromptMarkerKind Kind,
			string Expected
		)[] cases = [
			(
				TerminalSemanticPromptMarker.CreatePromptStart(),
				TerminalSemanticPromptMarkerKind.PromptStart,
				"\u001b]133;A\u001b\\"
			),
			(
				TerminalSemanticPromptMarker.CreateCommandInputStart(),
				TerminalSemanticPromptMarkerKind.CommandInputStart,
				"\u001b]133;B\u001b\\"
			),
			(
				TerminalSemanticPromptMarker.CreateCommandOutputStart(),
				TerminalSemanticPromptMarkerKind.CommandOutputStart,
				"\u001b]133;C\u001b\\"
			),
			(
				TerminalSemanticPromptMarker.CreateCommandFinished( 0 ),
				TerminalSemanticPromptMarkerKind.CommandFinished,
				"\u001b]133;D;0\u001b\\"
			),
			(
				TerminalSemanticPromptMarker.CreateCommandAborted(),
				TerminalSemanticPromptMarkerKind.CommandAborted,
				"\u001b]133;D\u001b\\"
			)
		];

		Assert.Equal(
			Enum.GetValues<TerminalSemanticPromptMarkerKind>().Length,
			cases.Length
		);
		foreach ( var testCase in cases ) {
			Assert.Equal(
				testCase.Kind,
				testCase.Marker.Kind
			);
			Assert.Equal(
				Encoding.ASCII.GetBytes( testCase.Expected ),
				TerminalSemanticPromptMarkerCodec.EncodeFrame( testCase.Marker )
			);
		}
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 127 )]
	[InlineData( 255 )]
	public void CommandFinishedRetainsTypedExitStatus(
		int exitStatus
	) {
		TerminalSemanticPromptMarker marker = TerminalSemanticPromptMarker.CreateCommandFinished(
			(byte)exitStatus
		);

		Assert.Equal(
			TerminalSemanticPromptMarkerKind.CommandFinished,
			marker.Kind
		);
		Assert.True( marker.HasExitStatus );
		Assert.Equal(
			(byte)exitStatus,
			marker.ExitStatus
		);
	}

	[Fact]
	public void AbortIsDistinctFromSuccessfulCompletion() {
		TerminalSemanticPromptMarker aborted = TerminalSemanticPromptMarker.CreateCommandAborted();
		TerminalSemanticPromptMarker success = TerminalSemanticPromptMarker.CreateCommandFinished( 0 );

		Assert.Equal(
			TerminalSemanticPromptMarkerKind.CommandAborted,
			aborted.Kind
		);
		Assert.False( aborted.HasExitStatus );
		Assert.Equal(
			TerminalSemanticPromptMarkerKind.CommandFinished,
			success.Kind
		);
		Assert.True( success.HasExitStatus );
		Assert.Equal(
			(byte)0,
			success.ExitStatus
		);
		Assert.NotEqual(
			TerminalSemanticPromptMarkerCodec.EncodeFrame( aborted ),
			TerminalSemanticPromptMarkerCodec.EncodeFrame( success )
		);
	}

	[Fact]
	public void NonCompletionMarkerDoesNotExposeSyntheticExitStatus() {
		TerminalSemanticPromptMarker marker = TerminalSemanticPromptMarker.CreateCommandAborted();

		Assert.Throws<InvalidOperationException>(
			() => {
				_ = marker.ExitStatus;
			}
		);
	}

	[Fact]
	public void DefaultMarkerIsRejectedInsteadOfBecomingPromptStart() {
		TerminalSemanticPromptMarker marker = default;

		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalSemanticPromptMarkerCodec.EncodeFrame( marker )
		);
	}

	[Fact]
	public async Task SemanticWriterPreservesCompletionStatusAndT121CommitSemantics() {
		RecordingOutput output = new();
		TerminalSemanticPromptMarker marker = TerminalSemanticPromptMarker.CreateCommandFinished( 255 );

		await TerminalSemanticPromptMarkerCodec.WriteAsync(
			output,
			marker,
			CancellationToken.None
		);

		Assert.Single( output.Writes );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]133;D;255\u001b\\" ),
			output.Writes[ 0 ]
		);
		Assert.False( output.WriteCancellationTokens[ 0 ].CanBeCanceled );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task PreCancelledSemanticWriterEmitsNothing() {
		RecordingOutput output = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => TerminalSemanticPromptMarkerCodec.WriteAsync(
				output,
				TerminalSemanticPromptMarker.CreatePromptStart(),
				cancellation.Token
			).AsTask()
		);

		Assert.Empty( output.Writes );
		Assert.Equal( 0, output.FlushCount );
	}

	private sealed class RecordingOutput : ITerminalOutput {
		internal List<byte[]> Writes {
			get;
		} = [];

		internal List<CancellationToken> WriteCancellationTokens {
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
			this.WriteCancellationTokens.Add( cancellationToken );
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
