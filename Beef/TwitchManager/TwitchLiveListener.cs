using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beef.TwitchManager {
    /// <summary>
    /// Receives notifications when a stream goes live.
    /// </summary>
    public interface TwitchLiveListener {
        /// <summary>
        /// Called when a stream goes live.
        /// Note this is invoked on the service thread, not on the main thread.
        /// </summary>
        /// <param name="twitchName">The name of the channel that went live.</param>
        /// <param name="goLiveMessage">The message configured to send when they go live.</param>
        void OnTwitchStreamLive(String twitchName, String goLiveMessage);
    }
}
