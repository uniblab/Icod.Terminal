namespace Icod.Terminal;

using System.Collections.ObjectModel;

/// <summary>
/// Represents one live terminal-capability observation returned by XTGETTCAP.
/// </summary>
public sealed class TerminalCapabilityObservation {
	private readonly ReadOnlyCollection<byte>? valueBytes;

	internal TerminalCapabilityObservation(
		string name,
		bool isSupported,
		byte[]? valueBytes
	) {
		ArgumentNullException.ThrowIfNull( name );
		if ( 0 == name.Length ) {
			throw new ArgumentException(
				"A terminal capability name cannot be empty.",
				nameof( name )
			);
		}
		if ( isSupported && valueBytes is null ) {
			throw new ArgumentNullException(
				nameof( valueBytes ),
				"A supported terminal capability observation must contain a byte value."
			);
		}
		if ( !isSupported && valueBytes is not null ) {
			throw new ArgumentException(
				"An unsupported terminal capability observation cannot contain a byte value.",
				nameof( valueBytes )
			);
		}

		this.Name = name;
		this.IsSupported = isSupported;
		this.valueBytes = valueBytes is null
			? null
			: Array.AsReadOnly( valueBytes.ToArray() )
		;
	}

	/// <summary>
	/// Gets the capability name which was queried.
	/// </summary>
	public string Name {
		get;
	}

	/// <summary>
	/// Gets whether the terminal reported the capability as available.
	/// </summary>
	public bool IsSupported {
		get;
	}

	/// <summary>
	/// Gets the exact decoded capability value bytes for a supported observation,
	/// or <see langword="null"/> when the terminal reports the capability as
	/// unsupported.
	/// </summary>
	/// <remarks>
	/// XTGETTCAP values are terminal byte strings and may contain control bytes;
	/// they are intentionally not decoded as Unicode text by this API.
	/// </remarks>
	public IReadOnlyList<byte>? ValueBytes {
		get {
			return this.valueBytes;
		}
	}
}
