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
/// Represents one opaque terminal-resident virtual raster placement whose semantic cells may be
/// rendered through the session's typed placeholder-output APIs.
/// </summary>
public sealed class TerminalRasterPlaceholder : IAsyncDisposable {
	private TerminalSession? session;

	internal TerminalRasterPlaceholder(
		TerminalSession session,
		int columns,
		int rows
	) {
		ArgumentNullException.ThrowIfNull( session );
		if ( columns is < 1 or > TerminalRasterPlaceholderOptions.MaximumExtent ) {
			throw new ArgumentOutOfRangeException(
				nameof( columns ),
				columns,
				$"A raster placeholder extent must be between 1 and {TerminalRasterPlaceholderOptions.MaximumExtent}."
			);
		}
		if ( rows is < 1 or > TerminalRasterPlaceholderOptions.MaximumExtent ) {
			throw new ArgumentOutOfRangeException(
				nameof( rows ),
				rows,
				$"A raster placeholder extent must be between 1 and {TerminalRasterPlaceholderOptions.MaximumExtent}."
			);
		}

		this.session = session;
		this.Columns = columns;
		this.Rows = rows;
	}

	/// <summary>
	/// Gets the virtual placeholder width in terminal cells.
	/// </summary>
	public int Columns {
		get;
	}

	/// <summary>
	/// Gets the virtual placeholder height in terminal cells.
	/// </summary>
	public int Rows {
		get;
	}

	/// <summary>
	/// Gets one side-effect-free snapshot of Icod.Terminal's current local ownership certainty for
	/// this placeholder.
	/// </summary>
	/// <remarks>
	/// A current result is local certainty only; it is not authenticated proof that terminal-side
	/// virtual-placement state still exists.
	/// </remarks>
	public TerminalRasterOwnershipState OwnershipState {
		get {
			return Volatile.Read( ref this.session ) is null
				? new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Disposed,
					TerminalRasterOwnershipLossReason.ExplicitDisposal
				)
				: new TerminalRasterOwnershipState(
					TerminalRasterOwnershipStatus.Current,
					TerminalRasterOwnershipLossReason.None
				)
			;
		}
	}

	internal TerminalSession? Owner {
		get {
			return Volatile.Read( ref this.session );
		}
	}

	/// <summary>
	/// Creates one semantic cell token for the supplied zero-based placeholder row and column.
	/// </summary>
	/// <param name="row">The zero-based row within this placeholder.</param>
	/// <param name="column">The zero-based column within this placeholder.</param>
	/// <returns>An opaque semantic cell token associated with this placeholder.</returns>
	public TerminalRasterPlaceholderCell GetCell(
		int row,
		int column
	) {
		if ( Volatile.Read( ref this.session ) is null ) {
			throw new ObjectDisposedException(
				nameof( TerminalRasterPlaceholder ),
				"The raster placeholder has already been disposed."
			);
		}
		if ( row < 0 || this.Rows <= row ) {
			throw new ArgumentOutOfRangeException(
				nameof( row ),
				row,
				"The placeholder row must identify a cell within the placeholder height."
			);
		}
		if ( column < 0 || this.Columns <= column ) {
			throw new ArgumentOutOfRangeException(
				nameof( column ),
				column,
				"The placeholder column must identify a cell within the placeholder width."
			);
		}

		return new TerminalRasterPlaceholderCell(
			this,
			row,
			column
		);
	}

	/// <summary>
	/// Releases this wrapper's local ownership. Terminal-side virtual-placement cleanup is added by
	/// the virtual-placement implementation tranche.
	/// </summary>
	public ValueTask DisposeAsync() {
		_ = Interlocked.Exchange(
			ref this.session,
			null
		);
		return ValueTask.CompletedTask;
	}
}
