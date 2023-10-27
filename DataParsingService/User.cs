using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataParsingService
{
    public class User
    {
        public string userName;
        private string _email;
        [DefaultValue("not specified")]
        public string email
        {
            get
            {
                return _email;
            }
            set
            {
                _email = value;
            }
        }
    }
}
