using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.IO.Ports;
//using System.Text;

/* Using code posted on Netduino forms for a template and guidance located in creating a serial port helper class:
 * http://forums.netduino.com/index.php?/topic/366-netduino-serial-port-code-review/
 * 
 * Note: This serial port class is intended to be used with GPS NMEA sentences. At present the code uses the null character to indicate the 
 * end of a "packet" which conflicts with the NULL charactrers used in the NMEA sentences to indicate no data... I will have to choose a different
 * character or sets of characters to indicate the end of a packet...
 */

namespace SerialPort_Class
{

    #region Class Delegates

    /* PacketReadyEventHandler() delegate and an event handler to broadcast that the buffer contains a "full" line (\r\n) 
    * in the DataPacketBuffer
    * 
    * COMProcessBufferContentsEventHandler(object sender) delegate to call a method to process the recieved bytes into a ready to go data packet
    * 
    * Reference on how to use delegates:
    * http://www.switchonthecode.com/tutorials/csharp-snippet-tutorial-custom-event-handlers &
    * http://ondotnet.com/pub/a/dotnet/2002/04/15/events.html
    */

    public delegate void SerialPacketReadyEventHandler();
    public delegate void ProcessBufferEventHandler(int sender);

    #endregion Delegates

    #region Class Constants

    public class Constants
    {
        /* Max buffer bytes n, byte at position n-1 (zero referenced) is for null
         * Max count leaves room for null at end of packet
         * Packet for packet to be sent or PacketBuffer, TempBuffer for temporary buffers used to
         * hold data while being processed. May be able to condense this...
         */ 
        public const int iPacketMaxBytes = 256;
        public const int iPacketMaxCount = 255;
        public const int iPacketBufferMaxBytes = 256;
        public const int iPacketBufferMaxCount = 255;
        public const int iTransmitBufferMaxBytes = 256;
        public const int iTransmitBufferMaxCount = 255;
    }

    #endregion Class Constants

    #region Class Members and Constructors

    public class SerialPortBroker
    {

        /* create a SerialPort object called oSerialPort, was static but I think that caused issues
         * with the EventHandler stuff because each instance shared those static attributes, ie the
         * DataReceived EventHandler
         */ 
        SerialPort oSerialPort;

        //*create an event handler to broadcast that the buffer contains a "full" line (\n) in the ReceiveBuffer
        public event SerialPacketReadyEventHandler SerialPacketReady;
        private event ProcessBufferEventHandler ProcessBuffer;

        /* Create a thread to listen for data on the serial port, DataReceive causes COM2 to block COM1 for some reason
         * Not being used, DataReceived EventhHandler is
         */
        //private Thread mySerialListenThread;
        
        //public values for this class
        private byte[] baPacketBuffer = new Byte[Constants.iPacketBufferMaxBytes];
        public byte[] baPreparedPacket = new Byte[Constants.iPacketMaxBytes];
        private int iPreparedPacketOffset = 0;
         //*/

        //oSerialPort Configuration Method
        public SerialPortBroker(string sPortName, int iBaudRate, Parity parity, int iDataBits, StopBits stopBits, int iTimeOutms)
        {
            oSerialPort = new SerialPort(sPortName, iBaudRate, parity, iDataBits, stopBits);
            oSerialPort.ReadTimeout = iTimeOutms;

            /* Open the ports, must be done before creating DataReceived EventHandler or else the EventHandler
             * may fail. See http://blogs.msdn.com/b/bclteam/archive/2006/05/15/596656.aspx
             */
            oSerialPort.Open();
            Debug.Print(oSerialPort.PortName + " Open");

            /* Subscribe to the SerialPort DataReceived, and to the SerialPortBroker COMProcessBuffContents events
             * Moved DataReceived EventHandler assignment to startListeningToPort method to fix issue where
             * EventHandlers will not trigger unless the port is open before hand... Doing this gaurantees
             * See http://blogs.msdn.com/b/bclteam/archive/2006/05/15/596656.aspx
             */ 
            //oSerialPort.DataReceived += new SerialDataReceivedEventHandler(oSerialPort_DataReceivedHandler);
            ProcessBuffer += new ProcessBufferEventHandler(SerialPortBroker_ProcessBufferHandler);

            /* Point the thread to listen for incomming data using the serialListenThread method
             * This is not being used, DataReceived EventHandler is...
             */
            //this.mySerialListenThread = new Thread(new ThreadStart(serialListenThread));

        }

    #endregion Class Members and Constructors


    #region Event Handler Methods

        /*Event handler to copy data from SerialPort to the byte buffer, example found here:
         * http://wiki.microframework.nl/index.php/Serial_Port/RS232
         * 
         */
        #region DataReceived Handler
        void oSerialPort_DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            //Debug.Print(oSerialPort.PortName + " EventHandler Triggered!");
            //make sure there are bytes to read
            if (oSerialPort.BytesToRead != 0)
            {

                //create a temp buffer to handle this event
                //Debug.Print(oSerialPort.PortName + " BTRafE: " + oSerialPort.BytesToRead);
                byte[] baDataRcvWorkingBufferTemp = new byte[oSerialPort.BytesToRead];
                oSerialPort.Read(baDataRcvWorkingBufferTemp, 0, baDataRcvWorkingBufferTemp.Length);
                
                int iX = 0;

                //Copy what is in the WorkingBuffer to the PacketBuffer
                lock (baPacketBuffer)
                {
                    if (baDataRcvWorkingBufferTemp.Length > baPacketBuffer.Length)
                    {
                        /* Debug output the error for now, have to figure out what is needed to be done
                         * and whiping out in buffer
                         */
                        Debug.Print(oSerialPort.PortName + " RXPcktBffOvrflw!");
                        oSerialPort.DiscardInBuffer();

                    }
                    else
                    {
                        for (; iX < baDataRcvWorkingBufferTemp.Length; iX++)
                        {
                            baPacketBuffer[iX] = baDataRcvWorkingBufferTemp[iX];
                        }
                        //throw an event that says look at the buffered data and see if it is ready to go... send it the SerialPort object?
                        //Debug.Print("Calling ProcessBuffer");
                        if (this.ProcessBuffer != null)
                            this.ProcessBuffer(iX);
                        //Debug.Print("Out of ProcessBuffer");
                    }
                }

                
            }
            //Debug.Print("NoBytesRcvdafE");
        }
        #endregion DataReceived Handler

        /* DataReceive Thread as an alternative to the DataReceived Event Handler
         * Commented out to try some fixes using the Event Handler as the prefered
         * method.
         */    
        #region DataReceive Thread -- commented out
        /*private void serialListenThread()
        {
            Debug.Print("Thread Running for " + oSerialPort.PortName);
            while (true)
            {
                //Debug.Print("BTRbeE: " + oSerialPort.BytesToRead);
                //make sure there are bytes to read
                if (oSerialPort.BytesToRead != 0)
                {

                    //create a temp buffer to handle this event, I believe this will copy everything
                    //that is in the frameworks buffer
                    Debug.Print(oSerialPort.PortName + " BTRafE: " + oSerialPort.BytesToRead);
                    byte[] baDataRcvWorkingBufferTemp = new byte[oSerialPort.BytesToRead];
                    oSerialPort.Read(baDataRcvWorkingBufferTemp, 0, baDataRcvWorkingBufferTemp.Length);

                    int iX = 0;

                    //Copy what is in the WorkingBuffer to the PacketBuffer
                    lock (baPacketBuffer)
                    {
                        for (; iX < baDataRcvWorkingBufferTemp.Length; iX++)
                        {
                            baPacketBuffer[iX] = baDataRcvWorkingBufferTemp[iX];
                        }
                    }
                    //throw an event that says look at the buffered data and see if it is ready to go... send it the SerialPort object?
                    //Debug.Print("Calling ProcessBuffer");
                    if (this.ProcessBuffer != null)
                        this.ProcessBuffer(iX);
                    //Debug.Print("Out of ProcessBuffer");

                }
                //Debug.Print("NoBytesRcvdafE");
            }

        }*/
        #endregion DataReceive Thread


        /* Code below processes the recently received bytes into a "packet" with a \0 as the end terminator
         * Basically each GPS NMEA sentence is a packet...
         */ 
        #region ProcessBuffer Handler
        void SerialPortBroker_ProcessBufferHandler(int sender)
        {

            //Debug.Print("Processing...");
            byte[] baProcessWorkingBufferTemp = new byte[sender];

            //transfer contents of PacketBuffer to WorkingBuffer
            lock (baPacketBuffer)
            {
                for (int iX = 0; iX < baProcessWorkingBufferTemp.Length; iX++)
                {
                    baProcessWorkingBufferTemp[iX] = baPacketBuffer[iX];
                }
            }


           /* The code below attempts to form a packet from the (in theory) incomplete messages comming from
            * the DataReceived event. It does not look at the beginning of the byte array but looks for a 
            * '\n' char to indicate a "complete" burst of data. At the momment this is geared toward GPS NMEA streams. Would
            * have to spend time to make it work on any BoD or EoD indicator. Also note that PreparedPacket buffer is not locked
            * but should only be read by another object if the event is triggered. May have to prevent access through the classes methods
            * to prevent stuff getting weird should someone choose to directly read the PreparedPacketBuffer... Make a GET?
            * 
            */
            #region PacketForm Code
            //Only go through loop for however many bytes are in the WorkingBuffer to be copied into the PreparedPacket buffer
            for (int iX = 0; iX < baProcessWorkingBufferTemp.Length; iX++)
            {
                /* if PacketOffset is outside the buffers max count size (currently n-1) 
                 * send the packet regardless of what is in it but append the null at the end
                 * and reset the index counter (which is one step ahead of where data should 
                 * be
                 */
                if (iPreparedPacketOffset == Constants.iPacketMaxCount)
                {
                    //Debug.Print("StpIn to SndPkt frm full");

                    //append null 
                    baPreparedPacket[iPreparedPacketOffset] = 0x00;

                    //broadcast that the packet is ready
                    if (this.SerialPacketReady != null)
                        this.SerialPacketReady();

                    //Debug.Print("StpOut of SndPkt frm full");
                    //reset baPreparedPacket
                    iPreparedPacketOffset = 0;
                }

                //assuming that there are no nulls since the DataReceived event should take care of this
                baPreparedPacket[iPreparedPacketOffset] = baProcessWorkingBufferTemp[iX];

                //increment PreparedPacket offset if it is not outside of the buffers max count size
                if (iPreparedPacketOffset < Constants.iPacketMaxCount)
                {
                    //Debug.Print("PckOff: " + iPreparedPacketBuffOffset);
                    iPreparedPacketOffset++;
                }

                //Debug.Print("incr++");

                /* look to see if the newly entered byte in PreparedPacket is a '\n'
                 * if it is, we have a "full" packet and will broadcast that a PacketIsReady
                 * Have to look one byte behind PacketOffset because it was incremented above
                 * 
                 */
                if (baPreparedPacket[iPreparedPacketOffset - 1] == '\n')
                {

                    //append null 
                    baPreparedPacket[iPreparedPacketOffset] = 0x00;

                    //Debug.Print("StpIn to SndPkt");

                    //broadcast it is ready
                    if (this.SerialPacketReady != null)
                        this.SerialPacketReady();

                    //Debug.Print("StpOut of SndPkt");
                    //reset baPreparedPacket
                    iPreparedPacketOffset = 0;
                    
                }

                //Debug.Print("EL not found");

            }//*/
            #endregion PacketForm Code


            /* The code below should produce a packet that is "ready" when the PreparedPacket buffer is full
             * Note, if the datastream stops and the packet is not filled the packet will never be ready.
             * Would rather base packet ready off of new line but have some more thinking to do for this
             * Probably needs work...
             */
            #region PacketFill Code -- commented out
            //working index pointer iX
            /*int iX = 0;

            do
            {
                //Check to see if the PacketBuffer is already full
                if (iPreparedPacketBuffOffset == Constants.iPacketMax)
                {
                    if (this.COMRcvBufferReady != null)
                    {
                        this.COMRcvBufferReady();
                    }
                    //reset PacketOffset so that you can keep reading bytes...
                    iPreparedPacketBuffOffset = 0;
                }
                
                //copy a byte from PacketBuffer to PreparedPacket at the stored offset
                baPreparedPacket[iPreparedPacketBuffOffset] = baPacketBuffer[iX];

                if (iX < baWorkingBufferTemp.Length - 1)
                {
                    iX++;
                    iPreparedPacketBuffOffset++;
                }

                else
                {
                    //move the PacketOffset to the next open spot
                    iPreparedPacketBuffOffset++;
                    break;
                }
                
            } while (true);
            //Debug.Print("Leaving Byte Proc Loop");
            */
            #endregion PacketFill Code
        
        }
        #endregion ProcessBuffer Handler

        /* This event handler captures any SerialPort Receive Errors
         * 
         */
        #region Serial ErrorReceived Handler
        void oSerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            if (e.EventType == SerialError.RXOver)
                Debug.Print("RXOver on Port");

            else if (e.EventType == SerialError.Overrun)
                Debug.Print("Overrun on Port");

            else if (e.EventType == SerialError.RXParity)
                Debug.Print("Parity on Port");

            else if (e.EventType == SerialError.Frame)
                Debug.Print("Frame on Port");
        }
        #endregion Serial ErrorReceived Handler

    #endregion Event Handlers Methods

        #region Class Methods

        /* Send method, for debug for now pretty broken...probably needs some work
         */ 
        #region SerialPort Transmit
        public void serialPortTransmit(byte[] baBytesToSend)
        {

            //Debug.Print("Sndg Pkt!");
            byte[] baTransmitWorkingBufferTemp = new byte[Constants.iTransmitBufferMaxBytes];

            int iX = 0;

            //transfer contents from BytesToSend to WorkingBuffer
            lock (baBytesToSend)
            {
                //check to see if bytes to send is greater than the TransmitBufferMaxBytes
                if (baBytesToSend.Length > Constants.iTransmitBufferMaxBytes)
                {
                    //for now just debug print, need to do more than this...
                    Debug.Print("TX BffrOvflw");
                }

                else
                {
                    for (; iX < baBytesToSend.Length; iX++)
                    {
                        baTransmitWorkingBufferTemp[iX] = baBytesToSend[iX];

                        //only send what is needed to be sent
                        if (baTransmitWorkingBufferTemp[iX] == '\0')
                            break;
                    }
                }
            }

            //write the WorkingBuffer out the port only to when the first null
            oSerialPort.Write(baTransmitWorkingBufferTemp, 0, iX);

        }
        #endregion SerialPort Transmit


        /* Somewhat of a hack to make sure both
         * ports are open before the DataReceived EventHandler is subscribed too
         */ 
        #region Start Listening Method
        public void startListeningToPort()
        {
            /* For Event Handlers
             */ 
            Debug.Print(oSerialPort.PortName + " DataReceived EventHandler Subscribed!");
            oSerialPort.DataReceived += new SerialDataReceivedEventHandler(oSerialPort_DataReceivedHandler);
            Debug.Print(oSerialPort.PortName + " ErrorReceived EventHandler Subscribed!");
            oSerialPort.ErrorReceived += new SerialErrorReceivedEventHandler(oSerialPort_ErrorReceived);
           
 
            /*For Thread*/
            //Debug.Print(oSerialPort.PortName + "listen thread started!");
            //mySerialListenThread.Start();

        }

        public void closePort()
        {
            oSerialPort.Close();
        }
        #endregion Open and Close Port Methods


    #endregion Class Methods

    }

}
