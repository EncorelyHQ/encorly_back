using EncorelyApplication.Interfaces;
using EncorelyModels;

namespace EncorelyApplication.Services;

public class CompatibilityService : ICompatibilityService
{
    private const double COMPATIBILITY_THRESHOLD = 0.70;

    public double CalculateAffinity(MusicalProfile profileA, MusicalProfile profileB)
    {
        // Phase 1: Evolve to Cosine Similarity
        double dotProduct = (profileA.Energy * profileB.Energy) +
                            (profileA.Danceability * profileB.Danceability) +
                            (profileA.Valence * profileB.Valence);

        double magnitudeA = Math.Sqrt(Math.Pow(profileA.Energy, 2) + Math.Pow(profileA.Danceability, 2) + Math.Pow(profileA.Valence, 2));
        double magnitudeB = Math.Sqrt(Math.Pow(profileB.Energy, 2) + Math.Pow(profileB.Danceability, 2) + Math.Pow(profileB.Valence, 2));

        if (magnitudeA == 0 || magnitudeB == 0) return 0.0;

        double cosineSimilarity = dotProduct / (magnitudeA * magnitudeB);
        
        return Math.Round(cosineSimilarity, 2);
    }

    public bool IsCompatible(double affinityPercentage)
    {
        return affinityPercentage >= COMPATIBILITY_THRESHOLD;
    }

    public bool IsHighPriority(double affinityPercentage)
    {
        return affinityPercentage >= 0.85; // 85% Affinity = Priority
    }
}
