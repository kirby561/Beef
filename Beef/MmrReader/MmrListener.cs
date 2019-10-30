using System;
using System.Collections.Generic;

namespace Beef.MmrReader {
    public interface MmrListener {
        void OnMmrRead(List<Tuple<LadderInfo, String>> mmrList);
    }
}
