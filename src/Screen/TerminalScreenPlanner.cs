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
	private readonly TerminalDescription terminal;

	internal TerminalScreenPlanner(
		TerminalDescription terminal,
		TerminalProfile profile
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( profile );
		this.terminal = terminal;
		this.Profile = profile;
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
			ref best
		);

		if ( 0 == target.Row && 0 == target.Column ) {
			this.TryLiteral(
				TerminalScreenOperationKind.CursorMove,
				StringCapability.CursorHome,
				ref best
			);
		}

		TerminalScreenOperationPlan? row = this.CreateExpanded(
			TerminalScreenOperationKind.CursorMove,
			StringCapability.RowAddress,
			[ target.Row ]
		);
		TerminalScreenOperationPlan? column = this.CreateExpanded(
			TerminalScreenOperationKind.CursorMove,
			StringCapability.ColumnAddress,
			[ target.Column ]
		);
		if ( row.HasValue && column.HasValue ) {
			ChooseBetter( ref best, Combine( row.Value, column.Value ) );
		}

		if ( current.HasValue ) {
			if ( current.Value.Row == target.Row && 0 == target.Column ) {
				this.TryLiteral(
					TerminalScreenOperationKind.CursorMove,
					StringCapability.CarriageReturn,
					ref best
				);
			}
			if ( current.Value.Row == target.Row ) {
				TerminalScreenOperationPlan? sameRowColumn = this.CreateExpanded(
					TerminalScreenOperationKind.CursorMove,
					StringCapability.ColumnAddress,
					[ target.Column ]
				);
				if ( sameRowColumn.HasValue ) {
					ChooseBetter( ref best, sameRowColumn.Value );
				}
			}
			if ( current.Value.Column == target.Column ) {
				TerminalScreenOperationPlan? sameColumnRow = this.CreateExpanded(
					TerminalScreenOperationKind.CursorMove,
					StringCapability.RowAddress,
					[ target.Row ]
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
			preferred
		) ?? this.CreateLiteral(
			TerminalScreenOperationKind.Alert,
			fallback
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
		StringCapability parameterized = 0 > delta
			? negativeParameterized
			: positiveParameterized;
		StringCapability one = 0 > delta
			? negativeOne
			: positiveOne;
		TerminalScreenOperationPlan? best = this.CreateExpanded(
			TerminalScreenOperationKind.CursorMove,
			parameterized,
			[ count ]
		);
		string? literal = this.terminal.GetString( one );
		if ( literal is not null ) {
			TerminalScreenOperationPlan repeated = this.Create(
				TerminalScreenOperationKind.CursorMove,
				string.Concat( Enumerable.Repeat( literal, count ) ),
				1
			);
			ChooseBetter( ref best, repeated );
		}
		return best;
	}

	private void TryExpanded(
		TerminalScreenOperationKind kind,
		StringCapability capability,
		TermInfoParameter[] parameters,
		ref TerminalScreenOperationPlan? best
	) {
		TerminalScreenOperationPlan? candidate = this.CreateExpanded(
			kind,
			capability,
			parameters
		);
		if ( candidate.HasValue ) {
			ChooseBetter( ref best, candidate.Value );
		}
	}

	private void TryLiteral(
		TerminalScreenOperationKind kind,
		StringCapability capability,
		ref TerminalScreenOperationPlan? best
	) {
		TerminalScreenOperationPlan? candidate = this.CreateLiteral( kind, capability );
		if ( candidate.HasValue ) {
			ChooseBetter( ref best, candidate.Value );
		}
	}

	private TerminalScreenOperationPlan? CreateExpanded(
		TerminalScreenOperationKind kind,
		StringCapability capability,
		TermInfoParameter[] parameters
	) {
		ArgumentNullException.ThrowIfNull( parameters );
		return null == this.terminal.GetString( capability )
			? null
			: this.Create(
				kind,
				this.terminal.Expand( capability, parameters ),
				1
			);
	}

	private TerminalScreenOperationPlan? CreateLiteral(
		TerminalScreenOperationKind kind,
		StringCapability capability
	) {
		string? value = this.terminal.GetString( capability );
		return value is null ? null : this.Create( kind, value, 1 );
	}

	private TerminalScreenOperationPlan Create(
		TerminalScreenOperationKind kind,
		string value,
		int affectedLines
	) {
		int byteCount = 0;
		TermInfoOutput.TPuts(
			value,
			affectedLines,
			_ => byteCount = checked( byteCount + 1 )
		);
		return new TerminalScreenOperationPlan(
			this,
			kind,
			[ new TerminalScreenOutputSegment( value, affectedLines ) ],
			byteCount
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
			checked( first.ByteCount + second.ByteCount )
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
}
