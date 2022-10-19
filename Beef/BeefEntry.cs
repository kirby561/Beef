using System;

namespace Beef {
    public class BeefEntry : IComparable<BeefEntry> {
        public String PlayerName { get; set; }
        public int PlayerRank { get; set; }

        public int CompareTo(BeefEntry other) {
            if (PlayerRank < other.PlayerRank)
                return -1;
            if (PlayerRank == other.PlayerRank)
                return 0;
            return 1;
        }

        public BeefEntry() {
            // Nothing to do
        }

        public BeefEntry(BeefEntry other) {
            PlayerName = other.PlayerName;
            PlayerRank = other.PlayerRank;
        }
    }
}
