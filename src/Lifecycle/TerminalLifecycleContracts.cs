namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>
/// Identifies a normalized terminal or process lifecycle event observed by a
/// live <see cref="TerminalSession"/>.
/// </summary>
public enum TerminalLifecycleEventKind {
	/// <summary>The terminal dimensions may have changed.</summary>
	Resize,

	/// <summary>An interactive interrupt request was observed.</summary>
	Interrupt,

	/// <summary>A process termination request was observed.</summary>
	Termination,

	/// <summary>The session has restored host state in preparation for suspension.</summary>
	Suspending,

	/// <summary>The process resumed and session-owned host state was re-entered.</summary>
	Resumed
}

/// <summary>
/// Represents one normalized terminal or process lifecycle event.
/// </summary>
public sealed class TerminalLifecycleEvent {
	internal TerminalLifecycleEvent(
		TerminalLifecycleEventKind kind,
		TerminalSize? size = null
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}

		this.Kind = kind;
		this.Size = size;
	}

	/// <summary>Gets the lifecycle event kind.</summary>
	public TerminalLifecycleEventKind Kind {
		get;
	}

	/// <summary>
	/// Gets the observed terminal size for resize/resume events when live dimensions
	/// were available.
	/// </summary>
	public TerminalSize? Size {
		get;
	}
}

/// <summary>
/// Identifies one host lifecycle signal before session-level policy is applied.
/// </summary>
internal enum TerminalLifecycleSignalKind {
	Resize,
	Interrupt,
	Termination,
	Suspend,
	Resume
}

/// <summary>Represents one queued host lifecycle signal.</summary>
internal readonly record struct TerminalLifecycleSignal {
	internal TerminalLifecycleSignal(
		TerminalLifecycleSignalKind kind
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}

		this.Kind = kind;
	}

	internal TerminalLifecycleSignalKind Kind {
		get;
	}
}

/// <summary>
/// Supplies queued host lifecycle notifications without exposing operating-system
/// signal APIs to ordinary terminal consumers.
/// </summary>
internal interface ITerminalLifecycleSource : IDisposable {
	ValueTask<TerminalLifecycleSignal> ReadAsync(
		CancellationToken cancellationToken = default
	);
}

/// <summary>
/// Completes a previously intercepted POSIX suspend request after session-owned
/// host state has been restored.
/// </summary>
internal interface ITerminalSuspendController {
	TerminalControlMutationResult SuspendCurrentProcess();
}
