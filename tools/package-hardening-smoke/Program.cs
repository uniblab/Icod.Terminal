using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;

const string EnablePaste = "<P+>";
const string DisablePaste = "<P->";
const string EnterAlternateScreen = "<A+>";
const string ExitAlternateScreen = "<A->";

TestTerminalLifecycleSource lifecycle = new() {
	AutoResume = true
};
RecordingTransport transport = new();
await using TerminalSession session = await OpenSessionAsync(
	lifecycle,
	transport
);
BlockingParticipant participant = new();
using IDisposable registration = session.RegisterLifecycleParticipant(
	participant
);
using CancellationTokenSource timeout = new(
	TimeSpan.FromSeconds( 5 )
);

lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
await participant.WaitUntilPreparingAsync().WaitAsync( timeout.Token );
Require(
	!session.IsStateValid,
	"The package did not mark terminal state released during suspend preparation."
);

await RequireThrowsAsync<InvalidOperationException>(
	() => session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true
		}
	).AsTask(),
	"Rich-input acquisition was not rejected while lifecycle state was released."
);
await RequireThrowsAsync<InvalidOperationException>(
	() => session.AcquirePresentationAsync(
		new TerminalPresentationOptions {
			AlternateScreen = true
		}
	).AsTask(),
	"Presentation acquisition was not rejected while lifecycle state was released."
);
Require(
	0 == transport.Writes.Count,
	"Rejected state acquisition emitted terminal-control output."
);

participant.ReleasePreparation();
TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
	timeout.Token
);
TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
	timeout.Token
);
Require(
	TerminalLifecycleEventKind.Suspending == suspending.Kind
		&& TerminalLifecycleEventKind.Resumed == resumed.Kind
		&& session.IsStateValid,
	"The package did not complete lifecycle re-entry before restoring public state acquisition."
);

TerminalInputProtocolLease inputLease = (
	await session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true
		}
	)
).GetRequiredValue();
TerminalPresentationLease presentationLease = (
	await session.AcquirePresentationAsync(
		new TerminalPresentationOptions {
			AlternateScreen = true
		}
	)
).GetRequiredValue();

RequireSequence(
	transport.Writes,
	EnablePaste,
	EnterAlternateScreen
);

await presentationLease.DisposeAsync();
await inputLease.DisposeAsync();
RequireSequence(
	transport.Writes,
	EnablePaste,
	EnterAlternateScreen,
	ExitAlternateScreen,
	DisablePaste
);

Console.WriteLine(
	"Icod.Terminal 0.18 package lifecycle/state-acquisition hardening smoke passed."
);

static async Task RequireThrowsAsync<TException>(
	Func<Task> action,
	string message
) where TException : Exception {
	ArgumentNullException.ThrowIfNull( action );
	ArgumentNullException.ThrowIfNull( message );
	try {
		await action().ConfigureAwait( false );
	} catch ( TException ) {
		return;
	}
	throw new InvalidOperationException( message );
}

static void Require(
	bool condition,
	string message
) {
	ArgumentNullException.ThrowIfNull( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

static void RequireSequence(
	IReadOnlyList<string> actual,
	params string[] expected
) {
	ArgumentNullException.ThrowIfNull( actual );
	ArgumentNullException.ThrowIfNull( expected );
	Require(
		actual.SequenceEqual( expected, StringComparer.Ordinal ),
		"Unexpected terminal-control sequence: "
			+ string.Join( " | ", actual )
	);
}

static ValueTask<TerminalSession> OpenSessionAsync(
	TestTerminalLifecycleSource lifecycle,
	RecordingTransport transport
) {
	ArgumentNullException.ThrowIfNull( lifecycle );
	ArgumentNullException.ThrowIfNull( transport );

	TerminalDescription terminal = new TerminalDescriptionBuilder(
		"package-hardening-smoke"
	)
		.SetString(
			StringCapability.EnterCursorAddressingMode,
			EnterAlternateScreen
		)
		.SetString(
			StringCapability.ExitCursorAddressingMode,
			ExitAlternateScreen
		)
		.SetExtendedString( "BE", EnablePaste )
		.SetExtendedString( "BD", DisablePaste )
		.SetExtendedString( "PS", "\u001b[200~" )
		.SetExtendedString( "PE", "\u001b[201~" )
		.Build();

	return TerminalSession.OpenAsync(
		new TestTerminalControlProvider(),
		TerminalEndpoint.StandardInput,
		TerminalEndpoint.StandardOutput,
		transport,
		transport,
		new TerminalSessionOptions {
			TerminalOverride = terminal,
			ConfigureOutput = false,
			LifecycleSource = lifecycle
		}
	);
}

internal sealed class BlockingParticipant : ITerminalSessionLifecycleParticipant {
	private readonly TaskCompletionSource preparing = new(
		TaskCreationOptions.RunContinuationsAsynchronously
	);
	private readonly TaskCompletionSource release = new(
		TaskCreationOptions.RunContinuationsAsynchronously
	);

	internal Task WaitUntilPreparingAsync() {
		return this.preparing.Task;
	}

	internal void ReleasePreparation() {
		this.release.TrySetResult();
	}

	public async ValueTask PrepareForTerminalSuspendAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.preparing.TrySetResult();
		await this.release.Task.WaitAsync(
			cancellationToken
		).ConfigureAwait( false );
	}

	public ValueTask ResumeAfterTerminalSuspendAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}

internal sealed class TestTerminalLifecycleSource
	: ITerminalLifecycleSource,
	  ITerminalSuspendController {
	private readonly Channel<TerminalLifecycleSignal> signals =
		Channel.CreateUnbounded<TerminalLifecycleSignal>();

	internal bool AutoResume {
		get;
		init;
	}

	internal void Publish(
		TerminalLifecycleSignalKind kind
	) {
		if ( !this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) ) ) {
			throw new InvalidOperationException(
				"The package-smoke lifecycle signal could not be queued."
			);
		}
	}

	public ValueTask<TerminalLifecycleSignal> ReadAsync(
		CancellationToken cancellationToken = default
	) {
		return this.signals.Reader.ReadAsync( cancellationToken );
	}

	public TerminalControlMutationResult SuspendCurrentProcess() {
		if ( this.AutoResume ) {
			this.Publish( TerminalLifecycleSignalKind.Resume );
		}
		return TerminalControlMutationResult.Success();
	}

	public void Dispose() {
		this.signals.Writer.TryComplete();
	}
}

internal sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
	internal List<string> Writes {
		get;
	} = [];

	public ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult( 0 );
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.Writes.Add( Encoding.Latin1.GetString( buffer.Span ) );
		return ValueTask.CompletedTask;
	}

	public ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}

internal sealed class TestTerminalControlProvider : ITerminalControlProvider {
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
		0,
		0,
		0,
		0x0002UL,
		new byte[ 32 ],
		0,
		32,
		0,
		new TerminalSpeed( 13, 9600 ),
		new TerminalSpeed( 13, 9600 )
	);

	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
			)
		);
	}

	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalSize>.Unavailable(
			"Live size is not required by package hardening smoke."
		);
	}

	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalModeSnapshot>.Available(
			this.baseline
		);
	}

	public TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( mode );
		if ( !Enum.IsDefined( timing ) ) {
			throw new ArgumentOutOfRangeException( nameof( timing ) );
		}
		return TerminalControlMutationResult.Success();
	}
}
