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

/// <summary>
/// Represents one validated logical terminal-progress value.
/// </summary>
internal readonly struct TerminalProgressValue {
	private TerminalProgressValue(
		bool isIndeterminate,
		TerminalProgressState? state,
		long completed,
		long total,
		int percentage
	) {
		this.IsIndeterminate = isIndeterminate;
		this.State = state;
		this.Completed = completed;
		this.Total = total;
		this.Percentage = percentage;
	}

	internal bool IsIndeterminate {
		get;
	}

	internal TerminalProgressState? State {
		get;
	}

	internal long Completed {
		get;
	}

	internal long Total {
		get;
	}

	internal int Percentage {
		get;
	}

	internal static TerminalProgressValue CreateDeterminate(
		TerminalProgressState state,
		long completed,
		long total
	) {
		if ( !Enum.IsDefined( state ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( state ),
				state,
				"Terminal progress state must be normal, error, or attention."
			);
		}
		if ( 0 >= total ) {
			throw new ArgumentOutOfRangeException(
				nameof( total ),
				total,
				"Terminal progress total must be positive."
			);
		}
		if ( 0 > completed ) {
			throw new ArgumentOutOfRangeException(
				nameof( completed ),
				completed,
				"Terminal progress completed work may not be negative."
			);
		}
		if ( completed > total ) {
			throw new ArgumentOutOfRangeException(
				nameof( completed ),
				completed,
				"Terminal progress completed work may not exceed total work."
			);
		}

		int percentage = ConvertToPercentage(
			completed,
			total
		);
		return new TerminalProgressValue(
			isIndeterminate: false,
			state,
			completed,
			total,
			percentage
		);
	}

	internal static TerminalProgressValue CreateIndeterminate() {
		return new TerminalProgressValue(
			isIndeterminate: true,
			state: null,
			completed: 0,
			total: 0,
			percentage: 0
		);
	}

	internal Osc9ProgressState GetWireState() {
		if ( this.IsIndeterminate ) {
			return Osc9ProgressState.Indeterminate;
		}

		return this.State switch {
			TerminalProgressState.Normal => Osc9ProgressState.Normal,
			TerminalProgressState.Error => Osc9ProgressState.Error,
			TerminalProgressState.Attention => Osc9ProgressState.Attention,
			_ => throw new InvalidOperationException(
				"A determinate terminal progress value must have a semantic state."
			)
		};
	}

	private static int ConvertToPercentage(
		long completed,
		long total
	) {
		UInt128 numerator = (UInt128)(ulong)completed * 100u;
		UInt128 denominator = (UInt128)(ulong)total;
		UInt128 quotient = numerator / denominator;
		UInt128 remainder = numerator % denominator;
		if ( ( remainder * 2u ) >= denominator ) {
			++quotient;
		}

		return checked( (int)quotient );
	}
}
