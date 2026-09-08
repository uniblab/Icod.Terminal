/*
	Icod.Terminal.PublicApiSnapshot
	Public API snapshot generator for the Icod.Terminal library.
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
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Icod.Terminal;

if ( 1 != args.Length ) {
	throw new ArgumentException(
		"Expected exactly one output-file path."
	);
}

string outputPath = Path.GetFullPath( args[ 0 ] );
string? outputDirectory = Path.GetDirectoryName( outputPath );
if ( string.IsNullOrWhiteSpace( outputDirectory ) ) {
	throw new InvalidOperationException(
		"The public API snapshot output directory could not be resolved."
	);
}

Directory.CreateDirectory( outputDirectory );

Assembly assembly = typeof( TerminalSession ).Assembly;
var nullability = new NullabilityInfoContext();
var lines = new List<string> {
	"# Icod.Terminal public API snapshot v1"
};

foreach ( Type type in assembly.GetExportedTypes().OrderBy(
	static value => value.FullName,
	StringComparer.Ordinal
) ) {
	lines.Add( FormatTypeDeclaration( type ) );

	if ( type.IsEnum ) {
		lines.AddRange( FormatEnumValues( type ) );
		continue;
	}

	var members = new List<string>();
	const BindingFlags flags =
		BindingFlags.Public
		| BindingFlags.Instance
		| BindingFlags.Static
		| BindingFlags.DeclaredOnly;

	foreach ( ConstructorInfo constructor in type.GetConstructors( flags ) ) {
		members.Add(
			$"  C {FormatParameters( constructor.GetParameters(), nullability )}"
		);
	}

	foreach ( FieldInfo field in type.GetFields( flags ) ) {
		members.Add( FormatField( field, nullability ) );
	}

	foreach ( PropertyInfo property in type.GetProperties( flags ) ) {
		members.Add( FormatProperty( property, nullability ) );
	}

	foreach ( EventInfo eventInfo in type.GetEvents( flags ) ) {
		members.Add( FormatEvent( eventInfo, nullability ) );
	}

	foreach ( MethodInfo method in type.GetMethods( flags ) ) {
		if ( method.IsSpecialName
			&& !method.Name.StartsWith( "op_", StringComparison.Ordinal ) ) {
			continue;
		}

		members.Add( FormatMethod( method, nullability ) );
	}

	members.Sort( StringComparer.Ordinal );
	lines.AddRange( members );
}

File.WriteAllText(
	outputPath,
	string.Join( "\n", lines ) + "\n",
	new UTF8Encoding(
		encoderShouldEmitUTF8Identifier: false,
		throwOnInvalidBytes: true
	)
);

static string FormatTypeDeclaration(
	Type type
) {
	string kind;
	if ( type.IsInterface ) {
		kind = "interface";
	} else if ( type.IsEnum ) {
		kind = "enum";
	} else if ( typeof( MulticastDelegate ).IsAssignableFrom( type.BaseType ) ) {
		kind = "delegate";
	} else if ( type.IsValueType ) {
		kind = type.CustomAttributes.Any(
			static attribute =>
				"System.Runtime.CompilerServices.IsReadOnlyAttribute"
				== attribute.AttributeType.FullName
		)
			? "readonly struct"
			: "struct";
	} else if ( type.IsAbstract && type.IsSealed ) {
		kind = "static class";
	} else if ( type.IsAbstract ) {
		kind = "abstract class";
	} else if ( type.IsSealed ) {
		kind = "sealed class";
	} else {
		kind = "class";
	}

	var suffix = new List<string>();
	if ( type.CustomAttributes.Any(
		static attribute =>
			typeof( FlagsAttribute ).FullName == attribute.AttributeType.FullName
	) ) {
		suffix.Add( "flags" );
	}

	Type? baseType = type.BaseType;
	if ( baseType is not null
		&& typeof( object ) != baseType
		&& typeof( ValueType ) != baseType
		&& typeof( Enum ) != baseType
		&& typeof( MulticastDelegate ) != baseType ) {
		suffix.Add( $"base={FormatTypeName( baseType, null )}" );
	}

	string[] interfaces = type.GetInterfaces()
		.Where( static value => value.IsPublic || value.IsNestedPublic )
		.Select( static value => FormatTypeName( value, null ) )
		.Distinct( StringComparer.Ordinal )
		.OrderBy( static value => value, StringComparer.Ordinal )
		.ToArray();
	if ( 0 < interfaces.Length ) {
		suffix.Add( $"interfaces={string.Join( ",", interfaces )}" );
	}

	string constraints = FormatGenericConstraints( type.GetGenericArguments() );
	if ( 0 < constraints.Length ) {
		suffix.Add( constraints );
	}

	return 0 == suffix.Count
		? $"T {kind} {FormatTypeName( type, null )}"
		: $"T {kind} {FormatTypeName( type, null )} [{string.Join( ";", suffix )}]";
}

static IEnumerable<string> FormatEnumValues(
	Type type
) {
	Type underlying = Enum.GetUnderlyingType( type );
	yield return $"  U {FormatTypeName( underlying, null )}";

	foreach ( string name in Enum.GetNames( type ) ) {
		object value = Enum.Parse( type, name );
		object converted = Convert.ChangeType(
			value,
			underlying,
			CultureInfo.InvariantCulture
		);
		yield return $"  V {name}={Convert.ToString( converted, CultureInfo.InvariantCulture )}";
	}
}

static string FormatField(
	FieldInfo field,
	NullabilityInfoContext nullability
) {
	var modifiers = new List<string>();
	if ( field.IsStatic ) {
		modifiers.Add( "static" );
	}
	if ( field.IsLiteral ) {
		modifiers.Add( "const" );
	} else if ( field.IsInitOnly ) {
		modifiers.Add( "readonly" );
	}

	string value = field.IsLiteral
		? $"={FormatDefaultValue( field.GetRawConstantValue(), field.FieldType )}"
		: string.Empty;
	return $"  F {string.Join( " ", modifiers )} {FormatTypeName( field.FieldType, nullability.Create( field ) )} {field.Name}{value}".Replace(
		"F  ",
		"F ",
		StringComparison.Ordinal
	);
}

static string FormatProperty(
	PropertyInfo property,
	NullabilityInfoContext nullability
) {
	MethodInfo? getter = property.GetMethod;
	MethodInfo? setter = property.SetMethod;
	bool isStatic = true == getter?.IsStatic || true == setter?.IsStatic;
	string access = string.Concat(
		true == getter?.IsPublic ? "get;" : string.Empty,
		true == setter?.IsPublic
			? IsInitOnly( setter ) ? "init;" : "set;"
			: string.Empty
	);
	string index = FormatParameters( property.GetIndexParameters(), nullability );
	string name = 0 == property.GetIndexParameters().Length
		? property.Name
		: $"{property.Name}{index}";
	return $"  P {( isStatic ? "static " : string.Empty )}{FormatTypeName( property.PropertyType, nullability.Create( property ) )} {name} {{{access}}}";
}

static string FormatEvent(
	EventInfo eventInfo,
	NullabilityInfoContext nullability
) {
	MethodInfo? add = eventInfo.AddMethod;
	Type handler = eventInfo.EventHandlerType
		?? throw new InvalidOperationException(
			$"Public event '{eventInfo.Name}' has no handler type."
		);
	return $"  E {( true == add?.IsStatic ? "static " : string.Empty )}{FormatTypeName( handler, nullability.Create( eventInfo ) )} {eventInfo.Name}";
}

static string FormatMethod(
	MethodInfo method,
	NullabilityInfoContext nullability
) {
	var modifiers = new List<string>();
	if ( method.IsStatic ) {
		modifiers.Add( "static" );
	}
	if ( method.IsAbstract ) {
		modifiers.Add( "abstract" );
	} else if ( method.IsVirtual ) {
		modifiers.Add( method.IsFinal ? "virtual-final" : "virtual" );
	}
	if ( 0 != ( method.Attributes & MethodAttributes.NewSlot ) ) {
		modifiers.Add( "newslot" );
	}

	string genericArguments = method.IsGenericMethodDefinition
		? $"<{string.Join( ",", method.GetGenericArguments().Select( static value => value.Name ) )}>"
		: string.Empty;
	string constraints = FormatGenericConstraints( method.GetGenericArguments() );
	string constraintSuffix = 0 == constraints.Length
		? string.Empty
		: $" [{constraints}]";
	string modifierPrefix = 0 == modifiers.Count
		? string.Empty
		: string.Join( " ", modifiers ) + " ";
	return $"  M {modifierPrefix}{FormatTypeName( method.ReturnType, nullability.Create( method.ReturnParameter ) )} {method.Name}{genericArguments}{FormatParameters( method.GetParameters(), nullability )}{constraintSuffix}";
}

static string FormatParameters(
	ParameterInfo[] parameters,
	NullabilityInfoContext nullability
) {
	return $"({string.Join( ",", parameters.Select( value => FormatParameter( value, nullability ) ) )})";
}

static string FormatParameter(
	ParameterInfo parameter,
	NullabilityInfoContext nullability
) {
	Type parameterType = parameter.ParameterType;
	string prefix = string.Empty;
	if ( parameterType.IsByRef ) {
		prefix = parameter.IsOut
			? "out "
			: parameter.IsIn ? "in " : "ref ";
		parameterType = parameterType.GetElementType()
			?? throw new InvalidOperationException(
				$"By-ref parameter '{parameter.Name}' has no element type."
			);
	}
	if ( parameter.GetCustomAttribute<ParamArrayAttribute>() is not null ) {
		prefix = "params " + prefix;
	}

	string optional = parameter.HasDefaultValue
		? $"={FormatDefaultValue( parameter.DefaultValue, parameterType )}"
		: parameter.IsOptional ? "=<optional>" : string.Empty;
	return $"{prefix}{FormatTypeName( parameterType, nullability.Create( parameter ) )} {parameter.Name}{optional}";
}

static string FormatGenericConstraints(
	Type[] genericArguments
) {
	var clauses = new List<string>();
	foreach ( Type argument in genericArguments.Where( static value => value.IsGenericParameter ) ) {
		var constraints = new List<string>();
		GenericParameterAttributes attributes = argument.GenericParameterAttributes;
		GenericParameterAttributes special =
			attributes & GenericParameterAttributes.SpecialConstraintMask;
		if ( 0 != ( special & GenericParameterAttributes.ReferenceTypeConstraint ) ) {
			constraints.Add( "class" );
		}
		if ( 0 != ( special & GenericParameterAttributes.NotNullableValueTypeConstraint ) ) {
			constraints.Add( "struct" );
		}
		constraints.AddRange(
			argument.GetGenericParameterConstraints()
				.Select( static value => FormatTypeName( value, null ) )
				.OrderBy( static value => value, StringComparer.Ordinal )
		);
		if ( 0 != ( special & GenericParameterAttributes.DefaultConstructorConstraint )
			&& !constraints.Contains( "struct", StringComparer.Ordinal ) ) {
			constraints.Add( "new()" );
		}
		if ( 0 < constraints.Count ) {
			clauses.Add( $"{argument.Name}:{string.Join( "&", constraints )}" );
		}
	}

	return string.Join( ",", clauses );
}

static bool IsInitOnly(
	MethodInfo setter
) {
	return setter.ReturnParameter.GetRequiredCustomModifiers().Any(
		static value =>
			"System.Runtime.CompilerServices.IsExternalInit" == value.FullName
	);
}

static string FormatTypeName(
	Type type,
	NullabilityInfo? nullability
) {
	if ( type.IsByRef ) {
		type = type.GetElementType()
			?? throw new InvalidOperationException(
				"A by-ref type has no element type."
			);
	}

	if ( type.IsArray ) {
		Type element = type.GetElementType()
			?? throw new InvalidOperationException(
				"An array type has no element type."
			);
		string ranks = type.GetArrayRank() <= 1
			? "[]"
			: $"[{new string( ',', type.GetArrayRank() - 1 )}]";
		return FormatTypeName( element, nullability?.ElementType )
			+ ranks
			+ NullableSuffix( type, nullability );
	}

	Type? nullableUnderlying = Nullable.GetUnderlyingType( type );
	if ( nullableUnderlying is not null ) {
		return FormatTypeName(
			nullableUnderlying,
			nullability?.GenericTypeArguments.FirstOrDefault()
		) + "?";
	}

	if ( type.IsGenericParameter ) {
		return type.Name + NullableSuffix( type, nullability );
	}

	string name = GetNonGenericTypeName( type );
	if ( type.IsGenericType ) {
		NullabilityInfo[] nullableArguments = nullability?.GenericTypeArguments
			?? [];
		Type[] arguments = type.GetGenericArguments();
		string formattedArguments = string.Join(
			",",
			arguments.Select(
				(value, index) => FormatTypeName(
					value,
					index < nullableArguments.Length
						? nullableArguments[ index ]
						: null
				)
			)
		);
		name += $"<{formattedArguments}>";
	}

	return name + NullableSuffix( type, nullability );
}

static string GetNonGenericTypeName(
	Type type
) {
	string rawName = type.Name;
	int arity = rawName.IndexOf( '`' );
	if ( 0 <= arity ) {
		rawName = rawName[ ..arity ];
	}

	if ( type.IsNested && type.DeclaringType is not null ) {
		return GetNonGenericTypeName( type.DeclaringType ) + "." + rawName;
	}

	return string.IsNullOrEmpty( type.Namespace )
		? rawName
		: type.Namespace + "." + rawName;
}

static string NullableSuffix(
	Type type,
	NullabilityInfo? nullability
) {
	return !type.IsValueType
		&& NullabilityState.Nullable == nullability?.ReadState
		? "?"
		: string.Empty;
}

static string FormatDefaultValue(
	object? value,
	Type declaredType
) {
	if ( value is null ) {
		return "null";
	}
	if ( ReferenceEquals( value, Missing.Value ) ) {
		return "<missing>";
	}
	if ( value is string text ) {
		return $"\"{Escape( text )}\"";
	}
	if ( value is char character ) {
		return $"'{Escape( character.ToString() )}'";
	}
	if ( value is bool boolean ) {
		return boolean ? "true" : "false";
	}
	if ( declaredType.IsEnum ) {
		string? name = Enum.GetName( declaredType, value );
		return name is null
			? Convert.ToString( value, CultureInfo.InvariantCulture ) ?? string.Empty
			: FormatTypeName( declaredType, null ) + "." + name;
	}
	if ( value is IFormattable formattable ) {
		return formattable.ToString( null, CultureInfo.InvariantCulture );
	}

	return value.ToString() ?? string.Empty;
}

static string Escape(
	string value
) {
	return value
		.Replace( "\\", "\\\\", StringComparison.Ordinal )
		.Replace( "\"", "\\\"", StringComparison.Ordinal )
		.Replace( "\r", "\\r", StringComparison.Ordinal )
		.Replace( "\n", "\\n", StringComparison.Ordinal )
		.Replace( "\t", "\\t", StringComparison.Ordinal );
}
