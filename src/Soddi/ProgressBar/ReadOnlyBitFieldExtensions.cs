using MonoTorrent;

namespace Soddi.ProgressBar;

internal static class ReadOnlyBitFieldExtensions
{
    public static bool[] ToBoolArray(this ReadOnlyBitField bits)
    {
        var boolArray = new bool[bits.Length];
        for (var i = 0; i < bits.Length; i++)
        {
            boolArray[i] = bits[i];
        }

        return boolArray;
    } 
}
