using System;

namespace Beef {
    public class ProfileInfo {
        // Information about the account to get the MMR for:
        public String RegionId { get; }       // Can be "US", "EU", "KO" or "CN"
        public int RealmId { get; }           // Can be 1 or 2.  You get this from the link below ".../profile/<regionId>/<realmId>/...".
        public long ProfileId { get; }        // Example: https://starcraft2.com/en-us/profile/1/1/1986271, profile ID is 1986271

        public ProfileInfo(
                String regionId,
                int realmId,
                long profileId) {
            RegionId = regionId;
            RealmId = realmId;
            ProfileId = profileId;
        }
    }
}
