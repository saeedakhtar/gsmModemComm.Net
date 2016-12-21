using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsmModemComm.Net
{
    public class SMS
    {

        #region Private Variables
        private string index;
        private string status;
        private string sender;
        private string alphabet;
        private string sent;
        private string message;
        #endregion

        #region Public Properties
        public string Index
        {
            get { return index; }
            set { index = value; }
        }
        public string Status
        {
            get { return GetString(status); }
            set { status = value; }
        }
        public string Sender
        {
            get { return GetString(sender); }
            set { sender = value; }
        }
        public string Alphabet
        {
            get { return GetString(alphabet); }
            set { alphabet = value; }
        }
        public string Sent
        {
            get { return GetString(sent); }
            set { sent = value; }
        }
        public string Message
        {
            get { return GetString(message); }
            set { message = value; }
        }
        #endregion

        private string GetString(string hex)
        {
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return Encoding.ASCII.GetString(raw); // GatewayServer
        }

    }
}
