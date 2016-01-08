using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace CloudStationWPF
{
    class ClientConnection
    {

        public bool ackCriticalSection = false;
        public int id;
        public string host = "";
        public int port;
        public bool onlyConnect = false;
        public bool requestedToJoin = false;

        public string stringId = "";

        private static String response = String.Empty;



        public void startConnecting()
        {
            connectClient(host, port);
        }

        public Socket socket;

        public void setInfoFromSocket()
        {
            IPEndPoint remoteIpEndPoint = socket.RemoteEndPoint as IPEndPoint;
            host = remoteIpEndPoint.Address.ToString();
            port = remoteIpEndPoint.Port;
            stringId = host + ":" + port;
        }

        private void connectClient(string host, int port)
        {
            //stringId = host + ":" + port;
            TcpClient t = new TcpClient(AddressFamily.InterNetwork);
            //            IPAddress remoteHost = new IPAddress(host);
            IPAddress[] remoteHost = Dns.GetHostAddresses(host);
            IPEndPoint remoteEP = new IPEndPoint(remoteHost[0], port);
            stringId = host + ":" + port;
            MainWindow.self.writeToLog("Establishing Connection to " + stringId);

            // Create a TCP/ IP socket.
            Socket client = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            socket = client;
            //setInfoFromSocket();

            // Connect to the remote endpoint.
            client.Connect(remoteEP);
            afterConnect();
            //client.BeginConnect(remoteEP,
            //    new AsyncCallback(ConnectCallback), client);

            //Send("This is a test");
            Receive();
        }

        private void afterConnect()
        {
            if (onlyConnect)
            {
                MainWindow.self.writeToLog("Sending connect only (K) to" + socket.RemoteEndPoint.ToString());
                sendMessage(new MessageLIS('K', MainWindow.self.stringId));
            }
            else
            {
                MainWindow.self.writeToLog("Socket connected to" + socket.RemoteEndPoint.ToString());
                MainWindow.self.writeToLog("Sending topology connection request");
                sendMessage(new MessageLIS('J', MainWindow.self.stringId));
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Complete the connection.
                socket.EndConnect(ar);
                afterConnect();

            }
            catch (Exception e)
            {
                MainWindow.self.writeToLog(e.ToString());
            }
        }



        public void Receive()
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = socket;

                // Begin receiving the data from the remote device.
                socket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                MainWindow.self.writeToLog(e.ToString());
            }
        }

        private void writeToLog(string text)
        {
            MainWindow.self.writeToLog("#"+stringId+":"+text);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                /*if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                }*/
                string data = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                //writeToLog("<<<<<<<<<<<<"+ data);
                for (int i = 0; i < bytesRead; i++)
                {
                    parseRecData(state.buffer[i]);
                }


                // Get the rest of the data.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                writeToLog(e.ToString());
                Console.WriteLine(e.ToString());
            }
        }

        int state = 0;
        int expectedSize = 0;
        int receivedSize = 0;
        MessageLIS message;
        private void parseRecData(byte symbol)
        {
            if (state == 0 && symbol == '\\')
            {
                message = new MessageLIS();
                message.idSource = this.id;
                message.stringId = this.stringId;
                receivedSize = 0;
                state++;
            }
            else if (state == 1)
            {
                message.messageType = (char)symbol;
                state++;
            }
            else if (state == 2)
            {
                expectedSize = symbol;
                state++;
            }
            else if (state == 3)
            {
                expectedSize = expectedSize * 256 + symbol;
                state++;
                message.messageDataOrig = new byte[expectedSize];
                if(expectedSize == 0)
                {
                    MainWindow.self.receivedMessage(message);
                    state = 0;
                }
            }
            else
            {
                //message.messageData += (char)symbol;
                message.messageDataOrig[receivedSize] = symbol;
                receivedSize++;
                if (receivedSize == expectedSize)
                {
                    message.setMessageDataFromOrig();
                    MainWindow.self.receivedMessage(message);
                    state = 0;
                }
            }


        }

        public void sendMessage(MessageLIS message)
        {
            Send(message.buildMessage());
        }

        public void sendMessage(char messageType, String data)
        {
            MessageLIS message = new MessageLIS();
            message.messageData = data;
            message.messageType = messageType;
            Send(message.buildMessage());
        }

        public void Send(byte[] data)
        {
            // Convert the string data to byte data using ASCII encoding.
            //byte[] byteData = Encoding.ASCII.GetBytes(data);
            byte[] byteData = data;
            //writeToLog(">>>>>>>>>>>>" + data);
            // Begin sending the data to the remote device.
            socket.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), socket);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                Debug.WriteLine("Sent {0} bytes to server.", bytesSent);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }


    }


    public class MessageLIS
    {
        public int idSource = 0;
        public string stringId = "";
        public char messageType;
        public int messageLength = 0;
        public string messageData = "";
        public byte[] messageDataOrig = null;

        public MessageLIS()
        {

        }

        public MessageLIS(char messageType, string messageData)
        {
            this.messageType = messageType;
            this.messageData = messageData;
        }

        public byte[] buildMessage()
        {
            int totalLength = messageData.Length;// + 2;

            byte[] data = Encoding.ASCII.GetBytes(messageData);

            if (messageDataOrig != null)
            {
                data = messageDataOrig;
                totalLength = messageDataOrig.Length;
            }

            byte[] ret = new byte[data.Length + 4];
            ret[0] = (byte)'\\';
            ret[1] = (byte)messageType;
            ret[2] = (byte)(totalLength / 256);
            ret[3] = (byte)(totalLength % 256);
            System.Buffer.BlockCopy(data, 0, ret, 4, data.Length);
            return ret;
        }

        public void setMessageDataFromOrig()
        {
            try
            {
                messageData = Encoding.ASCII.GetString(messageDataOrig, 0, messageDataOrig.Length);
            }
            catch(Exception)
            {
            }
        }

        public int getLamportCounter()
        {
            return int.Parse(messageData);
            //return messageDataOrig[0] * 256 + messageDataOrig[1];
        }

        public void setLamportCounter(int value)
        {
            messageData = value.ToString();
            /*messageDataOrig = new byte[2];
            messageDataOrig[0] = (byte)(value / 256);
            messageDataOrig[1] = (byte)(value % 256);
            */
        }
    }

    public class CriticalRequest
    {
        public int counter = 0;
        public string stringId = "";
        public bool ackSend = false;

        public CriticalRequest()
        {

        }

        public CriticalRequest(int counter, string stringId)
        {
            this.counter = counter;
            this.stringId = stringId;
        }
    }

}
