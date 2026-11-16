namespace HelixEngine
{
    /// <summary>A range of whole numbers from start, length long.</summary>
    public struct RangeInt
    {
        public int start;
        public int length;

        public RangeInt(int start, int length)
        {
            this.start = start;
            this.length = length;
        }

        public int end => start + length;
    }
}
