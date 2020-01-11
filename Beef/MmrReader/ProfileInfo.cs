using System;

namespace Beef {
    public class ProfileInfo {
        public static String[] RegionIdMap = new String[] { "", "US", "EU", "KO", "", "CN" };

        // Information about the account to get the MMR for:
        public String RegionId { get; set; }       // Can be "US", "EU", "KO" or "CN"
        public int RealmId { get; set;  }           // Can be 1 or 2.  You get this from the link below ".../profile/<regionId>/<realmId>/...".
        public long ProfileId { get; set; }        // Example: https://starcraft2.com/en-us/profile/1/1/1986271, profile ID is 1986271

        public ProfileInfo(
                String regionId,
                int realmId,
                long profileId) {
            RegionId = regionId;
            RealmId = realmId;
            ProfileId = profileId;
        }

        public ProfileInfo(ProfileInfo other) {
            RegionId = other.RegionId;
            RealmId = other.RealmId;
            ProfileId = other.ProfileId;
        }

        public ProfileInfo() {
            // This constructor is for deserializing from JSON.
        }

        public String GetBattleNetAccountUrl() {
            return $"https://starcraft2.com/en-us/profile/{RegionStringToId(RegionId)}/{RealmId}/{ProfileId}";
        }

        /// <summary>
        /// Converts a region string to the integer identifier (IE "US" to 1, "EU" to 2, etc..)
        /// </summary>
        /// <param name="regionString">A region string to convert (See RegionIdMap for the options)</param>
        /// <returns>Returns the int matching the given region or -1 if it is not found.</returns>
        public static int RegionStringToId(String regionString) {
            for (int i = 0; i < RegionIdMap.Length; i++) {
                if (regionString == RegionIdMap[i])
                    return i;
            }

            return -1;
        }
    }
}
