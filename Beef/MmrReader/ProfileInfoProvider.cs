using System.Collections.Generic;

namespace Beef.MmrReader {
    public interface ProfileInfoProvider {
        List<ProfileInfo> GetLadderUsers();
    }
}
