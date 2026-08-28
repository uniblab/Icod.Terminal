namespace Icod.Terminal;

using System.Collections.ObjectModel;

/// <summary>
/// Reports the current device-status condition returned by an ECMA-48 DSR request.
/// </summary>
public enum TerminalDeviceStatus {
	/// <summary>The terminal reports that it is ready with no malfunction detected.</summary>
	Ready = 0,

	/// <summary>The terminal is busy and the caller should request status again later.</summary>
	BusyRequestAgain = 1,

	/// <summary>The terminal is busy and reports that another status report will follow.</summary>
	BusyReportFollows = 2,

	/// <summary>The terminal reports a malfunction and the caller should request status again later.</summary>
	MalfunctionRequestAgain = 3,

	/// <summary>The terminal reports a malfunction and reports that another status report will follow.</summary>
	MalfunctionReportFollows = 4
}

/// <summary>
/// Represents one Primary Device Attributes response.
/// </summary>
public sealed class TerminalPrimaryDeviceAttributes {
	private readonly ReadOnlyCollection<int> attributes;

	internal TerminalPrimaryDeviceAttributes(
		int deviceCode,
		IEnumerable<int> attributes
	) {
		if ( 0 > deviceCode ) {
			throw new ArgumentOutOfRangeException( nameof( deviceCode ) );
		}
		ArgumentNullException.ThrowIfNull( attributes );

		int[] values = attributes.ToArray();
		if ( values.Any( static value => 0 > value ) ) {
			throw new ArgumentException(
				"Primary device-attribute values cannot be negative.",
				nameof( attributes )
			);
		}

		this.DeviceCode = deviceCode;
		this.attributes = Array.AsReadOnly( values );
	}

	/// <summary>
	/// Gets the first Primary Device Attributes parameter, identifying the reported
	/// DEC/VT device class or compatible terminal family code.
	/// </summary>
	public int DeviceCode {
		get;
	}

	/// <summary>
	/// Gets the remaining Primary Device Attributes parameters in wire order.
	/// </summary>
	public IReadOnlyList<int> Attributes {
		get {
			return this.attributes;
		}
	}

	/// <summary>
	/// Determines whether the terminal reported a particular Primary Device Attributes value.
	/// </summary>
	/// <param name="attribute">The nonnegative attribute code.</param>
	/// <returns><see langword="true"/> when the response contained the code.</returns>
	public bool HasAttribute(
		int attribute
	) {
		if ( 0 > attribute ) {
			throw new ArgumentOutOfRangeException( nameof( attribute ) );
		}

		return this.attributes.Contains( attribute );
	}
}

/// <summary>
/// Represents one Secondary Device Attributes response.
/// </summary>
public sealed class TerminalSecondaryDeviceAttributes {
	internal TerminalSecondaryDeviceAttributes(
		int terminalTypeCode,
		int firmwareVersion,
		int optionCode
	) {
		if ( 0 > terminalTypeCode ) {
			throw new ArgumentOutOfRangeException( nameof( terminalTypeCode ) );
		}
		if ( 0 > firmwareVersion ) {
			throw new ArgumentOutOfRangeException( nameof( firmwareVersion ) );
		}
		if ( 0 > optionCode ) {
			throw new ArgumentOutOfRangeException( nameof( optionCode ) );
		}

		this.TerminalTypeCode = terminalTypeCode;
		this.FirmwareVersion = firmwareVersion;
		this.OptionCode = optionCode;
	}

	/// <summary>
	/// Gets the terminal-type code from the first Secondary Device Attributes parameter.
	/// </summary>
	public int TerminalTypeCode {
		get;
	}

	/// <summary>
	/// Gets the firmware/version parameter reported by the terminal.
	/// </summary>
	public int FirmwareVersion {
		get;
	}

	/// <summary>
	/// Gets the terminal-specific option or ROM-registration parameter.
	/// </summary>
	public int OptionCode {
		get;
	}
}

/// <summary>
/// Represents a cursor position reported by the terminal.
/// </summary>
public sealed class TerminalCursorPosition {
	internal TerminalCursorPosition(
		int row,
		int column
	) {
		if ( 0 >= row ) {
			throw new ArgumentOutOfRangeException( nameof( row ) );
		}
		if ( 0 >= column ) {
			throw new ArgumentOutOfRangeException( nameof( column ) );
		}

		this.Row = row;
		this.Column = column;
	}

	/// <summary>
	/// Gets the one-based terminal row reported by CPR.
	/// </summary>
	public int Row {
		get;
	}

	/// <summary>
	/// Gets the one-based terminal column reported by CPR.
	/// </summary>
	public int Column {
		get;
	}
}
