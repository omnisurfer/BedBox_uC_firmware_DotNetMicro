using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
//using System.Text;

/* Referencing website:
 * http://netduinohacking.blogspot.com/2011/03/netduino-plus-web-server-hello-world.html
 * http://msdn.microsoft.com/en-us/library/ych8bz3x.aspx
 * http://www.switchonthecode.com/tutorials/csharp-tutorial-simple-threaded-tcp-server
 * http://www.codeproject.com/KB/IP/TCPIPChat.aspx
 */

namespace TCP_Class
{

    #region Class Delegates

    /* Place to hold Delegates
    */

    public delegate void TCPPacketReadyEventHandler();
    public delegate void EndPointConnectedEventHandler();
    public delegate void EndPointDisconnectedEventHandler();

    #endregion Delegates

    #region Class Constants
    public class TCPBrokerConstants
    {
        /* Place to hold constants
         */
        public const int iPacketMaxBytes = 128;
        public const int iBufferMaxBytes = 128;
    }
    #endregion Class Constants


    #region Class Members and Constructors
    public class TCPBroker : IDisposable
    {
        //socket is how you get access to the Ether Port, IPEndPoint holder for Session
        private Socket mySocket = null;
        private IPEndPoint mySessionEndPoint = null;

        /* Thread to listen for data on the socket, EndPoint Connected and Disconnected EventHandlers
         * and a public ServerPacketReady even to notify when a packet is received from the "Server"
         * May add a handler for buffer overflow?   
         */ 
        private Thread myListenThread;
        private event EndPointConnectedEventHandler TCPEndPointConnected;
        private event EndPointDisconnectedEventHandler TCPEndPointDisconnected;

        public event TCPPacketReadyEventHandler TCPServerPacketReady;

        /* byte array to store the received data from server, an int to
         * store the port number, mostly for debug and a public bool to indicate
         * whether or not the port is open or close (don't try to use socket if
         * it is closed).
         */ 
        private byte[] baRcvPacketBuffer = new byte[TCPBrokerConstants.iPacketMaxBytes];
        private int iPortNoStore;

        //default to false so user does not try to use it before it is open
        public bool boEndPointOpen = false;

        //For fixed IP and chosen Port
        public TCPBroker(string sIPAddress, int iPortNo)
        {
            iPortNoStore = iPortNo;
            //Request and bind to the IP address I am calling out to send to and initialize Socket
            mySessionEndPoint = new IPEndPoint(IPAddress.Parse(sIPAddress), iPortNo);
            mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //Print the IP Address I am Listening On
            Debug.Print("EP " + Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].IPAddress + "@" + iPortNoStore);

            //creat event handlers and subscribe to do stuff when the TCPRcvBuffers are Ready - seems to work but not sure if done properly...
            TCPEndPointConnected += new EndPointConnectedEventHandler(TCPBroker_TCPEndPointConnected);
            TCPEndPointDisconnected += new EndPointDisconnectedEventHandler(TCPBroker_TCPEndPointDisconnected);

            //Init Listening Thread
            this.myListenThread = new Thread(new ThreadStart(receiveFromEndpoint));
            
        }

        //For DHCP IP and chosen Port
        public TCPBroker(int iPortNo)
        {
            iPortNoStore = iPortNo;
            //Request and bind an IP from the DHCP server and Initialize Socket
            mySessionEndPoint = new IPEndPoint(IPAddress.Any, iPortNo);
            mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //Print the IP Address I am Listening On
            Debug.Print("EP " + Microsoft.SPOT.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0].IPAddress + "@" + iPortNoStore);

            //creat event handlers and subscribe to do stuff when the TCPRcvBuffers are Ready - seems to work but not sure if done properly...
            TCPEndPointConnected += new EndPointConnectedEventHandler(TCPBroker_TCPEndPointConnected);
            TCPEndPointDisconnected += new EndPointDisconnectedEventHandler(TCPBroker_TCPEndPointDisconnected);

            //Init Listening Thread
            this.myListenThread = new Thread(new ThreadStart(receiveFromEndpoint));

        }

        #endregion Class Members and Constructors


        #region Event Handler Methods
        void TCPBroker_TCPEndPointDisconnected()
        {
            boEndPointOpen = false;
        }

        void TCPBroker_TCPEndPointConnected()
        {
            boEndPointOpen = true;
        }
        #endregion Event Handler Methods


        #region Class Methods
        /* connectToEndPoint returns a bool to indicate if the attempt was successful or not
         * If it returns true, the EndPoint (ie host computer) was found at whatever the IP and 
         * Port was passed to the constructor. Else, the host is not present or found for whatever
         * reason.
         */
        public bool connectToEndPoint(IPEndPoint thisEndPoint)
        {

            //Attempt EndPoint connection
            try
            {

                mySocket.Connect(thisEndPoint);
               /* Debug print the IP address @ Port Listening On and Broadcast that the EndPoint is connected,
                * start the listen thread, broadcast that the EndPoint is connected
                * 
                */

                //Connection successful!
                Debug.Print("EP " + mySessionEndPoint.Address + "@" + iPortNoStore);
                this.myListenThread.Start();
                if (this.TCPEndPointConnected != null)
                    this.TCPEndPointConnected();

                return true;

            }

            //EndPoint connection failed, close the socket
            catch (SocketException ex)
            {
                /* Displaying exception, broadcast EndPoint Disconnected, close the EndPoint
                    * 
                    */
                Debug.Print("EP Not Found! " + ex.Message);
                if (this.TCPEndPointDisconnected != null)
                    this.TCPEndPointDisconnected();
                mySocket.Close();

                return false;
            }

        }

        /* Takes in a byte array and sends it along. No processing done 
         * on the array.
         * 
         */ 
        public bool sendToEndpoint(byte[] baStoreArray)
        {

            try
            {
                mySocket.Send(baStoreArray);
                return true;
                //Debug.Print("Sent!");
            }

            catch (SocketException ex)
            {

                /* Displaying exception, broadcast EndPoint Disconnected, close the EndPoint
                 * 
                 */ 
                Debug.Print("ERR: " + ex.ToString());
                if (this.TCPEndPointDisconnected != null)
                    this.TCPEndPointDisconnected();
                mySocket.Close();
                return false;

            }
            

        }

        private void receiveFromEndpoint()
        {
            //Debug.Print("Tread Starting...");
            byte[] baRcvBufferTemp = new byte[TCPBrokerConstants.iBufferMaxBytes];
            int iBytesToRead;
            
            while (true)
            {
                iBytesToRead = 0;
                try
                {

                    iBytesToRead = mySocket.Receive(baRcvBufferTemp, SocketFlags.None);

                }

                catch (SocketException e)
                {

                    /* Displaying exception, broadcast EndPoint Disconnected, close the EndPoint
                     * 
                     */ 
                    Debug.Print("SvrRcvEx: " + e.ToString());
                    if(this.TCPEndPointDisconnected != null)
                        this.TCPEndPointDisconnected();
                    mySocket.Close();
                    break;

                }

                if (iBytesToRead == 0)
                {

                    /* Displaying exception, broadcast EndPoint Disconnected, close the EndPoint
                     * 
                     */ 
                    Debug.Print("Host D/C");
                    if (this.TCPEndPointDisconnected != null)
                        this.TCPEndPointDisconnected();    
                    mySocket.Close();
                    break;

                }

                else
                {
                    //Copy RcvBuffer to RcvPacketBuffer
                    lock (baRcvPacketBuffer)
                    {
                        int i = 0;
                        for (; i < iBytesToRead && i < TCPBrokerConstants.iBufferMaxBytes; i++)
                        {

                            baRcvPacketBuffer[i] = baRcvBufferTemp[i];

                        }

                        if (i == TCPBrokerConstants.iBufferMaxBytes)
                            Debug.Print("Ovrflw, i@: " + i);
                    }

                    //Broadcast that a packet is ready
                    if (this.TCPServerPacketReady != null)
                        this.TCPServerPacketReady();
                }

            }
            Debug.Print("Thread Exiting...");
            return;

        }

        public IPEndPoint getSessionIPEndPoint()
        {
            return mySessionEndPoint;
        }

        public byte[] getReceivedPacket()
        {
            return baRcvPacketBuffer;
        }
        #endregion Class Methods


        #region Disposer
        ~TCPBroker()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (mySocket != null)
                mySocket.Close();
        }

        #endregion Disposer

    }
}
