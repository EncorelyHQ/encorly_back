using EncorelyApplication.Interfaces;
using EncorelyDomain.Entities;

namespace EncorelyApplication.Services;

public class CompatibilityService : ICompatibilityService
{
    private const double COMPATIBILITY_THRESHOLD = 0.70;

    public double CalculateAffinity(MusicalProfile profileA, MusicalProfile profileB)
    {
        // Tarea: Implementar algoritmo de similitud musical (Euclidean Distance en 3D/4D)
        // Atributos: Energy, Danceability, Valence
        
        double sumSquares = Math.Pow(profileA.Energy - profileB.Energy, 2) +
                           Math.Pow(profileA.Danceability - profileB.Danceability, 2) +
                           Math.Pow(profileA.Valence - profileB.Valence, 2);
        
        double distance = Math.Sqrt(sumSquares);
        
        // La distancia máxima en un espacio 3D [0,1] es sqrt(3) ≈ 1.732
        // Normalizamos la afinidad: 1.0 (identidad) a 0.0 (opuestos)
        double maxDistance = Math.Sqrt(3);
        double affinity = 1.0 - (distance / maxDistance);

        return Math.Round(affinity, 2);
    }

    public bool IsCompatible(double affinityPercentage)
    {
        return affinityPercentage >= COMPATIBILITY_THRESHOLD;
    }
}
