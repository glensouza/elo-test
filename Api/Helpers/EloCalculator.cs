using System;

namespace Api.Helpers;

public static class EloCalculator
{
    private const int KFactor = 32;

    public static (double, double) CalculateElo(double winnerRating, double loserRating)
    {
        // Calculate the expected scores for each picture
        double winnerExpectedScore = 1 / (1 + Math.Pow(10, (loserRating - winnerRating) / 400));
        double loserExpectedScore = 1 / (1 + Math.Pow(10, (winnerRating - loserRating) / 400));

        // Update the ratings for each picture
        double winnerScore = KFactor * (1 - winnerExpectedScore);
        double loserScore = KFactor * (0 - loserExpectedScore);

        return (winnerScore, loserScore);
    }
}
