namespace MobaTrainer.Gameplay
{
    /// <summary>Live numbers the HUD shows above the ability bar (hits, misses, run time).</summary>
    public interface IRunCounters
    {
        int Hits { get; }
        int Misses { get; }
        float Elapsed { get; }
    }
}
