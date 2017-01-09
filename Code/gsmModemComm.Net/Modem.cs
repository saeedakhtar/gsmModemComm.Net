using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace gsmModemComm.Net
{
    public class Modem
    {
        private string m_PortName;
        private int m_BaudRate;
        private int m_DataBits;
        private int m_ReadTimeout;
        private int m_WriteTimeout;

        private AutoResetEvent receiveNow;

        public Modem(string p_PortName)
        {
            m_PortName = p_PortName;
            m_BaudRate = 9600;
            m_DataBits = 8;
            m_ReadTimeout = 300;
            m_WriteTimeout = 300;
        }

        /// <summary>
        /// Read all messages available on SIM
        /// </summary>
        /// <returns>List of SMS, null if no message available on SIM</returns>
        public List<SMS> ReadAllMessages()
        {
            SerialPort port = null;
            try
            {
                port = OpenPort();
                if (port != null)
                {
                    int count = CountSMSmessages(port);
                    if (count > 0)
                    {

                        #region Command
                        string strCommand = "AT+CMGL=\"ALL\"";

                        //if (this.rbReadAll.Checked)
                        //{
                        //    strCommand = "AT+CMGL=\"ALL\"";
                        //}
                        //else if (this.rbReadUnRead.Checked)
                        //{
                        //    strCommand = "AT+CMGL=\"REC UNREAD\"";
                        //}
                        //else if (this.rbReadStoreSent.Checked)
                        //{
                        //    strCommand = "AT+CMGL=\"STO SENT\"";
                        //}
                        //else if (this.rbReadStoreUnSent.Checked)
                        //{
                        //    strCommand = "AT+CMGL=\"STO UNSENT\"";
                        //}
                        #endregion

                        // If SMS exist then read SMS
                        #region Read SMS
                        //.............................................. Read all SMS ....................................................
                        List<SMS> messages = ReadSMS(port, strCommand);

                        #endregion
                        ClosePort(port);
                        return messages;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                    throw new Exception("Failed to open port");
            }
            catch(Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (port != null && port.IsOpen)
                    ClosePort(port);
            }
        }

        /// <summary>
        /// Open serial port for communication
        /// </summary>
        /// <returns>Serial port object</returns>
        private SerialPort OpenPort()
        {
            receiveNow = new AutoResetEvent(false);
            SerialPort port = new SerialPort();

            try
            {
                port.PortName = m_PortName;                 //COM1
                port.BaudRate = m_BaudRate;                   //9600
                port.DataBits = m_DataBits;                   //8
                port.StopBits = StopBits.One;                  //1
                port.Parity = Parity.None;                     //None
                port.ReadTimeout = m_ReadTimeout;             //300
                port.WriteTimeout = m_WriteTimeout;           //300
                port.Encoding = Encoding.GetEncoding("iso-8859-1");
                port.DataReceived += new SerialDataReceivedEventHandler
                        (port_DataReceived);
                port.Open();
                port.DtrEnable = true;
                port.RtsEnable = true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return port;
        }

        /// <summary>
        /// Receive data from port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (e.EventType == SerialData.Chars)
                {
                    receiveNow.Set();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Execute AT command on serial port
        /// </summary>
        /// <param name="port">Serial port object</param>
        /// <param name="command">AT command to execute</param>
        /// <param name="responseTimeout">Timeout in seconds</param>
        /// <param name="errorMessage">Defualt error message</param>
        /// <returns>Modem response string</returns>
        private string ExecCommand(SerialPort port, string command, int responseTimeout, string errorMessage)
        {
            try
            {

                port.DiscardOutBuffer();
                port.DiscardInBuffer();
                receiveNow.Reset();
                port.Write(command + "\r");

                string input = ReadResponse(port, responseTimeout);
                if ((input.Length == 0) || ((!input.EndsWith("\r\n> ")) && (!input.EndsWith("\r\nOK\r\n"))))
                    throw new ApplicationException("No success message was received.");
                return input;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Read list of SMS available on SIM
        /// </summary>
        /// <param name="port">Serial Port object</param>
        /// <param name="p_strCommand">AT command to read SMS</param>
        /// <returns>List of SMS read</returns>
        private List<SMS> ReadSMS(SerialPort port, string p_strCommand)
        {

            // Set up the phone and read the messages
            List<SMS> messages = null;
            try
            {

                #region Execute Command
                // Check connection
                ExecCommand(port, "AT", 300, "No phone connected");
                // Use message format "Text mode"
                ExecCommand(port, "AT+CMGF=1", 300, "Failed to set message format.");
                // Use character set "PCCP437"
                //ExecCommand(port,"AT+CSCS=\"PCCP437\"", 300, "Failed to set character set.");
                ExecCommand(port, "AT+CSCS=\"HEX\"", 300, "Failed to set character set.");
                // Select SIM storage
                ExecCommand(port, "AT+CPMS=\"SM\"", 300, "Failed to select message storage.");
                // Read the messages
                string input = ExecCommand(port, p_strCommand, 5000, "Failed to read the messages.");
                #endregion

                #region Parse messages
                messages = ParseMessages(input);
                #endregion

            }
            catch (Exception ex)
            {
                throw ex;
            }

            if (messages != null)
                return messages;
            else
                return null;

        }

        /// <summary>
        /// Parse SMSes to List of SMS Objects
        /// </summary>
        /// <param name="input">SMS stream</param>
        /// <returns></returns>
        private List<SMS> ParseMessages(string input)
        {
            List<SMS> messages = new List<SMS>();
            try
            {
                Regex r = new Regex(@"\+CMGL: (\d+),""(.+)"",""(.+)"",(.*),""(.+)""\r\n(.+)\r\n");
                Match m = r.Match(input);
                while (m.Success)
                {
                    SMS msg = new SMS();
                    //msg.Index = int.Parse(m.Groups[1].Value);
                    msg.Index = m.Groups[1].Value;
                    msg.Status = m.Groups[2].Value;
                    msg.Sender = m.Groups[3].Value;
                    msg.Alphabet = m.Groups[4].Value;
                    msg.Sent = m.Groups[5].Value;
                    msg.Message = m.Groups[6].Value;
                    messages.Add(msg);

                    m = m.NextMatch();
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
            return messages;
        }

        /// <summary>
        /// Read response on serial port after AT command
        /// </summary>
        /// <param name="port">Serial port object</param>
        /// <param name="timeout">Timeout value in seconds</param>
        /// <returns>Response string</returns>
        private string ReadResponse(SerialPort port, int timeout)
        {
            string buffer = string.Empty;
            try
            {
                do
                {
                    if (receiveNow.WaitOne(timeout, false))
                    {
                        string t = port.ReadExisting();
                        buffer += t;
                    }
                    else
                    {
                        if (buffer.Length > 0)
                            throw new ApplicationException("Response received is incomplete.");
                        else
                            throw new ApplicationException("No data received from phone.");
                    }
                }
                while (!buffer.EndsWith("\r\nOK\r\n") && !buffer.EndsWith("\r\n> ") && !buffer.EndsWith("\r\nERROR\r\n"));
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return buffer;
        }

        /// <summary>
        /// Read total number of messages available on SIM
        /// </summary>
        /// <param name="port">Serial port object</param>
        /// <returns>SMS count</returns>
        private int CountSMSmessages(SerialPort port)
        {
            int CountTotalMessages = 0;
            try
            {

                #region Execute Command

                string recievedData = ExecCommand(port, "AT", 300, "No phone connected at ");
                recievedData = ExecCommand(port, "AT+CMGF=1", 300, "Failed to set message format.");
                String command = "AT+CPMS?";
                recievedData = ExecCommand(port, command, 1000, "Failed to count SMS message");
                int uReceivedDataLength = recievedData.Length;

                #endregion

                #region If command is executed successfully
                if ((recievedData.Length >= 45) && (recievedData.StartsWith("\r\n+CPMS:")))
                {

                    #region Parsing SMS
                    string[] strSplit = recievedData.Split(',');
                    string strMessageStorageArea1 = strSplit[0];     //SM
                    string strMessageExist1 = strSplit[1];           //Msgs exist in SM
                    #endregion

                    #region Count Total Number of SMS In SIM
                    CountTotalMessages = Convert.ToInt32(strMessageExist1);
                    #endregion

                }
                #endregion

                #region If command is not executed successfully
                else if (recievedData.Contains("ERROR"))
                {

                    #region Error in Counting total number of SMS
                    string recievedError = recievedData;
                    recievedError = recievedError.Trim();
                    recievedData = "Following error occured while counting the message" + recievedError;
                    #endregion

                }
                #endregion

                return CountTotalMessages;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Close serial port
        /// </summary>
        /// <param name="port">Serial Port object</param>
        private void ClosePort(SerialPort port)
        {
            try
            {
                port.Close();
                port.DataReceived -= new SerialDataReceivedEventHandler(port_DataReceived);
                port = null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Delete all messages on SIM
        /// </summary>
        /// <returns>true if deletion successful</returns>
        public bool DeleteAllMessages()
        {
            bool result = false;
            string strCommand = "AT+CMGD=1,4";
            if (DeleteMsg(strCommand))
            {
                result = true;
            }
            else
            {
                result = false;
            }
            return result;
        }

        /// <summary>
        /// Delete read messages on SIM
        /// </summary>
        /// <returns>true if deletion successful</returns>
        public bool DeleteReadMessages()
        {
            bool result = false;
            string strCommand = "AT+CMGD=1,3";
            if (DeleteMsg(strCommand))
            {
                result = true;
            }
            else
            {
                result = false;
            }
            return result;

        }

        /// <summary>
        /// Execute delete message command
        /// </summary>
        /// <param name="p_strCommand">AT command to delete messages</param>
        /// <returns>true if deletion successful</returns>
        private bool DeleteMsg(string p_strCommand)
        {
            bool isDeleted = false;
            SerialPort port = null;
            try
            {
                //Open port
                port = OpenPort();
                if (port != null)
                {

                    #region Execute Command
                    string recievedData = ExecCommand(port, "AT", 300, "No phone connected");
                    recievedData = ExecCommand(port, "AT+CMGF=1", 300, "Failed to set message format.");
                    String command = p_strCommand;
                    recievedData = ExecCommand(port, command, 300, "Failed to delete message");
                    #endregion

                    if (recievedData.EndsWith("\r\nOK\r\n"))
                    {
                        isDeleted = true;
                    }
                    if (recievedData.Contains("ERROR"))
                    {
                        isDeleted = false;
                    }
                    return isDeleted;
                }
                else
                    throw new Exception("Failed to open port");
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (port != null && port.IsOpen)
                    ClosePort(port);
            }
        }
    }
}
