namespace Lumina.Data.Parsing.Tex
{
    // From DotSquish
    internal abstract class ColourFit
    {
        protected ColourSet _Colours;
        private SquishOptions _Flags;
        protected ColourFit(ColourSet colours, SquishOptions flags)
        {
            this._Colours = colours;
            this._Flags = flags;
        }

        public void Compress(ref byte[] block)
        {
            if (this._Flags.HasFlag(SquishOptions.DXT1))
            {
                Compress3(block);
                if (!this._Colours.IsTransparent)
                    Compress4(block);
            }
            else
                Compress4(block);
        }

        protected abstract void Compress3(byte[] block);
        protected abstract void Compress4(byte[] block);
    }
}
