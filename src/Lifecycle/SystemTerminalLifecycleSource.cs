namespace Icod.Terminal;

using System.Runtime.InteropServices;
using System.Threading.Channels;

/// <summary>
/// Observes process and terminal lifecycle events for supported desktop hosts.
/// Native callbacks only enqueue compact notifications; ordinary processing occurs
/// on the managed session lifecycle pump.
/// </summary>
internal sealed partial class SystemTerminalLifecycleSource
	: ITerminalLifecycleSource,
	  ITerminalSuspendController {
	private const int LinuxSigTstp = 20;
	private const int MacOsSigTstp = 18;

	private readonly Channel<TerminalLifecycleSignal> signals;
	private readonly List<IDisposable> registrations = [];

	private ConsoleCancelEventHandler? consoleCancelHandler;
	private int allowSuspendDelivery;
	private int resizePending;
	private int disposed;

	private SystemTerminalLifecycleSource() {
		this.signals = Channel.CreateUnbounded<TerminalLifecycleSignal>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);

		if ( OperatingSystem.IsWindows() ) {
			this.consoleCancelHandler = this.HandleConsoleCancel;
			Console.CancelKeyPress += this.consoleCancelHandler;
			return;
		}

		this.registrations.Add(
			PosixSignalRegistration.Create(
				PosixSignal.SIGWINCH,
				context => {
					context.Cancel = true;
					this.Publish( TerminalLifecycleSignalKind.Resize );
				}
			)
		);
		this.registrations.Add(
			PosixSignalRegistration.Create(
				PosixSignal.SIGCONT,
				context => {
					context.Cancel = false;
					this.Publish( TerminalLifecycleSignalKind.Resume );
				}
			)
		);
		this.registrations.Add(
			PosixSignalRegistration.Create(
				PosixSignal.SIGTSTP,
				this.HandleSuspendSignal
			)
		);

		this.RegisterTerminationSignal(
			PosixSignal.SIGINT,
			TerminalLifecycleSignalKind.Interrupt
		);
		this.RegisterTerminationSignal(
			PosixSignal.SIGTERM,
			TerminalLifecycleSignalKind.Termination
		);
		this.RegisterTerminationSignal(
			PosixSignal.SIGQUIT,
			TerminalLifecycleSignalKind.Termination
		);
		this.RegisterTerminationSignal(
			PosixSignal.SIGHUP,
			TerminalLifecycleSignalKind.Termination
		);
	}

	internal static ITerminalLifecycleSource? TryCreate() {
		if ( OperatingSystem.IsWindows()
			|| OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS() ) {
			return new SystemTerminalLifecycleSource();
		}

		return null;
	}

	public async ValueTask<TerminalLifecycleSignal> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		TerminalLifecycleSignal signal = await this.signals.Reader.ReadAsync(
			cancellationToken
		).ConfigureAwait( false );

		if ( TerminalLifecycleSignalKind.Resize == signal.Kind ) {
			Interlocked.Exchange( ref this.resizePending, 0 );
		}

		return signal;
	}

	public TerminalControlMutationResult SuspendCurrentProcess() {
		if ( 0 != Volatile.Read( ref this.disposed ) ) {
			return TerminalControlMutationResult.Unavailable(
				"The terminal lifecycle source has already been disposed."
			);
		}

		int signalNumber;
		if ( OperatingSystem.IsLinux() ) {
			signalNumber = LinuxSigTstp;
		} else if ( OperatingSystem.IsMacOS() ) {
			signalNumber = MacOsSigTstp;
		} else {
			return TerminalControlMutationResult.Unsupported(
				"Process suspension is supported only by the POSIX lifecycle source."
			);
		}

		Interlocked.Exchange( ref this.allowSuspendDelivery, 1 );

		try {
			int result = NativeRaise( signalNumber );
			if ( 0 == result ) {
				return TerminalControlMutationResult.Success();
			}

			Interlocked.Exchange( ref this.allowSuspendDelivery, 0 );
			return TerminalControlMutationResult.Failed(
				$"The host rejected the suspend signal with error code {result}."
			);
		} catch ( DllNotFoundException exception ) {
			Interlocked.Exchange( ref this.allowSuspendDelivery, 0 );
			return TerminalControlMutationResult.Unsupported( exception.Message );
		} catch ( EntryPointNotFoundException exception ) {
			Interlocked.Exchange( ref this.allowSuspendDelivery, 0 );
			return TerminalControlMutationResult.Unsupported( exception.Message );
		}
	}

	public void Dispose() {
		if ( 0 != Interlocked.Exchange( ref this.disposed, 1 ) ) {
			return;
		}

		if ( this.consoleCancelHandler is not null ) {
			Console.CancelKeyPress -= this.consoleCancelHandler;
			this.consoleCancelHandler = null;
		}

		foreach ( IDisposable registration in this.registrations ) {
			registration.Dispose();
		}
		this.registrations.Clear();

		this.signals.Writer.TryComplete();
	}

	private void HandleConsoleCancel(
		object? sender,
		ConsoleCancelEventArgs eventArgs
	) {
		ArgumentNullException.ThrowIfNull( eventArgs );

		eventArgs.Cancel = true;
		this.Publish(
			ConsoleSpecialKey.ControlC == eventArgs.SpecialKey
				? TerminalLifecycleSignalKind.Interrupt
				: TerminalLifecycleSignalKind.Termination
		);
	}

	private void HandleSuspendSignal(
		PosixSignalContext context
	) {
		if ( 0 != Interlocked.Exchange( ref this.allowSuspendDelivery, 0 ) ) {
			context.Cancel = false;
			return;
		}

		context.Cancel = true;
		this.Publish( TerminalLifecycleSignalKind.Suspend );
	}

	private void RegisterTerminationSignal(
		PosixSignal signal,
		TerminalLifecycleSignalKind kind
	) {
		this.registrations.Add(
			PosixSignalRegistration.Create(
				signal,
				context => {
					context.Cancel = true;
					this.Publish( kind );
				}
			)
		);
	}

	private void Publish(
		TerminalLifecycleSignalKind kind
	) {
		if ( 0 != Volatile.Read( ref this.disposed ) ) {
			return;
		}

		if ( TerminalLifecycleSignalKind.Resize == kind
			&& 0 != Interlocked.Exchange( ref this.resizePending, 1 ) ) {
			return;
		}

		if ( !this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			&& ( TerminalLifecycleSignalKind.Resize == kind ) ) {
			Interlocked.Exchange( ref this.resizePending, 0 );
		}
	}

	[LibraryImport(
		"libc",
		EntryPoint = "raise"
	)]
	private static partial int NativeRaise( int signal );
}
