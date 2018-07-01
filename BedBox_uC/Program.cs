using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using System.IO.Ports;
using System.Text;
using TCP_Class;
using SerialPort_Class;
using SecretLabs;

namespace BedBox_uC
{
    public class Program
    {
        #region Serial and TCP Port Object Creation
        /* Setup Serial Port
         * Timeout set to -1 means it will wait forever to Receive data which may work OK for GPS and Compass streams...
         */
        static SerialPortBroker myCOM1 = new SerialPortBroker("COM1", 4800, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One, -1);
        static SerialPortBroker myCOM2 = new SerialPortBroker("COM2", 4800, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One, -1);

        /* Setup TCP Port
         * 
         */
        const string sRemoteEndPointIP0 = "192.168.45.116";
        const int iEndPointPort0 = 3000;
        const int iEndPointPort1 = 3001;

        static TCPBroker myEndPoint0;
        static TCPBroker myEndPoint1;

        #endregion Serial and TCP Port Object Creation


        public static void Main()
        {
            //light an LED to say it is starting
            //Port.

            //Starting TCP first to prevent buffer overflows on the Serial ports while TCP sets up
            #region Main() TCP Setup Code
            do
            {
                //Create the EndPoint
                myEndPoint0 = new TCPBroker(sRemoteEndPointIP0, iEndPointPort0);

                //first attempt to connect to EndPoint
                Debug.Print("EP0...");
                if (!myEndPoint0.connectToEndPoint(myEndPoint0.getSessionIPEndPoint()))
                {
                    /* EndPoint Not Found, resetting Netduino b/c not sure how to clear event handler right now or if I can...?
                     * Also, if Ethernet is only com between Netduino and Head Controller, nothing else can tell it to reset
                     * 
                     */
                    Debug.Print("not found\n");
                    //PowerState.RebootDevice(true);
                }

                else
                {
                    Debug.Print("connected\n");
                    break;
                }

                //sit and wait a bit before trying again, somewhat crude
                Thread.Sleep(1000);

            } while (true);

            do
            {
                //Create the EndPoint
                myEndPoint1 = new TCPBroker(sRemoteEndPointIP0, iEndPointPort1);

                Debug.Print("EP1...");
                if (!myEndPoint1.connectToEndPoint(myEndPoint1.getSessionIPEndPoint()))
                {
                    /* EndPoint Not Found, resetting Netduino b/c not sure how to clear event handler right now or if I can...?
                    * Also, if Ethernet is only com between Netduino and Head Controller, nothing else can tell it to reset
                    * 
                    */
                    Debug.Print("not found\n");
                    //PowerState.RebootDevice(true);
                }

                else
                {
                    Debug.Print("connected\n");
                    break;
                }

                //sit and wait a bit before trying again, somewhat crude
                Thread.Sleep(5000);
            }
            while (true);

            //Create event handlers and subscribe to do stuff when the TCPRcvBuffers are Ready - seems to work but not sure if done properly...
            myEndPoint0.TCPServerPacketReady += new TCPPacketReadyEventHandler(myEndPoint0_TCPServerPacketReady);
            myEndPoint1.TCPServerPacketReady += new TCPPacketReadyEventHandler(myEndPoint1_TCPServerPacketReady);
            #endregion Main() TCP Code


            #region Main() Serial Port Setup Code
            /* Start the Listen EventHandlers
             * 
             */
            myCOM1.startListeningToPort();
            myCOM2.startListeningToPort();

            /* Serial Port Event Handler
             * create an event handler and subscribe to do stuff when the RcvBuffer is Ready - seems to work but not sure if done properly...
             */
            myCOM1.SerialPacketReady += new SerialPacketReadyEventHandler(oCOM1_PacketReadyHandler);
            myCOM2.SerialPacketReady += new SerialPacketReadyEventHandler(oCOM2_PacketReadyHandler);

            #endregion Main() SerialPort Setup Code


            while (true)
            {
                string sTemp = "RAM: " + Debug.GC(true) + '\n';
                byte[] baTemp = Encoding.UTF8.GetBytes(sTemp);
                //Debug.Print("RAM: " + Debug.GC(true));
                myEndPoint0.sendToEndpoint(baTemp);
                Thread.Sleep(2000);
            }

        }

        #region Serial Port Event Handlers
        static void oCOM1_PacketReadyHandler()
        {
            //maybe process the buffer here so that it only sends what is present instead of the full 256bytes...
            
            /* Check that the port is still open, still dont have anything that will reattempt connection...
             * For now just restarting the uC...
             */
            if (myEndPoint0.boEndPointOpen)
            {
                lock (myCOM1.baPreparedPacket)
                {

                    myEndPoint0.sendToEndpoint(myCOM1.baPreparedPacket);
                    //see how much RAM is being used
                    //Debug.Print("RAM: " + Debug.GC(true));

                }
            }

            else
            {
                Debug.Print("EP0 EndPoint D/C!");
                PowerState.RebootDevice(true);
            }

        }
       
        static void oCOM2_PacketReadyHandler()
        {
            //maybe process the buffer here so that it only sends what is present instead of the full 256bytes...

            if (myEndPoint1.boEndPointOpen)
            {
                lock (myCOM2.baPreparedPacket)
                {

                    myEndPoint1.sendToEndpoint(myCOM2.baPreparedPacket);
                    //see how much RAM is being used
                    //Debug.Print("RAM: " + Debug.GC(true));

                }

            }

            else
            {
                Debug.Print("EP1 EndPoint D/C!");
                PowerState.RebootDevice(true);
            }

        }
        #endregion Serial Port Event Handlers

        #region TCP Event Handlers
        static void myEndPoint0_TCPServerPacketReady()
        {
            //will have to create a way to copy bytes over...
            string sTemp = new string(Encoding.UTF8.GetChars(myEndPoint0.getReceivedPacket()));
           //Debug.Print("Rcvd: " + sTemp);

        }

        static void myEndPoint1_TCPServerPacketReady()
        {
            //will have to create a way to copy bytes over...
            string sTemp = new string(Encoding.UTF8.GetChars(myEndPoint1.getReceivedPacket()));
            //Debug.Print("Rcvd: " + sTemp);

        }
        #endregion TCP Event Handlers

    }
}
