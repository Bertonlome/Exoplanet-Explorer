public static class FragmentSampleRover
{
    public static FragmentAnalysisProposal ProposeAnalysis(
        FragmentSampleAvailability sample)
    {
        if (sample == null ||
            sample.Status == FragmentSampleAnalysisStatus.Analysing)
        {
            return null;
        }

        string reason = sample.Status switch
        {
            FragmentSampleAnalysisStatus.Available => "Unanalysed sample is in range.",
			FragmentSampleAnalysisStatus.Completed =>
				"Completed fragment analysis is available for review.",
            FragmentSampleAnalysisStatus.Solved => "Solved sample is available for review.",
            _ => "Previously analysed sample is available for review."
        };

        return new FragmentAnalysisProposal
        {
            Position = sample.Position,
            Status = sample.Status,
            Activity = FragmentRoverActivity.WaitingForPlayer,
            Reason = reason
        };
    }
}
