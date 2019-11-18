using System;

namespace Beef {
    public class ProfileInfo {
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
    }
}
