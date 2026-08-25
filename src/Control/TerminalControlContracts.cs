namespace Icod.Terminal;

using System.Collections.ObjectModel;
using Icod.TermInfo;

/// <summary>
/// Identifies the availability of one terminal-control operation.
/// </summary>
public enum TerminalControlStatus {
	/// <summary>The requested operation completed and returned a value.</summary>
	Available,

	/// <summary>The host supports the operation, but the requested endpoint does not expose it.</summary>
	Unavailable,

	/// <summary>The current platform does not implement the requested capability.</summary>
	Unsupported,

	/// <summary>The host operation failed in a controlled manner.</summary>
	Failed
}

/// <summary>
/// Identifies the native terminal model represented by a snapshot.
/// </summary>
public enum TerminalPlatformKind {
	/// <summary>A POSIX <c>termios</c> terminal.</summary>
	PosixTermios,

	/// <summary>A Windows console input or output handle.</summary>
	WindowsConsole
}

/// <summary>
/// Identifies the kind of endpoint supplied to a terminal-control provider.
/// </summary>
public enum TerminalEndpointKind {
	/// <summary>An existing process file descriptor.</summary>
	FileDescriptor,

	/// <summary>A terminal device or console-device pathname.</summary>
	Path
}

/// <summary>
/// Identifies how terminal mode changes are applied.
/// </summary>
public enum TerminalModeApplyTiming {
	/// <summary>Apply the settings immediately.</summary>
	Immediately,

	/// <summary>Wait for pending output before applying the settings.</summary>
	AfterOutputDrained,

	/// <summary>Wait for output, discard unread input, and then apply the settings.</summary>
	AfterOutputDrainedAndInputDiscarded
}

/// <summary>
/// Identifies the direction of a Windows console mode.
/// </summary>
public enum TerminalConsoleDirection {
	/// <summary>The mode belongs to a console input handle.</summary>
	Input,

	/// <summary>The mode belongs to a console output handle.</summary>
	Output
}

/// <summary>
/// Describes terminal-control capabilities exposed for one endpoint.
/// </summary>
[Flags]
public enum TerminalControlCapabilities {
	/// <summary>No terminal-control capability is available.</summary>
	None = 0,

	/// <summary>Terminal attachment can be inspected.</summary>
	Attachment = 1 << 0,

	/// <summary>A terminal pathname or stable console alias can be reported.</summary>
	Pathname = 1 << 1,

	/// <summary>The terminal mode can be retrieved.</summary>
	ModeRead = 1 << 2,

	/// <summary>The terminal mode can be changed.</summary>
	ModeWrite = 1 << 3,

	/// <summary>Input and output speeds can be reported.</summary>
	Speeds = 1 << 4,

	/// <summary>POSIX terminal control characters can be reported.</summary>
	ControlCharacters = 1 << 5,

	/// <summary>The mode can be serialized and restored.</summary>
	MachineSerialization = 1 << 6,

	/// <summary>Live terminal dimensions can be queried for this endpoint.</summary>
	LiveSize = 1 << 7
}

/// <summary>
/// Identifies a file descriptor or named terminal device without transferring
/// ownership of an existing descriptor to the provider.
/// </summary>
public sealed class TerminalEndpoint {
	private TerminalEndpoint(
		TerminalEndpointKind kind,
		int? fileDescriptor,
		string? path
	) {
		this.Kind = kind;
		this.FileDescriptor = fileDescriptor;
		this.Path = path;
	}

	/// <summary>Gets the standard-input endpoint.</summary>
	public static TerminalEndpoint StandardInput {
		get;
	} = ForFileDescriptor( 0 );

	/// <summary>Gets the standard-output endpoint.</summary>
	public static TerminalEndpoint StandardOutput {
		get;
	} = ForFileDescriptor( 1 );

	/// <summary>Gets the standard-error endpoint.</summary>
	public static TerminalEndpoint StandardError {
		get;
	} = ForFileDescriptor( 2 );

	/// <summary>Gets the endpoint kind.</summary>
	public TerminalEndpointKind Kind {
		get;
	}

	/// <summary>Gets the file descriptor for a descriptor endpoint.</summary>
	public int? FileDescriptor {
		get;
	}

	/// <summary>Gets the device pathname for a path endpoint.</summary>
	public string? Path {
		get;
	}

	/// <summary>Gets a diagnostic display name for the endpoint.</summary>
	public string DisplayName {
		get {
			return TerminalEndpointKind.FileDescriptor == this.Kind
				? string.Concat(
					"file descriptor ",
					this.FileDescriptor!.Value.ToString(
						System.Globalization.CultureInfo.InvariantCulture
					)
				)
				: this.Path!;
		}
	}

	/// <summary>Creates an endpoint for an existing process file descriptor.</summary>
	/// <param name="fileDescriptor">A nonnegative file descriptor.</param>
	/// <returns>The endpoint.</returns>
	public static TerminalEndpoint ForFileDescriptor(
		int fileDescriptor
	) {
		if ( 0 > fileDescriptor ) {
			throw new ArgumentOutOfRangeException(
				nameof( fileDescriptor ),
				"A terminal file descriptor cannot be negative."
			);
		}

		return new TerminalEndpoint(
			TerminalEndpointKind.FileDescriptor,
			fileDescriptor,
			null
		);
	}

	/// <summary>Creates an endpoint for a named terminal or console device.</summary>
	/// <param name="path">The nonempty device pathname.</param>
	/// <returns>The endpoint.</returns>
	public static TerminalEndpoint ForPath(
		string path
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );

		return new TerminalEndpoint(
			TerminalEndpointKind.Path,
			null,
			path
		);
	}
}

/// <summary>
/// Represents a controlled result from a terminal query.
/// </summary>
/// <typeparam name="T">The available value type.</typeparam>
public sealed class TerminalControlResult<T> {
	private TerminalControlResult(
		TerminalControlStatus status,
		T? value,
		string? message,
		int? nativeErrorCode
	) {
		this.Status = status;
		this.Value = value;
		this.Message = message;
		this.NativeErrorCode = nativeErrorCode;
	}

	/// <summary>Gets the result status.</summary>
	public TerminalControlStatus Status {
		get;
	}

	/// <summary>Gets the available value, or the default value when unavailable.</summary>
	public T? Value {
		get;
	}

	/// <summary>Gets a controlled diagnostic explanation, when present.</summary>
	public string? Message {
		get;
	}

	/// <summary>Gets the native error code, when one was reported.</summary>
	public int? NativeErrorCode {
		get;
	}

	/// <summary>Gets whether the result contains an available value.</summary>
	public bool IsAvailable {
		get {
			return TerminalControlStatus.Available == this.Status;
		}
	}

	/// <summary>Returns the available value or throws when the result is not available.</summary>
	/// <returns>The available value.</returns>
	public T GetRequiredValue() {
		if ( !this.IsAvailable ) {
			throw new InvalidOperationException(
				this.Message ?? "The terminal-control value is not available."
			);
		}

		return this.Value!;
	}

	/// <summary>Creates an available result.</summary>
	/// <param name="value">The available value.</param>
	/// <returns>The result.</returns>
	public static TerminalControlResult<T> Available(
		T value
	) {
		ArgumentNullException.ThrowIfNull( value );

		return new TerminalControlResult<T>(
			TerminalControlStatus.Available,
			value,
			null,
			null
		);
	}

	/// <summary>Creates an unavailable result.</summary>
	/// <param name="message">The controlled explanation.</param>
	/// <param name="nativeErrorCode">The optional native error code.</param>
	/// <returns>The result.</returns>
	public static TerminalControlResult<T> Unavailable(
		string? message,
		int? nativeErrorCode = null
	) {
		return CreateWithoutValue(
			TerminalControlStatus.Unavailable,
			message,
			"The terminal capability is unavailable.",
			nativeErrorCode
		);
	}

	/// <summary>Creates an unsupported result.</summary>
	/// <param name="message">The controlled explanation.</param>
	/// <returns>The result.</returns>
	public static TerminalControlResult<T> Unsupported(
		string? message
	) {
		return CreateWithoutValue(
			TerminalControlStatus.Unsupported,
			message,
			"The terminal capability is unsupported on this platform.",
			null
		);
	}

	/// <summary>Creates a failed result.</summary>
	/// <param name="message">The controlled failure explanation.</param>
	/// <param name="nativeErrorCode">The optional native error code.</param>
	/// <returns>The result.</returns>
	public static TerminalControlResult<T> Failed(
		string? message,
		int? nativeErrorCode = null
	) {
		return CreateWithoutValue(
			TerminalControlStatus.Failed,
			message,
			"The terminal operation failed.",
			nativeErrorCode
		);
	}

	private static TerminalControlResult<T> CreateWithoutValue(
		TerminalControlStatus status,
		string? message,
		string fallback,
		int? nativeErrorCode
	) {
		return new TerminalControlResult<T>(
			status,
			default,
			string.IsNullOrWhiteSpace( message ) ? fallback : message.Trim(),
			nativeErrorCode
		);
	}
}

/// <summary>
/// Represents the outcome of changing terminal state.
/// </summary>
public sealed class TerminalControlMutationResult {
	private TerminalControlMutationResult(
		TerminalControlStatus status,
		string? message,
		int? nativeErrorCode
	) {
		this.Status = status;
		this.Message = message;
		this.NativeErrorCode = nativeErrorCode;
	}

	/// <summary>Gets the mutation status.</summary>
	public TerminalControlStatus Status {
		get;
	}

	/// <summary>Gets the controlled explanation, when present.</summary>
	public string? Message {
		get;
	}

	/// <summary>Gets the native error code, when one was reported.</summary>
	public int? NativeErrorCode {
		get;
	}

	/// <summary>Gets whether the mutation succeeded.</summary>
	public bool Succeeded {
		get {
			return TerminalControlStatus.Available == this.Status;
		}
	}

	/// <summary>Creates a successful mutation result.</summary>
	/// <returns>The result.</returns>
	public static TerminalControlMutationResult Success() {
		return new TerminalControlMutationResult(
			TerminalControlStatus.Available,
			null,
			null
		);
	}

	/// <summary>Creates an unavailable mutation result.</summary>
	/// <param name="message">The controlled explanation.</param>
	/// <param name="nativeErrorCode">The optional native error code.</param>
	/// <returns>The result.</returns>
	public static TerminalControlMutationResult Unavailable(
		string? message,
		int? nativeErrorCode = null
	) {
		return Create(
			TerminalControlStatus.Unavailable,
			message,
			"The terminal mutation is unavailable.",
			nativeErrorCode
		);
	}

	/// <summary>Creates an unsupported mutation result.</summary>
	/// <param name="message">The controlled explanation.</param>
	/// <returns>The result.</returns>
	public static TerminalControlMutationResult Unsupported(
		string? message
	) {
		return Create(
			TerminalControlStatus.Unsupported,
			message,
			"The terminal mutation is unsupported on this platform.",
			null
		);
	}

	/// <summary>Creates a failed mutation result.</summary>
	/// <param name="message">The controlled failure explanation.</param>
	/// <param name="nativeErrorCode">The optional native error code.</param>
	/// <returns>The result.</returns>
	public static TerminalControlMutationResult Failed(
		string? message,
		int? nativeErrorCode = null
	) {
		return Create(
			TerminalControlStatus.Failed,
			message,
			"The terminal mutation failed.",
			nativeErrorCode
		);
	}

	private static TerminalControlMutationResult Create(
		TerminalControlStatus status,
		string? message,
		string fallback,
		int? nativeErrorCode
	) {
		return new TerminalControlMutationResult(
			status,
			string.IsNullOrWhiteSpace( message ) ? fallback : message.Trim(),
			nativeErrorCode
		);
	}
}

/// <summary>
/// Describes attachment, pathname, platform, and capabilities for one endpoint.
/// </summary>
public sealed class TerminalEndpointObservation {
	/// <summary>
	/// Initializes an endpoint observation.
	/// </summary>
	/// <param name="isTerminal">Whether the endpoint is attached to a terminal.</param>
	/// <param name="pathname">The terminal pathname or stable console alias, when available.</param>
	/// <param name="platform">The native terminal model, when attached.</param>
	/// <param name="capabilities">The endpoint capabilities.</param>
	public TerminalEndpointObservation(
		bool isTerminal,
		string? pathname,
		TerminalPlatformKind? platform,
		TerminalControlCapabilities capabilities
	) {
		if ( !isTerminal && ( platform is not null ) ) {
			throw new ArgumentException(
				"A nonterminal endpoint cannot report a terminal platform.",
				nameof( platform )
			);
		}
		if ( !isTerminal && TerminalControlCapabilities.None != capabilities ) {
			throw new ArgumentException(
				"A nonterminal endpoint cannot report terminal capabilities.",
				nameof( capabilities )
			);
		}
		if ( isTerminal && ( platform is null ) ) {
			throw new ArgumentException(
				"A terminal endpoint must identify its native terminal platform.",
				nameof( platform )
			);
		}
		if ( isTerminal && !capabilities.HasFlag( TerminalControlCapabilities.Attachment ) ) {
			throw new ArgumentException(
				"A terminal endpoint must report the attachment capability.",
				nameof( capabilities )
			);
		}
		if ( platform.HasValue && !Enum.IsDefined( platform.Value ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( platform ),
				platform,
				"The terminal platform is not recognized."
			);
		}

		const TerminalControlCapabilities knownCapabilities =
			TerminalControlCapabilities.Attachment
			| TerminalControlCapabilities.Pathname
			| TerminalControlCapabilities.ModeRead
			| TerminalControlCapabilities.ModeWrite
			| TerminalControlCapabilities.Speeds
			| TerminalControlCapabilities.ControlCharacters
			| TerminalControlCapabilities.MachineSerialization
			| TerminalControlCapabilities.LiveSize;

		if ( TerminalControlCapabilities.None != ( capabilities & ~knownCapabilities ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( capabilities ),
				capabilities,
				"The terminal capability set contains an unrecognized bit."
			);
		}

		string? normalizedPathname = string.IsNullOrWhiteSpace( pathname )
			? null
			: pathname;

		if ( capabilities.HasFlag( TerminalControlCapabilities.Pathname )
			!= ( normalizedPathname is not null ) ) {
			throw new ArgumentException(
				"The pathname capability and pathname value must be reported together.",
				nameof( capabilities )
			);
		}

		this.IsTerminal = isTerminal;
		this.Pathname = normalizedPathname;
		this.Platform = platform;
		this.Capabilities = capabilities;
	}

	/// <summary>Gets whether the endpoint is attached to a terminal.</summary>
	public bool IsTerminal {
		get;
	}

	/// <summary>Gets the terminal pathname or console alias, when available.</summary>
	public string? Pathname {
		get;
	}

	/// <summary>Gets the native terminal model, when attached.</summary>
	public TerminalPlatformKind? Platform {
		get;
	}

	/// <summary>Gets the endpoint capabilities.</summary>
	public TerminalControlCapabilities Capabilities {
		get;
	}
}

/// <summary>
/// Represents one native speed code and its reported baud rate.
/// </summary>
public readonly record struct TerminalSpeed {
	/// <summary>
	/// Initializes a terminal speed observation.
	/// </summary>
	/// <param name="nativeCode">The native <c>speed_t</c> code.</param>
	/// <param name="baudRate">The baud rate, or <see langword="null"/> when the code is not recognized.</param>
	public TerminalSpeed(
		ulong nativeCode,
		ulong? baudRate
	) {
		this.NativeCode = nativeCode;
		this.BaudRate = baudRate;
	}

	/// <summary>Gets the native speed code.</summary>
	public ulong NativeCode {
		get;
	}

	/// <summary>Gets the reported baud rate, when recognized.</summary>
	public ulong? BaudRate {
		get;
	}
}

/// <summary>
/// Represents a complete POSIX terminal mode or Windows console mode.
/// </summary>
public sealed class TerminalModeSnapshot {
	private readonly ReadOnlyCollection<byte> controlCharacters;
	private readonly byte[]? nativeImage;

	private TerminalModeSnapshot(
		TerminalPlatformKind platform,
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags,
		IEnumerable<byte> controlCharacters,
		byte disabledControlCharacter,
		int nativeFlagWidth,
		byte? lineDiscipline,
		TerminalSpeed? inputSpeed,
		TerminalSpeed? outputSpeed,
		TerminalConsoleDirection? consoleDirection,
		uint? consoleMode,
		byte[]? nativeImage
	) {
		ArgumentNullException.ThrowIfNull( controlCharacters );

		byte[] characters = controlCharacters.ToArray();

		switch ( platform ) {
			case TerminalPlatformKind.PosixTermios:
				ValidatePosix(
					inputFlags,
					outputFlags,
					controlFlags,
					localFlags,
					characters,
					nativeFlagWidth,
					inputSpeed,
					outputSpeed,
					consoleDirection,
					consoleMode
				);
				break;

			case TerminalPlatformKind.WindowsConsole:
				ValidateWindows(
					inputFlags,
					outputFlags,
					controlFlags,
					localFlags,
					characters,
					nativeFlagWidth,
					lineDiscipline,
					inputSpeed,
					outputSpeed,
					consoleDirection,
					consoleMode,
					nativeImage
				);
				break;

			default:
				throw new ArgumentOutOfRangeException(
					nameof( platform ),
					platform,
					"The terminal platform is not recognized."
				);
		}

		if ( ( nativeImage is not null ) && ( 0 == nativeImage.Length ) ) {
			throw new ArgumentException(
				"A captured native terminal image cannot be empty.",
				nameof( nativeImage )
			);
		}

		this.Platform = platform;
		this.InputFlags = inputFlags;
		this.OutputFlags = outputFlags;
		this.ControlFlags = controlFlags;
		this.LocalFlags = localFlags;
		this.controlCharacters = Array.AsReadOnly( characters );
		this.DisabledControlCharacter = disabledControlCharacter;
		this.NativeFlagWidth = nativeFlagWidth;
		this.LineDiscipline = lineDiscipline;
		this.InputSpeed = inputSpeed;
		this.OutputSpeed = outputSpeed;
		this.ConsoleDirection = consoleDirection;
		this.ConsoleMode = consoleMode;
		this.nativeImage = nativeImage?.ToArray();
	}

	/// <summary>Gets the native terminal model.</summary>
	public TerminalPlatformKind Platform {
		get;
	}

	/// <summary>Gets the native POSIX input flags.</summary>
	public ulong InputFlags {
		get;
	}

	/// <summary>Gets the native POSIX output flags.</summary>
	public ulong OutputFlags {
		get;
	}

	/// <summary>Gets the native POSIX control flags.</summary>
	public ulong ControlFlags {
		get;
	}

	/// <summary>Gets the native POSIX local flags.</summary>
	public ulong LocalFlags {
		get;
	}

	/// <summary>Gets the native POSIX control-character array.</summary>
	public IReadOnlyList<byte> ControlCharacters {
		get {
			return this.controlCharacters;
		}
	}

	/// <summary>Gets the host value used to disable a POSIX control character, or zero on Windows.</summary>
	public byte DisabledControlCharacter {
		get;
	}

	/// <summary>Gets the native POSIX terminal-flag width in bits, or zero on Windows.</summary>
	public int NativeFlagWidth {
		get;
	}

	/// <summary>Gets the POSIX line discipline when represented by the host ABI.</summary>
	public byte? LineDiscipline {
		get;
	}

	/// <summary>Gets the POSIX input speed, or <see langword="null"/> on Windows.</summary>
	public TerminalSpeed? InputSpeed {
		get;
	}

	/// <summary>Gets the POSIX output speed, or <see langword="null"/> on Windows.</summary>
	public TerminalSpeed? OutputSpeed {
		get;
	}

	/// <summary>Gets the Windows console direction, or <see langword="null"/> on POSIX.</summary>
	public TerminalConsoleDirection? ConsoleDirection {
		get;
	}

	/// <summary>Gets the native Windows console mode, or <see langword="null"/> on POSIX.</summary>
	public uint? ConsoleMode {
		get;
	}

	/// <summary>Creates a POSIX terminal-mode snapshot.</summary>
	/// <param name="inputFlags">The native input flags.</param>
	/// <param name="outputFlags">The native output flags.</param>
	/// <param name="controlFlags">The native control flags.</param>
	/// <param name="localFlags">The native local flags.</param>
	/// <param name="controlCharacters">The complete native control-character array.</param>
	/// <param name="disabledControlCharacter">The host disabled-character value.</param>
	/// <param name="nativeFlagWidth">The native flag width, 32 or 64 bits.</param>
	/// <param name="lineDiscipline">The optional line discipline.</param>
	/// <param name="inputSpeed">The input speed.</param>
	/// <param name="outputSpeed">The output speed.</param>
	/// <returns>The snapshot.</returns>
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
	) {
		ArgumentNullException.ThrowIfNull( controlCharacters );

		return new TerminalModeSnapshot(
			TerminalPlatformKind.PosixTermios,
			inputFlags,
			outputFlags,
			controlFlags,
			localFlags,
			controlCharacters,
			disabledControlCharacter,
			nativeFlagWidth,
			lineDiscipline,
			inputSpeed,
			outputSpeed,
			null,
			null,
			null
		);
	}

	/// <summary>Creates a Windows console-mode snapshot.</summary>
	/// <param name="direction">The console-handle direction.</param>
	/// <param name="mode">The native console mode.</param>
	/// <returns>The snapshot.</returns>
	public static TerminalModeSnapshot CreateWindowsConsole(
		TerminalConsoleDirection direction,
		uint mode
	) {
		return new TerminalModeSnapshot(
			TerminalPlatformKind.WindowsConsole,
			0,
			0,
			0,
			0,
			Array.Empty<byte>(),
			0,
			0,
			null,
			null,
			null,
			direction,
			mode,
			null
		);
	}

	/// <summary>
	/// Creates a POSIX snapshot by replacing serialized flags and control
	/// characters while preserving speeds and host ABI details from this snapshot.
	/// </summary>
	/// <param name="inputFlags">The restored input flags.</param>
	/// <param name="outputFlags">The restored output flags.</param>
	/// <param name="controlFlags">The restored control flags.</param>
	/// <param name="localFlags">The restored local flags.</param>
	/// <param name="controlCharacters">The restored control characters.</param>
	/// <returns>The restored snapshot.</returns>
	public TerminalModeSnapshot WithPosixSerializedState(
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags,
		IEnumerable<byte> controlCharacters
	) {
		ArgumentNullException.ThrowIfNull( controlCharacters );

		if ( TerminalPlatformKind.PosixTermios != this.Platform ) {
			throw new InvalidOperationException(
				"Only a POSIX terminal snapshot can receive POSIX serialized state."
			);
		}

		return new TerminalModeSnapshot(
			TerminalPlatformKind.PosixTermios,
			inputFlags,
			outputFlags,
			controlFlags,
			localFlags,
			controlCharacters,
			this.DisabledControlCharacter,
			this.NativeFlagWidth,
			this.LineDiscipline,
			this.InputSpeed!.Value,
			this.OutputSpeed!.Value,
			null,
			null,
			this.nativeImage
		);
	}

	internal static TerminalModeSnapshot CreateCapturedPosix(
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags,
		IEnumerable<byte> controlCharacters,
		byte disabledControlCharacter,
		int nativeFlagWidth,
		byte? lineDiscipline,
		TerminalSpeed inputSpeed,
		TerminalSpeed outputSpeed,
		byte[] nativeImage
	) {
		ArgumentNullException.ThrowIfNull( controlCharacters );
		ArgumentNullException.ThrowIfNull( nativeImage );

		return new TerminalModeSnapshot(
			TerminalPlatformKind.PosixTermios,
			inputFlags,
			outputFlags,
			controlFlags,
			localFlags,
			controlCharacters,
			disabledControlCharacter,
			nativeFlagWidth,
			lineDiscipline,
			inputSpeed,
			outputSpeed,
			null,
			null,
			nativeImage
		);
	}

	internal byte[] CreateNativeImage(
		int structureSize
	) {
		if ( 0 >= structureSize ) {
			throw new ArgumentOutOfRangeException(
				nameof( structureSize ),
				"Native terminal structure size must be positive."
			);
		}

		if ( ( this.nativeImage is not null )
			&& ( structureSize == this.nativeImage.Length ) ) {
			return this.nativeImage.ToArray();
		}

		return new byte[ structureSize ];
	}

	private static void ValidatePosix(
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags,
		byte[] characters,
		int nativeFlagWidth,
		TerminalSpeed? inputSpeed,
		TerminalSpeed? outputSpeed,
		TerminalConsoleDirection? consoleDirection,
		uint? consoleMode
	) {
		if ( 0 == characters.Length ) {
			throw new ArgumentException(
				"A POSIX terminal mode must contain its native control-character array.",
				nameof( characters )
			);
		}
		if ( ( 32 != nativeFlagWidth ) && ( 64 != nativeFlagWidth ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( nativeFlagWidth ),
				"A POSIX terminal flag width must be 32 or 64 bits."
			);
		}
		if ( ( 32 == nativeFlagWidth )
			&& ( ( uint.MaxValue < inputFlags )
				|| ( uint.MaxValue < outputFlags )
				|| ( uint.MaxValue < controlFlags )
				|| ( uint.MaxValue < localFlags ) ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( inputFlags ),
				"A POSIX terminal flag exceeds the declared 32-bit native width."
			);
		}
		if ( ( consoleDirection is not null ) || ( consoleMode is not null ) ) {
			throw new ArgumentException(
				"A POSIX terminal mode cannot contain Windows console fields.",
				nameof( consoleMode )
			);
		}
		if ( ( inputSpeed is null ) || ( outputSpeed is null ) ) {
			throw new ArgumentException(
				"A POSIX terminal mode requires native input and output speed values.",
				nameof( inputSpeed )
			);
		}
	}

	private static void ValidateWindows(
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags,
		byte[] characters,
		int nativeFlagWidth,
		byte? lineDiscipline,
		TerminalSpeed? inputSpeed,
		TerminalSpeed? outputSpeed,
		TerminalConsoleDirection? consoleDirection,
		uint? consoleMode,
		byte[]? nativeImage
	) {
		if ( consoleDirection.HasValue && !Enum.IsDefined( consoleDirection.Value ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( consoleDirection ),
				consoleDirection,
				"The console direction is not recognized."
			);
		}
		if ( 0 != characters.Length ) {
			throw new ArgumentException(
				"A Windows console mode cannot contain POSIX control characters.",
				nameof( characters )
			);
		}
		if ( ( consoleDirection is null ) || ( consoleMode is null ) ) {
			throw new ArgumentException(
				"A Windows console mode requires its direction and native mode value.",
				nameof( consoleMode )
			);
		}
		if ( ( 0 != inputFlags )
			|| ( 0 != outputFlags )
			|| ( 0 != controlFlags )
			|| ( 0 != localFlags )
			|| ( 0 != nativeFlagWidth )
			|| lineDiscipline.HasValue
			|| inputSpeed.HasValue
			|| outputSpeed.HasValue
			|| ( nativeImage is not null ) ) {
			throw new ArgumentException(
				"A Windows console mode cannot contain POSIX terminal fields.",
				nameof( inputFlags )
			);
		}
	}
}

/// <summary>
/// Supplies terminal attachment, identity, live size, mode, and mutation
/// operations for file descriptors and named devices.
/// </summary>
public interface ITerminalControlProvider {
	/// <summary>Observes terminal attachment and identity for an endpoint.</summary>
	/// <param name="endpoint">The endpoint.</param>
	/// <returns>The controlled observation result.</returns>
	TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	);

	/// <summary>Retrieves the current live size for an endpoint.</summary>
	/// <param name="endpoint">The endpoint.</param>
	/// <returns>The controlled terminal-size result.</returns>
	TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	);

	/// <summary>Retrieves the complete terminal mode for an endpoint.</summary>
	/// <param name="endpoint">The endpoint.</param>
	/// <returns>The controlled mode result.</returns>
	TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	);

	/// <summary>Applies a complete terminal mode to an endpoint.</summary>
	/// <param name="endpoint">The endpoint.</param>
	/// <param name="mode">The complete mode.</param>
	/// <param name="timing">The application timing.</param>
	/// <returns>The controlled mutation result.</returns>
	TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	);
}