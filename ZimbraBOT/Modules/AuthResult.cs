using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ZimbraBOT.Modules
{
    [Serializable]
    public class AuthResult
    {
        public string AccessToken { get; set; }

        public string UserName { get; set; }

        public string Email { get; set; }

    }
}