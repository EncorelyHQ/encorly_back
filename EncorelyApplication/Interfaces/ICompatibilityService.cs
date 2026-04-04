using EncorelyDomain.Entities;

namespace EncorelyApplication.Interfaces;

public interface ICompatibilityService
{
    double CalculateAffinity(MusicalProfile profileA, MusicalProfile profileB);
    bool IsCompatible(double affinityPercentage);
}
