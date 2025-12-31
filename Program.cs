using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

/*
 ======================================================================
  ULTRA FAST RANDOM NUMBER GENERATOR XOSHIRO256**
  - no allocations
  - no base classes
  - perfect for simulation / brute-force
 ======================================================================
*/

/// <summary>
/// Very fast pseudo-random number generator Xoshiro256**.
/// Uses 256-bit state (4 × ulong).
/// </summary>
struct Xoshiro256ss
{
    // Generator state (256 bits)
    private ulong s0, s1, s2, s3;

    /// <summary>
    /// Creates generator with given seed.
    /// Uses SplitMix64 to avoid weak start.
    /// </summary>
    public Xoshiro256ss(ulong seed)
    {
        var sm = new SplitMix64(seed);
        s0 = sm.Next();
        s1 = sm.Next();
        s2 = sm.Next();
        s3 = sm.Next();
    }

    /// <summary>
    /// Returns random 32-bit number.
    /// AggressiveInlining → max speed in loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Next32()
    {
        // Helper shift
        ulong t = s1 << 17;

        // Result based on state sum
        ulong result = s0 + s1 + s2;

        // Bit mixing (XOR)
        s3 ^= s0;
        s2 ^= s1;
        s1 ^= s3;
        s0 ^= t;

        // Bit rotation (better distribution)
        s0 = (s0 << 45) | (s0 >> (64 - 45));

        // Return upper 32 bits
        return (uint)(result >> 32);
    }

    /// <summary>
    /// Helper generator used ONLY to init state.
    /// Gives very good bit spread.
    /// </summary>
    private struct SplitMix64
    {
        private ulong state;

        public SplitMix64(ulong seed) => state = seed;

        public ulong Next()
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}

/*
 ======================================================================
  MAIN PROGRAM – LOTTO 6/49 SIMULATION
  - full multithreading
  - no GC inside loop
  - bit masks instead of array compare
 ======================================================================
*/

class Program
{
    /// <summary>Random winning numbers (generated once).</summary>
    static readonly int[] Winning = GetSortedSample(6);

    /// <summary>Bit mask of winning set.</summary>
    static readonly uint WinningMask = ComputeMask(Winning);

    /// <summary>Thread count = CPU cores.</summary>
    static readonly int ThreadCount = Environment.ProcessorCount;

    /// <summary>
    /// Number of draws per thread batch.
    /// Smaller value → more accurate counter.
    /// </summary>
    static readonly long BatchSize = 100_000;

    /// <summary>Global STOP flag for all threads.</summary>
    static volatile bool Found = false;

    /// <summary>Total number of draws.</summary>
    static long totalTries = 0;

    static void Main()
    {
        Console.WriteLine($"Searching: {string.Join(" ", Winning)} (mask: {WinningMask:x8})");
        Console.WriteLine($"Threads: {ThreadCount} | Batch: {BatchSize:N0}\n");

        var stopwatch = Stopwatch.StartNew();

        // Start worker threads
        var tasks = new Task[ThreadCount];
        for (int i = 0; i < ThreadCount; i++)
            tasks[i] = Task.Run(Worker);

        // Wait until one thread wins
        Task.WaitAny(tasks);

        // Stop others
        Found = true;
        Task.WaitAll(tasks);

        stopwatch.Stop();

        double seconds = stopwatch.Elapsed.TotalSeconds;
        long speed = (long)(totalTries / seconds);

        Console.WriteLine($"\nHit after {totalTries:N0} draws");
        Console.WriteLine($"Time: {seconds:F2} s → {speed:N0} draws/s");
    }

    /// <summary>
    /// Code executed by each thread.
    /// Draws Lotto sets until hit.
    /// </summary>
    static void Worker()
    {
        // Unique seed per thread
        var rng = new Xoshiro256ss(
            (ulong)Random.Shared.NextInt64() ^
            (ulong)Thread.CurrentThread.ManagedThreadId
        );

        long localCounter = 0;

        // Buffer for 6 numbers – stack only (no GC)
        Span<int> buffer = stackalloc int[6];

        while (!Found)
        {
            for (long i = 0; i < BatchSize; i++)
            {
                FillUniqueFast(ref rng, buffer);

                // Mask compare instead of array compare
                if (ComputeMask(buffer) == WinningMask)
                {
                    Interlocked.Add(ref totalTries, localCounter + i + 1);
                    Found = true;
                    return;
                }
            }

            Interlocked.Add(ref totalTries, BatchSize);
            localCounter += BatchSize;
        }

        Interlocked.Add(ref totalTries, localCounter);
    }

    /// <summary>
    /// Converts 6 numbers (1–49) into 32-bit bit mask.
    /// Order does not matter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint ComputeMask(Span<int> nums)
    {
        // Manual unroll for max speed
        return (1u << (nums[0] - 1)) |
               (1u << (nums[1] - 1)) |
               (1u << (nums[2] - 1)) |
               (1u << (nums[3] - 1)) |
               (1u << (nums[4] - 1)) |
               (1u << (nums[5] - 1));
    }

    /// <summary>
    /// Fast draw of 6 unique numbers from range 1–49.
    /// Method: partial Fisher–Yates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void FillUniqueFast(ref Xoshiro256ss rng, Span<int> result)
    {
        Span<int> pool = stackalloc int[49];

        // Pool init
        for (int i = 0; i < 49; i++)
            pool[i] = i + 1;

        // Draw only first 6 values
        for (int i = 0; i < 6; i++)
        {
            uint upper = (uint)(49 - i);
            uint r = rng.Next32() % upper;

            int idx = (int)r + i;

            result[i] = pool[idx];
            pool[idx] = pool[i];
        }
    }

    /// <summary>
    /// Draws one Lotto set (used to set target).
    /// </summary>
    static int[] GetSortedSample(int count)
    {
        var result = new int[count];
        Span<int> pool = stackalloc int[49];

        for (int i = 0; i < 49; i++)
            pool[i] = i + 1;

        var rnd = Random.Shared;

        for (int i = 0; i < count; i++)
        {
            int idx = rnd.Next(i, 49);
            result[i] = pool[idx];
            pool[idx] = pool[i];
        }

        Array.Sort(result);
        return result;
    }

    /// <summary>
    /// Mask version for array (used only at init).
    /// </summary>
    static uint ComputeMask(int[] nums)
    {
        uint mask = 0;
        foreach (int n in nums)
            mask |= 1u << (n - 1);
        return mask;
    }
}
