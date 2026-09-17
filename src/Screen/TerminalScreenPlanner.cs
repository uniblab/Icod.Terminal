/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

using Icod.TermInfo;

/// <summary>Creates side-effect-free semantic screen-operation plans for one terminal session.</summary>
public sealed class TerminalScreenPlanner {
	// Keep repeated sources within the padding parser's bounded input size.
	// Oversized fallback-only operations are unavailable instead of allocating without limit.
	private const int MaximumRepeatedSourceLength = 1_048_576;

	private readonly TerminalDescription terminal;
	private readonly TerminalColorSupport colorSupport;
	private readonly TerminalTextAttributes reversibleAttributes;

	internal TerminalScreenPlanner(
		TerminalDescription terminal,
		TerminalProfile profile
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( profile );
		this.terminal = terminal;
		this.colorSupport = TerminalColors.GetColorSupport( terminal );
		this.Profile = profile;
		this.reversibleAttributes = GetReversibleAttributes(
			terminal,
			profile.Screen.SupportedAttributes
		);
	}

	/// <summary>Gets the Terminal-owned semantic profile used for planning.</summary>
	public TerminalProfile Profile {
		get;
	}

	/// <summary>Plans the shortest safe cursor movement advertised by the selected profile.</summary>
	public TerminalScreenOperationPlan? PlanCursorMove(
		TerminalScreenPosition? current,
		TerminalScreenPosition target
	) {
		TerminalScreenOperationPlan? best = null;
		this.TryExpanded(
			TerminalScreenOperationKind.CursorMove,
			StringCapability.CursorAddress,
			[ target.Row, target.Column ],
			1,
			ref best
		);

		if ( 0 == target.Row && 0 == target.Column ) {
			this.TryLiteral(
				TerminalScreenOperationKind.CursorMove,
				StringCapability.CursorHome,
				1,
				ref best
			);
		}

		TerminalScreenOperationPlan? row = this.CreateExpanded(
			TerminalScreenOperationKind.CursorMove,
			StringCapability.RowAddress,
			[ target.Row ],
			1
		);
		TerminalScreenOperationPlan? column = this.CreateExpanded(
			TerminalScreenOperationKind.CursorMove,
			StringCapability.ColumnAddress,
			[ target.Column ],
			1
		);
		if ( row.HasValue && column.HasValue ) {
			ChooseBetter( ref best, Combine( row.Value, column.Value ) );
		}

		if ( current.HasValue ) {
			if ( current.Value.Row == target.Row && 0 == target.Column ) {
				this.TryLiteral(
					TerminalScreenOperationKind.CursorMove,
					StringCapability.CarriageReturn,
					1,
					ref best
				);
			}
			if ( current.Value.Row == target.Row ) {
				TerminalScreenOperationPlan? sameRowColumn = this.CreateExpanded(
					TerminalScreenOperationKind.CursorMove,
					StringCapability.ColumnAddress,
					[ target.Column ],
					1
				);
				if ( sameRowColumn.HasValue ) {
					ChooseBetter( ref best, sameRowColumn.Value );
				}
			}
			if ( current.Value.Column == target.Column ) {
				TerminalScreenOperationPlan? sameColumnRow = this.CreateExpanded(
					TerminalScreenOperationKind.CursorMove,
					StringCapability.RowAddress,
					[ target.Row ],
					1
				);
				if ( sameColumnRow.HasValue ) {
					ChooseBetter( ref best, sameColumnRow.Value );
				}
			}

			TerminalScreenOperationPlan? vertical = this.PlanRelativeAxis(
				target.Row - current.Value.Row,
				StringCapability.CursorUp,
				StringCapability.CursorUpOne,
				StringCapability.CursorDown,
				StringCapability.CursorDownOne
			);
			TerminalScreenOperationPlan? horizontal = this.PlanRelativeAxis(
				target.Column - current.Value.Column,
				StringCapability.CursorLeft,
				StringCapability.CursorLeftOne,
				StringCapability.CursorRight,
				StringCapability.CursorRightOne
			);
			if ( vertical.HasValue && horizontal.HasValue ) {
				ChooseBetter( ref best, Combine( vertical.Value, horizontal.Value ) );
			}
		}

		return best;
	}

	/// <summary>Normalizes a rendition request to a safe reversible representation.</summary>
	public TerminalScreenRendition NormalizeRendition(
		TerminalScreenRendition requested
	) {
		TerminalScreenColor foreground = this.ResolveColor(
			requested.Foreground,
			foreground: true
		);
		TerminalScreenColor background = this.ResolveColor(
			requested.Background,
			foreground: false
		);
		TerminalTextAttributes attributes = requested.Attributes
			& this.reversibleAttributes;
		if ( 0 != ( requested.Attributes & TerminalTextAttributes.Standout )
			&& 0 == ( attributes & TerminalTextAttributes.Standout )
			&& 0 != ( this.reversibleAttributes & TerminalTextAttributes.Reverse ) ) {
			attributes |= TerminalTextAttributes.Reverse;
		}
		if ( !foreground.IsDefault || !background.IsDefault ) {
			attributes &= ~this.Profile.Screen.ColorRestrictedAttributes;
		}
		return new TerminalScreenRendition(
			foreground,
			background,
			attributes
		);
	}

	/// <summary>Plans a safe transition between normalized screen renditions.</summary>
	public TerminalScreenOperationPlan? PlanRenditionTransition(
		TerminalScreenRendition current,
		TerminalScreenRendition target
	) {
		TerminalScreenRendition from = this.NormalizeRendition( current );
		TerminalScreenRendition to = this.NormalizeRendition( target );
		if ( from == to ) {
			return this.Create(
				TerminalScreenOperationKind.Rendition,
				string.Empty,
				1
			);
		}

		List<TerminalScreenOutputSegment> segments = [];
		if ( !from.IsDefault ) {
			if ( TerminalTextAttributes.None != from.Attributes ) {
				if ( !this.TryAddLiteral( segments, StringCapability.ExitAttributeMode, 1 )
					&& !this.TryAddAttributeExits( segments, from.Attributes ) ) {
					return null;
				}
			}
			if ( !from.Foreground.IsDefault || !from.Background.IsDefault ) {
				if ( !this.TryAddLiteral( segments, StringCapability.OriginalColorPair, 1 ) ) {
					return null;
				}
			}
		}

		if ( !this.TryAddColor( segments, to.Foreground, foreground: true )
			|| !this.TryAddColor( segments, to.Background, foreground: false )
			|| !this.TryAddAttributeEnters( segments, to.Attributes ) ) {
			return null;
		}

		return this.Create(
			TerminalScreenOperationKind.Rendition,
			segments,
			1
		);
	}

	/// <summary>Plans a rendition reset from the supplied current state.</summary>
	public TerminalScreenOperationPlan? PlanRenditionReset(
		TerminalScreenRendition current
	) {
		return this.PlanRenditionTransition(
			current,
			TerminalScreenRendition.Default
		);
	}

	/// <summary>Resolves a semantic line glyph through the advertised alternate-character-set map.</summary>
	public TerminalLineGlyphRepresentation? ResolveLineGlyph(
		TerminalLineGlyph glyph
	) {
		if ( !Enum.IsDefined( glyph ) ) {
			throw new ArgumentOutOfRangeException( nameof( glyph ) );
		}
		if ( !this.Profile.Screen.SupportsAlternateCharacterSet ) {
			return null;
		}
		string? mapping = this.terminal.GetString( StringCapability.AlternateCharacterSet );
		if ( string.IsNullOrEmpty( mapping ) ) {
			return null;
		}
		char source = GetAlternateCharacterSetSource( glyph );
		for ( int index = 0; index + 1 < mapping.Length; index += 2 ) {
			if ( mapping[ index ] != source ) {
				continue;
			}
			char mapped = mapping[ index + 1 ];
			return char.IsControl( mapped )
				? null
				: new TerminalLineGlyphRepresentation(
					mapped.ToString(),
					usesAlternateCharacterSet: true
				);
		}
		return null;
	}

	/// <summary>Plans entry to or exit from alternate-character-set mode.</summary>
	public TerminalScreenOperationPlan? PlanAlternateCharacterSet(
		bool enabled
	) {
		return this.CreateLiteral(
			TerminalScreenOperationKind.AlternateCharacterSet,
			enabled
				? StringCapability.EnterAlternateCharacterSetMode
				: StringCapability.ExitAlternateCharacterSetMode,
			1
		);
	}

	/// <summary>Plans the preferred alert with the opposite presentation as fallback.</summary>
	public TerminalScreenOperationPlan? PlanAlert(
		TerminalAlertKind kind
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		StringCapability preferred = TerminalAlertKind.Audible == kind
			? StringCapability.Bell
			: StringCapability.FlashScreen;
		StringCapability fallback = TerminalAlertKind.Audible == kind
			? StringCapability.FlashScreen
			: StringCapability.Bell;
		return this.CreateLiteral(
			TerminalScreenOperationKind.Alert,
			preferred,
			1
		) ?? this.CreateLiteral(
			TerminalScreenOperationKind.Alert,
			fallback,
			1
		);
	}

	/// <summary>Plans one advertised semantic erase operation.</summary>
	public TerminalScreenOperationPlan? PlanErase(
		TerminalScreenEraseKind kind,
		int affectedLines = 1
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException( nameof( kind ) );
		}
		ValidateAffectedLines( affectedLines );
		StringCapability capability = kind switch {
			TerminalScreenEraseKind.ToEndOfLine => StringCapability.ClearToEndOfLine,
			TerminalScreenEraseKind.ToBeginningOfLine => StringCapability.ClearToBeginningOfLine,
			TerminalScreenEraseKind.ToEndOfScreen => StringCapability.ClearToEndOfScreen,
			TerminalScreenEraseKind.Screen => StringCapability.ClearScreen,
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
		return this.CreateLiteral(
			TerminalScreenOperationKind.Erase,
			capability,
			affectedLines
		);
	}

	/// <summary>Plans insertion, deletion, or erasure of character positions.</summary>
	public TerminalScreenOperationPlan? PlanCharacterShift(
		TerminalScreenCharacterShiftKind kind,
		int count
	) {
		ValidatePositiveCount( count );
		return kind switch {
			TerminalScreenCharacterShiftKind.Insert => this.PlanRepeatedOperation(
				TerminalScreenOperationKind.CharacterShift,
				StringCapability.InsertCharacters,
				StringCapability.InsertCharacter,
				count,
				1
			),
			TerminalScreenCharacterShiftKind.Delete => this.PlanRepeatedOperation(
				TerminalScreenOperationKind.CharacterShift,
				StringCapability.DeleteCharacters,
				StringCapability.DeleteCharacter,
				count,
				1
			),
			TerminalScreenCharacterShiftKind.Erase => this.CreateExpanded(
				TerminalScreenOperationKind.CharacterShift,
				StringCapability.EraseCharacters,
				[ count ],
				1
			),
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
	}

	/// <summary>Plans insertion, deletion, or scrolling of terminal lines.</summary>
	public TerminalScreenOperationPlan? PlanLineShift(
		TerminalScreenLineShiftKind kind,
		int count,
		int affectedLines
	) {
		ValidatePositiveCount( count );
		ValidateAffectedLines( affectedLines );
		return kind switch {
			TerminalScreenLineShiftKind.Insert => this.PlanRepeatedOperation(
				TerminalScreenOperationKind.LineShift,
				StringCapability.InsertLines,
				StringCapability.InsertLine,
				count,
				affectedLines
			),
			TerminalScreenLineShiftKind.Delete => this.PlanRepeatedOperation(
				TerminalScreenOperationKind.LineShift,
				StringCapability.DeleteLines,
				StringCapability.DeleteLine,
				count,
				affectedLines
			),
			TerminalScreenLineShiftKind.ScrollForward => this.PlanRepeatedOperation(
				TerminalScreenOperationKind.LineShift,
				StringCapability.ScrollForwardLines,
				StringCapability.ScrollForward,
				count,
				affectedLines
			),
			TerminalScreenLineShiftKind.ScrollReverse => this.PlanRepeatedOperation(
				TerminalScreenOperationKind.LineShift,
				StringCapability.ScrollReverseLines,
				StringCapability.ScrollReverse,
				count,
				affectedLines
			),
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
	}

	/// <summary>Plans a zero-based inclusive scrolling region.</summary>
	public TerminalScreenOperationPlan? PlanScrollRegion(
		int topRow,
		int bottomRow,
		int affectedLines
	) {
		if ( 0 > topRow ) {
			throw new ArgumentOutOfRangeException( nameof( topRow ) );
		}
		if ( topRow > bottomRow ) {
			throw new ArgumentOutOfRangeException( nameof( bottomRow ) );
		}
		ValidateAffectedLines( affectedLines );
		return this.CreateExpanded(
			TerminalScreenOperationKind.ScrollRegion,
			StringCapability.ChangeScrollRegion,
			[ topRow, bottomRow ],
			affectedLines
		);
	}

	private TerminalScreenOperationPlan? PlanRelativeAxis(
		int delta,
		StringCapability negativeParameterized,
		StringCapability negativeOne,
		StringCapability positiveParameterized,
		StringCapability positiveOne
	) {
		if ( 0 == delta ) {
			return this.Create(
				TerminalScreenOperationKind.CursorMove,
				string.Empty,
				1
			);
		}
		int count = Math.Abs( delta );
		return this.PlanRepeatedOperation(
			TerminalScreenOperationKind.CursorMove,
			0 > delta ? negativeParameterized : positiveParameterized,
			0 > delta ? negativeOne : positiveOne,
			count,
			1
		);
	}

	private TerminalScreenOperationPlan? PlanRepeatedOperation(
		TerminalScreenOperationKind operationKind,
		StringCapability parameterizedCapability,
		StringCapability oneCapability,
		int count,
		int affectedLines
	) {
		TerminalScreenOperationPlan? best = this.CreateExpanded(
			operationKind,
			parameterizedCapability,
			[ count ],
			affectedLines
		);
		string? literal = this.terminal.GetString( oneCapability );
		if ( literal is not null
			&& (long)literal.Length * count <= MaximumRepeatedSourceLength ) {
			TerminalScreenOperationPlan single = this.Create( operationKind, literal, affectedLines );
			long repeatedByteCount = (long)single.ByteCount * count;
			if ( best.HasValue && repeatedByteCount >= best.Value.ByteCount ) {
				return best;
			}
			TerminalScreenOperationPlan repeated = this.Create(
				operationKind,
				0 == literal.Length ? string.Empty : string.Concat( Enumerable.Repeat( literal, count ) ),
				affectedLines
			);
			ChooseBetter( ref best, repeated );
		}
		return best;
	}

	private TerminalScreenColor ResolveColor(
		TerminalScreenColor requested,
		bool foreground
	) {
		if ( TerminalScreenColorKind.Default == requested.Kind ) {
			return TerminalScreenColor.Default;
		}
		bool hasSelector = foreground
			? this.colorSupport.HasForegroundSelector
			: this.colorSupport.HasBackgroundSelector;
		if ( !hasSelector || !this.colorSupport.HasOriginalColorPair ) {
			return TerminalScreenColor.Default;
		}

		switch ( requested.Kind ) {
			case TerminalScreenColorKind.Indexed:
				int index = requested.Index
					?? throw new InvalidOperationException( "An indexed screen color has no index." );
				return 0 <= index && index < this.colorSupport.IndexedColorCount
					? requested
					: TerminalScreenColor.Default;

			case TerminalScreenColorKind.Rgb:
				if ( TerminalColorModel.DirectRgb != this.colorSupport.Model
					|| !requested.Red.HasValue
					|| !requested.Green.HasValue
					|| !requested.Blue.HasValue
					|| !this.colorSupport.RgbLayout.HasValue
					|| !this.colorSupport.ColorCount.HasValue ) {
					return TerminalScreenColor.Default;
				}
				TerminalRgbColor rgb = new(
					requested.Red.Value,
					requested.Green.Value,
					requested.Blue.Value
				);
				int packed = this.colorSupport.RgbLayout.Value.Pack( rgb );
				return packed >= this.colorSupport.ColorCount.Value
					|| ( 0 < packed && packed < this.colorSupport.IndexedColorCount )
					? TerminalScreenColor.Default
					: requested;

			default:
				throw new ArgumentOutOfRangeException( nameof( requested ) );
		}
	}

	private bool TryAddColor(
		List<TerminalScreenOutputSegment> segments,
		TerminalScreenColor color,
		bool foreground
	) {
		if ( color.IsDefault ) {
			return true;
		}
		string value = color.Kind switch {
			TerminalScreenColorKind.Indexed => foreground
				? TerminalColors.ExpandForeground( this.terminal, color.Index!.Value )
				: TerminalColors.ExpandBackground( this.terminal, color.Index!.Value ),
			TerminalScreenColorKind.Rgb => foreground
				? TerminalColors.ExpandForeground(
					this.terminal,
					new TerminalRgbColor( color.Red!.Value, color.Green!.Value, color.Blue!.Value )
				)
				: TerminalColors.ExpandBackground(
					this.terminal,
					new TerminalRgbColor( color.Red!.Value, color.Green!.Value, color.Blue!.Value )
				),
			_ => throw new ArgumentOutOfRangeException( nameof( color ) )
		};
		segments.Add( new TerminalScreenOutputSegment( value, 1 ) );
		return true;
	}

	private bool TryAddAttributeEnters(
		List<TerminalScreenOutputSegment> segments,
		TerminalTextAttributes attributes
	) {
		return this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Bold, StringCapability.EnterBoldMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Dim, StringCapability.EnterDimMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Underline, StringCapability.EnterUnderlineMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Reverse, StringCapability.EnterReverseMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Standout, StringCapability.EnterStandoutMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Italic, StringCapability.EnterItalicMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Blink, StringCapability.EnterBlinkMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Conceal, StringCapability.EnterInvisibleMode )
			&& this.TryAddExtendedAttribute( segments, attributes, TerminalTextAttributes.Strikeout, "smxx" );
	}

	private bool TryAddAttribute(
		List<TerminalScreenOutputSegment> segments,
		TerminalTextAttributes attributes,
		TerminalTextAttributes flag,
		StringCapability capability
	) {
		return 0 == ( attributes & flag )
			|| this.TryAddLiteral( segments, capability, 1 );
	}

	private bool TryAddExtendedAttribute(
		List<TerminalScreenOutputSegment> segments,
		TerminalTextAttributes attributes,
		TerminalTextAttributes flag,
		string capabilityName
	) {
		if ( 0 == ( attributes & flag ) ) {
			return true;
		}
		if ( !this.terminal.TryGetExtendedString( capabilityName, out string? value )
			|| value is null ) {
			return false;
		}
		segments.Add( new TerminalScreenOutputSegment( value, 1 ) );
		return true;
	}

	private bool TryAddAttributeExits(
		List<TerminalScreenOutputSegment> segments,
		TerminalTextAttributes attributes
	) {
		return this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Underline, StringCapability.ExitUnderlineMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Standout, StringCapability.ExitStandoutMode )
			&& this.TryAddAttribute( segments, attributes, TerminalTextAttributes.Italic, StringCapability.ExitItalicMode )
			&& this.TryAddExtendedAttribute( segments, attributes, TerminalTextAttributes.Strikeout, "rmxx" );
	}

	private bool TryAddLiteral(
		List<TerminalScreenOutputSegment> segments,
		StringCapability capability,
		int affectedLines
	) {
		string? value = this.terminal.GetString( capability );
		if ( value is null ) {
			return false;
		}
		segments.Add( new TerminalScreenOutputSegment( value, affectedLines ) );
		return true;
	}

	private void TryExpanded(
		TerminalScreenOperationKind kind,
		StringCapability capability,
		TermInfoParameter[] parameters,
		int affectedLines,
		ref TerminalScreenOperationPlan? best
	) {
		TerminalScreenOperationPlan? candidate = this.CreateExpanded(
			kind,
			capability,
			parameters,
			affectedLines
		);
		if ( candidate.HasValue ) {
			ChooseBetter( ref best, candidate.Value );
		}
	}

	private void TryLiteral(
		TerminalScreenOperationKind kind,
		StringCapability capability,
		int affectedLines,
		ref TerminalScreenOperationPlan? best
	) {
		TerminalScreenOperationPlan? candidate = this.CreateLiteral(
			kind,
			capability,
			affectedLines
		);
		if ( candidate.HasValue ) {
			ChooseBetter( ref best, candidate.Value );
		}
	}

	private TerminalScreenOperationPlan? CreateExpanded(
		TerminalScreenOperationKind kind,
		StringCapability capability,
		TermInfoParameter[] parameters,
		int affectedLines
	) {
		ArgumentNullException.ThrowIfNull( parameters );
		return null == this.terminal.GetString( capability )
			? null
			: this.Create(
				kind,
				this.terminal.Expand( capability, parameters ),
				affectedLines
			);
	}

	private TerminalScreenOperationPlan? CreateLiteral(
		TerminalScreenOperationKind kind,
		StringCapability capability,
		int affectedLines
	) {
		string? value = this.terminal.GetString( capability );
		return value is null ? null : this.Create( kind, value, affectedLines );
	}

	private TerminalScreenOperationPlan Create(
		TerminalScreenOperationKind kind,
		string value,
		int affectedLines
	) {
		return this.Create(
			kind,
			[ new TerminalScreenOutputSegment( value, affectedLines ) ],
			affectedLines
		);
	}

	private TerminalScreenOperationPlan Create(
		TerminalScreenOperationKind kind,
		IReadOnlyList<TerminalScreenOutputSegment> segments,
		int affectedLines
	) {
		int byteCount = 0;
		foreach ( TerminalScreenOutputSegment segment in segments ) {
			TermInfoOutput.TPuts(
				segment.Value,
				segment.AffectedLines,
				_ => byteCount = checked( byteCount + 1 )
			);
		}
		return new TerminalScreenOperationPlan(
			this,
			kind,
			segments,
			byteCount,
			affectedLines
		);
	}

	private static TerminalScreenOperationPlan Combine(
		TerminalScreenOperationPlan first,
		TerminalScreenOperationPlan second
	) {
		if ( !ReferenceEquals( first.Owner, second.Owner ) ) {
			throw new ArgumentException( "Screen plans must belong to the same planner." );
		}
		return new TerminalScreenOperationPlan(
			first.Owner!,
			first.Kind,
			[ .. first.Segments!, .. second.Segments! ],
			checked( first.ByteCount + second.ByteCount ),
			Math.Max( first.AffectedLines, second.AffectedLines )
		);
	}

	private static void ChooseBetter(
		ref TerminalScreenOperationPlan? current,
		TerminalScreenOperationPlan candidate
	) {
		if ( !current.HasValue || candidate.ByteCount < current.Value.ByteCount ) {
			current = candidate;
		}
	}

	private static TerminalTextAttributes GetReversibleAttributes(
		TerminalDescription terminal,
		TerminalTextAttributes supportedAttributes
	) {
		if ( null != terminal.GetString( StringCapability.ExitAttributeMode ) ) {
			return supportedAttributes;
		}
		TerminalTextAttributes result = TerminalTextAttributes.None;
		if ( 0 != ( supportedAttributes & TerminalTextAttributes.Underline )
			&& null != terminal.GetString( StringCapability.ExitUnderlineMode ) ) {
			result |= TerminalTextAttributes.Underline;
		}
		if ( 0 != ( supportedAttributes & TerminalTextAttributes.Standout )
			&& null != terminal.GetString( StringCapability.ExitStandoutMode ) ) {
			result |= TerminalTextAttributes.Standout;
		}
		if ( 0 != ( supportedAttributes & TerminalTextAttributes.Italic )
			&& null != terminal.GetString( StringCapability.ExitItalicMode ) ) {
			result |= TerminalTextAttributes.Italic;
		}
		if ( 0 != ( supportedAttributes & TerminalTextAttributes.Strikeout )
			&& terminal.TryGetExtendedString( "rmxx", out _ ) ) {
			result |= TerminalTextAttributes.Strikeout;
		}
		return result;
	}

	private static char GetAlternateCharacterSetSource(
		TerminalLineGlyph glyph
	) {
		return glyph switch {
			TerminalLineGlyph.Horizontal => 'q',
			TerminalLineGlyph.Vertical => 'x',
			TerminalLineGlyph.UpperLeftCorner => 'l',
			TerminalLineGlyph.UpperRightCorner => 'k',
			TerminalLineGlyph.LowerLeftCorner => 'm',
			TerminalLineGlyph.LowerRightCorner => 'j',
			TerminalLineGlyph.TeeUp => 'v',
			TerminalLineGlyph.TeeDown => 'w',
			TerminalLineGlyph.TeeLeft => 'u',
			TerminalLineGlyph.TeeRight => 't',
			TerminalLineGlyph.Crossing => 'n',
			_ => throw new ArgumentOutOfRangeException( nameof( glyph ) )
		};
	}

	private static void ValidatePositiveCount(
		int count
	) {
		if ( 0 >= count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}
	}

	private static void ValidateAffectedLines(
		int affectedLines
	) {
		if ( 0 >= affectedLines ) {
			throw new ArgumentOutOfRangeException( nameof( affectedLines ) );
		}
	}
}
