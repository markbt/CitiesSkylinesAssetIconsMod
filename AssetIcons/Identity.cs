using System;

using ICities;

namespace AssetIcons
{
    public class Identity : IUserMod
    {
        public string Name
        {
            get { return "AssetIcons"; }
        }

        public string Description
        {
            get { return "Adds icons to assets based on their thumbnails."; }
        }
    }
}

