using System;

namespace Beef {
    public class LadderInfo {
        // Information about the account to get the MMR for:
        public String RegionId { get; }       // Can be "US", "EU", "KO" or "CN"
        public int RealmId { get; }           // Can be 1 or 2.  You get this from the link below ".../profile/<regionId>/<realmId>/...".
        public long ProfileId { get; }        // Example: https://starcraft2.com/en-us/profile/1/1/1986271/ladders?ladderId=274006, profile ID is 1986271
        public long LadderId { get; }         // In the above link, the LadderId is 274006

        public LadderInfo(
                String regionId,
                int realmId,
                long profileId,
                long ladderId) {
            RegionId = regionId;
            RealmId = realmId;
            ProfileId = profileId;
            LadderId = ladderId;
        }
    }
}
