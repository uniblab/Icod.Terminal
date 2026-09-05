namespace Icod.Terminal;

/// <summary>
/// Identifies one semantic OSC 133 prompt/command marker in the frozen 0.12 contract.
/// </summary>
internal enum TerminalSemanticPromptMarkerKind {
	PromptStart = 1,
	CommandInputStart = 2,
	CommandOutputStart = 3,
	CommandFinished = 4,
	CommandAborted = 5
}

/// <summary>
/// Represents one validated semantic OSC 133 prompt/command marker.
/// </summary>
internal readonly struct TerminalSemanticPromptMarker {
	private readonly byte exitStatus;

	private TerminalSemanticPromptMarker(
		TerminalSemanticPromptMarkerKind kind,
		byte exitStatus
	) {
		this.Kind = kind;
		this.exitStatus = exitStatus;
	}

	internal TerminalSemanticPromptMarkerKind Kind {
		get;
	}

	internal bool HasExitStatus {
		get {
			return TerminalSemanticPromptMarkerKind.CommandFinished == this.Kind;
		}
	}

	internal byte ExitStatus {
		get {
			if ( !this.HasExitStatus ) {
				throw new InvalidOperationException(
					"Only a completed OSC 133 command marker carries an exit status."
				);
			}

			return this.exitStatus;
		}
	}

	internal static TerminalSemanticPromptMarker CreatePromptStart() {
		return new TerminalSemanticPromptMarker(
			TerminalSemanticPromptMarkerKind.PromptStart,
			exitStatus: 0
		);
	}

	internal static TerminalSemanticPromptMarker CreateCommandInputStart() {
		return new TerminalSemanticPromptMarker(
			TerminalSemanticPromptMarkerKind.CommandInputStart,
			exitStatus: 0
		);
	}

	internal static TerminalSemanticPromptMarker CreateCommandOutputStart() {
		return new TerminalSemanticPromptMarker(
			TerminalSemanticPromptMarkerKind.CommandOutputStart,
			exitStatus: 0
		);
	}

	internal static TerminalSemanticPromptMarker CreateCommandFinished(
		byte exitStatus
	) {
		return new TerminalSemanticPromptMarker(
			TerminalSemanticPromptMarkerKind.CommandFinished,
			exitStatus
		);
	}

	internal static TerminalSemanticPromptMarker CreateCommandAborted() {
		return new TerminalSemanticPromptMarker(
			TerminalSemanticPromptMarkerKind.CommandAborted,
			exitStatus: 0
		);
	}
}

/// <summary>
/// Maps the frozen semantic OSC 133 marker model to the specialized T121 writer.
/// </summary>
internal static class TerminalSemanticPromptMarkerCodec {
	internal static byte[] EncodeFrame(
		TerminalSemanticPromptMarker marker
	) {
		Validate( marker );

		return marker.Kind switch {
			TerminalSemanticPromptMarkerKind.PromptStart
				=> OscWriter.EncodeOsc133PromptStartFrame(),
			TerminalSemanticPromptMarkerKind.CommandInputStart
				=> OscWriter.EncodeOsc133CommandInputStartFrame(),
			TerminalSemanticPromptMarkerKind.CommandOutputStart
				=> OscWriter.EncodeOsc133CommandOutputStartFrame(),
			TerminalSemanticPromptMarkerKind.CommandFinished
				=> OscWriter.EncodeOsc133CommandFinishedFrame( marker.ExitStatus ),
			TerminalSemanticPromptMarkerKind.CommandAborted
				=> OscWriter.EncodeOsc133CommandAbortedFrame(),
			_ => throw CreateInvalidMarkerException( marker )
		};
	}

	internal static ValueTask WriteAsync(
		ITerminalOutput output,
		TerminalSemanticPromptMarker marker,
		CancellationToken cancellationToken = default
	) {
		Validate( marker );
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();

		return marker.Kind switch {
			TerminalSemanticPromptMarkerKind.PromptStart
				=> OscWriter.WriteOsc133PromptStartAsync(
					output,
					cancellationToken
				),
			TerminalSemanticPromptMarkerKind.CommandInputStart
				=> OscWriter.WriteOsc133CommandInputStartAsync(
					output,
					cancellationToken
				),
			TerminalSemanticPromptMarkerKind.CommandOutputStart
				=> OscWriter.WriteOsc133CommandOutputStartAsync(
					output,
					cancellationToken
				),
			TerminalSemanticPromptMarkerKind.CommandFinished
				=> OscWriter.WriteOsc133CommandFinishedAsync(
					output,
					marker.ExitStatus,
					cancellationToken
				),
			TerminalSemanticPromptMarkerKind.CommandAborted
				=> OscWriter.WriteOsc133CommandAbortedAsync(
					output,
					cancellationToken
				),
			_ => throw CreateInvalidMarkerException( marker )
		};
	}

	internal static void Validate(
		TerminalSemanticPromptMarker marker
	) {
		if ( !Enum.IsDefined( marker.Kind ) ) {
			throw CreateInvalidMarkerException( marker );
		}
	}

	private static ArgumentOutOfRangeException CreateInvalidMarkerException(
		TerminalSemanticPromptMarker marker
	) {
		return new ArgumentOutOfRangeException(
			nameof( marker ),
			marker.Kind,
			"The semantic prompt marker is not defined by the frozen 0.12 contract."
		);
	}
}
