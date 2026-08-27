# Icod.Terminal 0.1 Public API Baseline

**Baseline line:** `0.1.x`
**Reviewed at:** `0.1.0-alpha.12`
**Purpose:** intentional source-level public API inventory before the `0.1.0` release

This document records the public surface reviewed by T12B. It is an intentional pre-1.0 baseline, not a `1.0` compatibility guarantee. Inherited `System.Object` members and implementation details are omitted.

Types named from `Icod.TermInfo` and `Icod.Timing` are owned by those packages and are listed where they appear in the `Icod.Terminal` signature surface.

---

## 1. Control and endpoint contracts

```csharp
namespace Icod.Terminal;

public enum TerminalControlStatus {
    Available,
    Unavailable,
    Unsupported,
    Failed
}

public enum TerminalPlatformKind {
    PosixTermios,
    WindowsConsole
}

public enum TerminalEndpointKind {
    FileDescriptor,
    Path
}

public enum TerminalModeApplyTiming {
    Immediately,
    AfterOutputDrained,
    AfterOutputDrainedAndInputDiscarded
}

public enum TerminalConsoleDirection {
    Input,
    Output
}

[Flags]
public enum TerminalControlCapabilities {
    None = 0,
    Attachment = 1 << 0,
    Pathname = 1 << 1,
    ModeRead = 1 << 2,
    ModeWrite = 1 << 3,
    Speeds = 1 << 4,
    ControlCharacters = 1 << 5,
    MachineSerialization = 1 << 6,
    LiveSize = 1 << 7
}

public sealed class TerminalEndpoint {
    public static TerminalEndpoint StandardInput { get; }
    public static TerminalEndpoint StandardOutput { get; }
    public static TerminalEndpoint StandardError { get; }

    public TerminalEndpointKind Kind { get; }
    public int? FileDescriptor { get; }
    public string? Path { get; }
    public string DisplayName { get; }

    public static TerminalEndpoint ForFileDescriptor( int fileDescriptor );
    public static TerminalEndpoint ForPath( string path );
}

public sealed class TerminalControlResult<T> {
    public TerminalControlStatus Status { get; }
    public T? Value { get; }
    public string? Message { get; }
    public int? NativeErrorCode { get; }
    public bool IsAvailable { get; }

    public T GetRequiredValue();

    public static TerminalControlResult<T> Available( T value );
    public static TerminalControlResult<T> Unavailable(
        string? message,
        int? nativeErrorCode = null
    );
    public static TerminalControlResult<T> Unsupported( string? message );
    public static TerminalControlResult<T> Failed(
        string? message,
        int? nativeErrorCode = null
    );
}

public sealed class TerminalControlMutationResult {
    public TerminalControlStatus Status { get; }
    public string? Message { get; }
    public int? NativeErrorCode { get; }
    public bool Succeeded { get; }

    public static TerminalControlMutationResult Success();
    public static TerminalControlMutationResult Unavailable(
        string? message,
        int? nativeErrorCode = null
    );
    public static TerminalControlMutationResult Unsupported( string? message );
    public static TerminalControlMutationResult Failed(
        string? message,
        int? nativeErrorCode = null
    );
}

public sealed class TerminalEndpointObservation {
    public TerminalEndpointObservation(
        bool isTerminal,
        string? pathname,
        TerminalPlatformKind? platform,
        TerminalControlCapabilities capabilities
    );

    public bool IsTerminal { get; }
    public string? Pathname { get; }
    public TerminalPlatformKind? Platform { get; }
    public TerminalControlCapabilities Capabilities { get; }
}

public readonly record struct TerminalSpeed {
    public TerminalSpeed( ulong nativeCode, ulong? baudRate );

    public ulong NativeCode { get; }
    public ulong? BaudRate { get; }
}
```

---

## 2. Complete mode snapshots

```csharp
public sealed class TerminalModeSnapshot {
    public TerminalPlatformKind Platform { get; }

    public ulong InputFlags { get; }
    public ulong OutputFlags { get; }
    public ulong ControlFlags { get; }
    public ulong LocalFlags { get; }
    public IReadOnlyList<byte> ControlCharacters { get; }
    public byte DisabledControlCharacter { get; }
    public int NativeFlagWidth { get; }
    public byte? LineDiscipline { get; }
    public TerminalSpeed? InputSpeed { get; }
    public TerminalSpeed? OutputSpeed { get; }

    public TerminalConsoleDirection? ConsoleDirection { get; }
    public uint? ConsoleMode { get; }

    public static TerminalModeSnapshot CreatePosix(
        ulong inputFlags,
        ulong outputFlags,
        ulong controlFlags,
        ulong localFlags,
        IEnumerable<byte> controlCharacters,
        byte disabledControlCharacter,
        int nativeFlagWidth,
        byte? lineDiscipline,
        TerminalSpeed inputSpeed,
        TerminalSpeed outputSpeed
    );

    public static TerminalModeSnapshot CreateWindowsConsole(
        TerminalConsoleDirection direction,
        uint mode
    );

    public TerminalModeSnapshot WithPosixSerializedState(
        ulong inputFlags,
        ulong outputFlags,
        ulong controlFlags,
        ulong localFlags,
        IEnumerable<byte> controlCharacters
    );
}

public interface ITerminalControlProvider {
    TerminalControlResult<TerminalEndpointObservation> Observe(
        TerminalEndpoint endpoint
    );

    TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize(
        TerminalEndpoint endpoint
    );

    TerminalControlResult<TerminalModeSnapshot> GetMode(
        TerminalEndpoint endpoint
    );

    TerminalControlMutationResult SetMode(
        TerminalEndpoint endpoint,
        TerminalModeSnapshot mode,
        TerminalModeApplyTiming timing
    );
}

public sealed class SystemTerminalControlProvider : ITerminalControlProvider {
    public static SystemTerminalControlProvider Instance { get; }

    public TerminalControlResult<TerminalEndpointObservation> Observe(
        TerminalEndpoint endpoint
    );

    public TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize(
        TerminalEndpoint endpoint
    );

    public TerminalControlResult<TerminalModeSnapshot> GetMode(
        TerminalEndpoint endpoint
    );

    public TerminalControlMutationResult SetMode(
        TerminalEndpoint endpoint,
        TerminalModeSnapshot mode,
        TerminalModeApplyTiming timing
    );
}
```

---

## 3. Semantic mode and serialization helpers

```csharp
public enum TerminalInputMode {
    Canonical,
    CBreak,
    Raw
}

public static class TerminalInputModePolicy {
    public static TerminalModeSnapshot Configure(
        TerminalModeSnapshot baseline,
        TerminalInputMode inputMode,
        bool echoInput
    );

    public static TerminalControlMutationResult Apply(
        ITerminalControlProvider provider,
        TerminalEndpoint endpoint,
        TerminalModeSnapshot baseline,
        TerminalInputMode inputMode,
        bool echoInput
    );
}

public static class TerminalModeCodec {
    public static string Serialize( TerminalModeSnapshot mode );

    public static bool TryRestore(
        string serialized,
        TerminalModeSnapshot baseline,
        out TerminalModeSnapshot? restored,
        out string? error
    );
}
```

---

## 4. Byte transport contracts

```csharp
public interface ITerminalInput {
    ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    );
}

public interface ITerminalOutput {
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    );

    ValueTask FlushAsync(
        CancellationToken cancellationToken = default
    );
}
```

The built-in stream adapters are intentionally internal. The interfaces are the public injection seam.

---

## 5. Decoded input and unified events

```csharp
public enum TerminalInputEventKind {
    Text,
    Key,
    EndOfInput
}

public enum TerminalKey {
    None,
    Character,
    Enter,
    Space,
    Escape,
    Backspace,
    Tab,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
    Insert,
    Delete,
    Function
}

[Flags]
public enum TerminalKeyModifiers {
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4
}

public sealed class TerminalInputEvent {
    public TerminalInputEventKind Kind { get; }
    public TerminalKey Key { get; }
    public System.Text.Rune? Character { get; }
    public TerminalKeyModifiers Modifiers { get; }
    public int? FunctionKeyNumber { get; }
}

public enum TerminalEventKind {
    Input,
    Lifecycle,
    Timeout,
    Cancelled
}

public sealed class TerminalEvent {
    public TerminalEventKind Kind { get; }
    public TerminalInputEvent? Input { get; }
    public TerminalLifecycleEvent? Lifecycle { get; }
}
```

Decoded event construction is intentionally internal so the library retains event-shape invariants.

---

## 6. Lifecycle contracts

```csharp
public enum TerminalLifecycleEventKind {
    Resize,
    Interrupt,
    Termination,
    Suspending,
    Resumed
}

public sealed class TerminalLifecycleEvent {
    public TerminalLifecycleEventKind Kind { get; }
    public Icod.TermInfo.TerminalSize? Size { get; }
}

public interface ITerminalSessionLifecycleParticipant {
    ValueTask PrepareForTerminalSuspendAsync(
        CancellationToken cancellationToken = default
    );

    ValueTask ResumeAfterTerminalSuspendAsync(
        CancellationToken cancellationToken = default
    );
}
```

Host signal/source types remain internal.

---

## 7. Presentation contracts

```csharp
public enum TerminalCursorVisibility {
    Hidden,
    Normal,
    VeryVisible
}

public sealed class TerminalPresentationOptions {
    public bool AlternateScreen { get; init; }
    public bool KeypadMode { get; init; }
    public TerminalCursorVisibility? CursorVisibility { get; init; }
}

public sealed class TerminalPresentationLease : IAsyncDisposable {
    public bool AlternateScreen { get; }
    public bool KeypadMode { get; }
    public TerminalCursorVisibility? CursorVisibility { get; }

    public ValueTask DisposeAsync();
}
```

The presentation manager and capability-composition implementation remain internal.

---

## 8. Terminal identity

```csharp
public enum TerminalIdentitySource {
    ExplicitOverride,
    NamedProfile,
    PlatformFallback
}

public sealed class TerminalIdentity {
    public Icod.TermInfo.TerminalDescription Terminal { get; }
    public string? RequestedName { get; }
    public TerminalIdentitySource Source { get; }
}
```

---

## 9. Session options

```csharp
public sealed class TerminalSessionOptions {
    public TerminalInputMode InputMode { get; init; }
    public bool EchoInput { get; init; }
    public bool RequireInteractiveOutput { get; init; }

    public Icod.TermInfo.TerminalDescription? TerminalOverride { get; init; }
    public string? TerminalName { get; init; }
    public Icod.TermInfo.TerminalDatabase? TerminalDatabase { get; init; }

    public bool ConfigureOutput { get; init; }
    public bool ObserveLifecycleEvents { get; init; }

    public Icod.Timing.IMonotonicClock MonotonicClock { get; init; }
    public System.Text.Encoding ApplicationEncoding { get; init; }

    public Icod.TermInfo.PaddingMode CapabilityPaddingMode { get; init; }
    public Icod.TermInfo.ITermInfoDelayProvider? CapabilityDelayProvider { get; init; }
}
```

Defaults are part of the reviewed behavior:

- input mode: `CBreak`;
- echo: disabled;
- interactive output required;
- output setup enabled;
- automatic lifecycle observation enabled for the system provider;
- monotonic clock: `SystemMonotonicClock.Instance`;
- application encoding: strict UTF-8 without BOM;
- TermInfo padding mode: `Delay`.

---

## 10. TerminalSession

```csharp
public sealed partial class TerminalSession : IAsyncDisposable {
    public TerminalEndpoint InputEndpoint { get; }
    public TerminalEndpoint OutputEndpoint { get; }

    public TerminalEndpointObservation InputObservation { get; }
    public TerminalEndpointObservation OutputObservation { get; }

    public TerminalIdentity Identity { get; }
    public Icod.TermInfo.TerminalDescription Terminal { get; }
    public System.Text.Encoding ApplicationEncoding { get; }

    public ITerminalInput Input { get; }
    public ITerminalOutput Output { get; }
    public TerminalSessionOptions Options { get; }

    public bool IsInteractive { get; }
    public bool IsStateValid { get; }

    public bool SupportsLifecycleEvents { get; }
    public CancellationToken TerminationToken { get; }

    public static TimeSpan DefaultEscapeSequenceTimeout { get; }
    public static int MaximumBufferedInputBytes { get; }

    public static ValueTask<TerminalSession> OpenAsync(
        TerminalSessionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    public static ValueTask<TerminalSession> OpenAsync(
        ITerminalControlProvider controlProvider,
        TerminalEndpoint inputEndpoint,
        TerminalEndpoint outputEndpoint,
        ITerminalInput input,
        ITerminalOutput output,
        TerminalSessionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    public ValueTask WriteTextAsync(
        string value,
        CancellationToken cancellationToken = default
    );

    public ValueTask WriteTerminalStringAsync(
        string value,
        int affectedLines = 1,
        CancellationToken cancellationToken = default
    );

    public ValueTask<bool> WriteCapabilityAsync(
        Icod.TermInfo.StringCapability capability,
        int affectedLines = 1,
        CancellationToken cancellationToken = default
    );

    public ValueTask<TerminalEvent> ReadEventAsync(
        CancellationToken cancellationToken = default
    );

    public ValueTask<TerminalEvent> ReadEventAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );

    public ValueTask<TerminalEvent> ReadEventAsync(
        DateTimeOffset deadline,
        CancellationToken cancellationToken = default
    );

    public TerminalControlResult<Icod.TermInfo.TerminalSize> GetSize();

    public ValueTask<TerminalLifecycleEvent> ReadLifecycleEventAsync(
        CancellationToken cancellationToken = default
    );

    public IDisposable RegisterLifecycleParticipant(
        ITerminalSessionLifecycleParticipant participant
    );

    public ValueTask<TerminalControlResult<TerminalPresentationLease>> AcquirePresentationAsync(
        TerminalPresentationOptions options,
        CancellationToken cancellationToken = default
    );

    public void InvalidateState();
    public ValueTask DisposeAsync();
}
```

---

## 11. Review boundary

This baseline deliberately does not expose:

- POSIX P/Invoke structures/constants;
- Windows console P/Invoke structures/constants;
- OS-specific terminal-control providers;
- the incremental input decoder implementation;
- lifecycle signal sources/controllers;
- stream byte-service adapters;
- the presentation manager;
- a process-global current `TerminalSession`;
- PTY/ConPTY creation or child-process hosting.

The corresponding behavioral review is recorded in [`T12B-Public-API-and-Consumer-Contract.md`](T12B-Public-API-and-Consumer-Contract.md).
