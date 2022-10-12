using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beef.SharedServices {
    /// <summary>
    /// Data class to keep track of the access token we were granted and when it
    /// expires.  This class is serialized to and from JSON directly.
    /// </summary>
    public class AccessTokenInfo {
        public String AccessToken { get; set; }     // The access token
        public long ExpirationTimeMs { get; set; }  // Unix timestamp when this token expires
    }
}
