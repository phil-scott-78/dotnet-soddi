using MonoTorrent;

namespace Soddi.ProgressBar
{
    internal readonly struct BitSmuggler
    {
        public BitSmuggler(BitField bits)
        {
            Bits = bits;
        }

        public BitField Bits { get; }
    }
}
