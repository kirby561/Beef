using System.Collections.Generic;

namespace Beef.MmrReader {
    public interface LadderInfoProvider {
        List<LadderInfo> GetLadderUsers();
    }
}
