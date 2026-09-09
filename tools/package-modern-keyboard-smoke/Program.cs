/*
	Icod.Terminal.PackageModernKeyboardSmoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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
using System.Reflection;
using Icod.Terminal;

TerminalInputProtocolOptions options = new() {
	KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
};
if ( TerminalKeyboardReportingMode.AllKeys != options.KeyboardReportingMode ) {
	throw new InvalidOperationException(
		"The shipped package did not preserve the requested keyboard reporting mode."
	);
}

if ( 0 != (int)TerminalKeyEventPhase.Press
	|| 1 != (int)TerminalKeyEventPhase.Repeat
	|| 2 != (int)TerminalKeyEventPhase.Release ) {
	throw new InvalidOperationException(
		"TerminalKeyEventPhase numeric values changed unexpectedly."
	);
}

if ( 0 != (int)TerminalKeyboardReportingMode.Disambiguated
	|| 1 != (int)TerminalKeyboardReportingMode.EventTypes
	|| 2 != (int)TerminalKeyboardReportingMode.AllKeys ) {
	throw new InvalidOperationException(
		"TerminalKeyboardReportingMode numeric values changed unexpectedly."
	);
}

TerminalKeyModifiers modernModifiers =
	TerminalKeyModifiers.Super
	| TerminalKeyModifiers.Hyper
	| TerminalKeyModifiers.Meta
	| TerminalKeyModifiers.CapsLock
	| TerminalKeyModifiers.NumLock;
if ( TerminalKeyModifiers.None == modernModifiers ) {
	throw new InvalidOperationException(
		"The shipped package is missing modern keyboard modifier flags."
	);
}

PropertyInfo[] inputProperties = typeof( TerminalInputEvent ).GetProperties(
	BindingFlags.Public | BindingFlags.Instance
);
RequireProperty( inputProperties, nameof( TerminalInputEvent.KeyPhase ) );
RequireProperty( inputProperties, nameof( TerminalInputEvent.ShiftedCharacter ) );
RequireProperty( inputProperties, nameof( TerminalInputEvent.BaseLayoutCharacter ) );
RequireProperty( inputProperties, nameof( TerminalInputEvent.AssociatedText ) );

PropertyInfo[] optionProperties = typeof( TerminalInputProtocolOptions ).GetProperties(
	BindingFlags.Public | BindingFlags.Instance
);
RequireProperty(
	optionProperties,
	nameof( TerminalInputProtocolOptions.KeyboardReportingMode )
);

PropertyInfo[] leaseProperties = typeof( TerminalInputProtocolLease ).GetProperties(
	BindingFlags.Public | BindingFlags.Instance
);
RequireProperty(
	leaseProperties,
	nameof( TerminalInputProtocolLease.KeyboardReportingMode )
);

// Kitty is a vendor namespace shared by multiple protocol families.  The
// keyboard contract forbids raw Kitty keyboard controls, not every future
// semantic API whose name truthfully identifies another Kitty protocol.
string[] forbiddenRawKeyboardMethods = typeof( TerminalSession )
	.GetMethods( BindingFlags.Public | BindingFlags.Instance )
	.Where(
		method => (
			method.Name.Contains(
				"Kitty",
				StringComparison.OrdinalIgnoreCase
			)
			&& method.Name.Contains(
				"Keyboard",
				StringComparison.OrdinalIgnoreCase
			)
		)
		|| method.Name.Contains(
			"ModifyOtherKeys",
			StringComparison.OrdinalIgnoreCase
		)
		|| method.Name.Contains(
			"RawKeyboard",
			StringComparison.OrdinalIgnoreCase
		)
	)
	.Select( method => method.Name )
	.Distinct( StringComparer.Ordinal )
	.OrderBy( name => name, StringComparer.Ordinal )
	.ToArray();
if ( 0 != forbiddenRawKeyboardMethods.Length ) {
	throw new InvalidOperationException(
		"The shipped package exposes raw/vendor keyboard control methods: "
			+ string.Join( ", ", forbiddenRawKeyboardMethods )
	);
}

if ( !Enum.IsDefined( TerminalKey.Unrecognized ) ) {
	throw new InvalidOperationException(
		"The shipped package is missing TerminalKey.Unrecognized."
	);
}

Console.WriteLine(
	"Icod.Terminal 0.17 modern-keyboard package API and exclusion smoke passed."
);

static void RequireProperty(
	IEnumerable<PropertyInfo> properties,
	string name
) {
	ArgumentNullException.ThrowIfNull( properties );
	ArgumentException.ThrowIfNullOrEmpty( name );
	if ( !properties.Any(
		property => string.Equals(
			property.Name,
			name,
			StringComparison.Ordinal
		)
	) ) {
		throw new InvalidOperationException(
			$"The shipped package is missing required public property '{name}'."
		);
	}
}
