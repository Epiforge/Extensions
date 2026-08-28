namespace Epiforge.Extensions.Benchmarking;

static class Mixer
{
    public static uint Mix(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x85ebca6b;
            value ^= value >> 13;
            value *= 0xc2b2ae35;
            value ^= value >> 16;
            return value;
        }
    }
}
