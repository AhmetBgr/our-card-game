/// <summary>
/// The Forged in Battle run's own random stream: every roll the run makes (hero options, milestones,
/// enemies, card and perk options) comes from here rather than UnityEngine.Random, so a run is fully
/// determined by its seed and the player's choices, and its state is a single number that saves with
/// the run. Xorshift32: tiny, fast and good enough for shuffles and picks.
/// </summary>
public class RunRandom
{
    public uint State { get; private set; }

    public RunRandom(uint seed) => State = seed == 0 ? 0x9E3779B9u : seed;

    /// <summary>A seed for a fresh run, from the clock and a Guid so two runs started together still differ.</summary>
    public static uint NewSeed()
    {
        unchecked
        {
            uint seed = (uint)System.Guid.NewGuid().GetHashCode() ^ (uint)System.DateTime.UtcNow.Ticks;
            return seed == 0 ? 1u : seed;
        }
    }

    public static RunRandom FromState(uint state) => new RunRandom(state);

    public uint NextUInt()
    {
        uint x = State;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        State = x;
        return x;
    }

    /// <summary>An int in [<paramref name="min"/>, <paramref name="max"/>), like UnityEngine.Random.Range.</summary>
    public int Range(int min, int max)
    {
        if (max <= min) return min;
        return min + (int)(NextUInt() % (uint)(max - min));
    }

    /// <summary>Mixes a seed with salts into a new seed, for derived streams such as a battle's deck shuffle.</summary>
    public static int Derive(uint seed, int a, int b)
    {
        unchecked
        {
            uint h = seed ^ 0x85EBCA6Bu;
            h = (h ^ (uint)a) * 0x27D4EB2Du;
            h ^= h >> 15;
            h = (h ^ (uint)b) * 0x165667B1u;
            h ^= h >> 13;
            return (int)h;
        }
    }
}
