using Terraria;

namespace RecipeBrowser
{
    public class Utils
    {
        public static int GetRequiredTileStyle(int tileID, bool isCrimson)
        {
            if (tileID == 26)
            {
                if (!isCrimson)
                    return 0;
                return 1;
            }
            return 0;
        }
    }
}
