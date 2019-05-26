using System;

namespace Beef {
    class BeefEntry : IComparable<BeefEntry> {
        public String ObjectId { get; set; }
        public String PlayerName { get; set; }
        public int PlayerRank { get; set; }

        public int CompareTo(BeefEntry other) {
            if (PlayerRank < other.PlayerRank)
                return -1;
            if (PlayerRank == other.PlayerRank)
                return 0;
            return 1;
        }
    }
}
