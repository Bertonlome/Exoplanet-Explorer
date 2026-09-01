using System;
using Godot;

/// <summary>
/// Player-facing language boundary for checkpoint 4.3. Reconstructed structures remain
/// candidates until the game's independent puzzle-solved predicate succeeds; the Rover never
/// turns a measurement or player review action into a correctness claim.
/// </summary>
public static class FragmentCandidateValidityPolicy
{
	public const string PlayerReviewRequired = "PLAYER REVIEW REQUIRED";

	private static readonly string[] ForbiddenConclusions =
	{
		"true signal",
		"correct candidate",
		"valid candidate",
		"solution found",
		"glyph identified",
		"correct glyph",
		"puzzle solved"
	};

	public static string DescribeStructureDisposition(FragmentAnnotationDisposition disposition)
	{
		return disposition switch
		{
			FragmentAnnotationDisposition.Accepted => "PLAYER RETAINED",
			FragmentAnnotationDisposition.Dismissed => "PLAYER DISMISSED",
			_ => "CANDIDATE"
		};
	}

	public static string DescribePlayerStructureAction(FragmentStructureEditAction action)
	{
		return action switch
		{
			FragmentStructureEditAction.Accept => "Player retained candidate structure for review",
			FragmentStructureEditAction.Dismiss => "Player dismissed candidate structure",
			FragmentStructureEditAction.Restore => "Player restored candidate structure",
			_ => "Player reviewed candidate structure"
		};
	}

	public static string GuardPlayerFacingCopy(string text)
	{
		if (string.IsNullOrEmpty(text)) return text;
		foreach (string forbidden in ForbiddenConclusions)
		{
			if (text.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) < 0) continue;
			GD.PushError(
				$"Blocked forbidden Rover candidate-validity conclusion: '{forbidden}'.");
			return PlayerReviewRequired;
		}
		return text;
	}
}
