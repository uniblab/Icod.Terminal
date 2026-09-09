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
/// Identifies how one correlated response completes a logical terminal query.
/// </summary>
internal enum TerminalQueryResponseDisposition {
	Completion,
	Barrier
}

/// <summary>
/// Associates one family-specific response matcher with its query disposition and frame bound.
/// </summary>
internal sealed class TerminalQueryResponseRule {
	internal TerminalQueryResponseRule(
		ITerminalResponseMatcher matcher,
		TerminalQueryResponseDisposition disposition,
		int maximumFrameBytes = TerminalResponseFramer.DefaultMaximumFrameBytes
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		if ( !Enum.IsDefined( disposition ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( disposition ),
				disposition,
				"The terminal query response disposition is not recognized."
			);
		}
		if ( 4 > maximumFrameBytes
			|| TerminalResponseFramer.HardMaximumFrameBytes < maximumFrameBytes ) {
			throw new ArgumentOutOfRangeException(
				nameof( maximumFrameBytes )
			);
		}

		this.Matcher = matcher;
		this.Disposition = disposition;
		this.MaximumFrameBytes = maximumFrameBytes;
	}

	internal ITerminalResponseMatcher Matcher {
		get;
	}

	internal TerminalQueryResponseDisposition Disposition {
		get;
	}

	internal int MaximumFrameBytes {
		get;
	}

	internal TerminalControlFamily Family {
		get {
			return TerminalResponseFrameKinds.GetControlFamily(
				this.Matcher.FrameKind
			);
		}
	}
}

/// <summary>
/// Defines the bounded set of response families which may complete one logical terminal query.
/// </summary>
internal sealed class TerminalQueryResponsePlan {
	internal const int MaximumRules = 6;

	private readonly TerminalQueryResponseRule[] rules;
	private readonly TerminalControlFamily[] families;

	internal TerminalQueryResponsePlan(
		params TerminalQueryResponseRule[] rules
	) {
		ArgumentNullException.ThrowIfNull( rules );
		if ( 0 == rules.Length ) {
			throw new ArgumentException(
				"A terminal query response plan must contain at least one response rule.",
				nameof( rules )
			);
		}
		if ( MaximumRules < rules.Length ) {
			throw new ArgumentOutOfRangeException(
				nameof( rules ),
				rules.Length,
				$"A terminal query response plan cannot contain more than {MaximumRules} rules."
			);
		}

		this.rules = new TerminalQueryResponseRule[ rules.Length ];
		this.families = new TerminalControlFamily[ rules.Length ];
		bool hasCompletion = false;
		for ( int index = 0; index < rules.Length; index++ ) {
			TerminalQueryResponseRule rule = rules[ index ]
				?? throw new ArgumentException(
					"A terminal query response plan cannot contain a null rule.",
					nameof( rules )
				);
			TerminalControlFamily family = rule.Family;
			for ( int prior = 0; prior < index; prior++ ) {
				if ( this.families[ prior ] == family ) {
					throw new ArgumentException(
						"A terminal query response plan cannot contain more than one rule for the same control family.",
						nameof( rules )
					);
				}
			}

			this.rules[ index ] = rule;
			this.families[ index ] = family;
			hasCompletion |= TerminalQueryResponseDisposition.Completion == rule.Disposition;
		}
		if ( !hasCompletion ) {
			throw new ArgumentException(
				"A terminal query response plan must contain at least one completion response.",
				nameof( rules )
			);
		}
	}

	internal IReadOnlyList<TerminalQueryResponseRule> Rules {
		get {
			return this.rules;
		}
	}

	internal IReadOnlyList<TerminalControlFamily> Families {
		get {
			return this.families;
		}
	}

	internal static TerminalQueryResponsePlan ForCompletion(
		ITerminalResponseMatcher matcher
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		int maximumFrameBytes = TerminalResponseFrameKind.Osc == matcher.FrameKind
			? TerminalOsc52PayloadCodec.MaximumFrameBytes
			: TerminalResponseFramer.DefaultMaximumFrameBytes
		;
		return new TerminalQueryResponsePlan(
			new TerminalQueryResponseRule(
				matcher,
				TerminalQueryResponseDisposition.Completion,
				maximumFrameBytes
			)
		);
	}

	internal TerminalQueryResponseRule GetRule(
		TerminalControlFamily family
	) {
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}

		for ( int index = 0; index < this.rules.Length; index++ ) {
			if ( this.families[ index ] == family ) {
				return this.rules[ index ];
			}
		}

		throw new ArgumentException(
			"The terminal query response plan does not accept the requested control family.",
			nameof( family )
		);
	}

	internal bool TryMatch(
		TerminalResponseFrame frame,
		out TerminalQueryResponseDisposition disposition
	) {
		ArgumentNullException.ThrowIfNull( frame );

		TerminalControlFamily family = TerminalResponseFrameKinds.GetControlFamily(
			frame.Kind
		);
		for ( int index = 0; index < this.rules.Length; index++ ) {
			if ( this.families[ index ] != family ) {
				continue;
			}

			TerminalQueryResponseRule rule = this.rules[ index ];
			if ( !rule.Matcher.IsMatch( frame ) ) {
				break;
			}

			disposition = rule.Disposition;
			return true;
		}

		disposition = default;
		return false;
	}

	internal bool IsCorrelatedPrefix(
		TerminalControlFamily family,
		IReadOnlyList<byte> bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		TerminalQueryResponseRule rule = this.GetRule( family );
		return rule.Matcher is ICorrelatedTerminalResponseMatcher correlated
			&& correlated.IsCorrelatedPrefix( bytes );
	}
}
