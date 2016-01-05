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

        public string stringId = "";

        private static String response = String.Empty;



        public void startConnecting()
        {
            connectClient(host, port);
        }

        public Socket socket;

        private void connectClient(string host, int port)
        {
            stringId = host + ":" + port;
            TcpClient t = new TcpClient(AddressFamily.InterNetwork);
            //            IPAddress remoteHost = new IPAddress(host);
            IPAddress[] remoteHost = Dns.GetHostAddresses(host);
            IPEndPoint remoteEP = new IPEndPoint(remoteHost[0], port);
            MainWindow.self.writeToLog("Establishing Connection to " + remoteHost[0]);

            // Create a TCP/ IP socket.
            Socket client = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            socket = client;
            // Connect to the remote endpoint.
            client.BeginConnect(remoteEP,
                new AsyncCallback(ConnectCallback), client);

            //Send("This is a test");
            Receive();
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Complete the connection.
                socket.EndConnect(ar);

                MainWindow.self.writeToLog("Socket connected to" + socket.RemoteEndPoint.ToString());
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
                for (int i = 0; i < bytesRead; i++)
                {
                    parseRecData(data[i]);
                }

                writeToLog("<<<<<<<<<<<<"+ data);
                // Get the rest of the data.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        int state = 0;
        int expectedSize = 0;
        int receivedSize = 0;
        MessageLIS message;
        private void parseRecData(char symbol)
        {
            if (state == 0 && symbol == '\\')
            {
                state = 1;
                message = new MessageLIS();
                message.idSource = this.id;
                message.stringId = this.stringId;
                receivedSize = 0;
            }
            else if (state == 1)
            {
                state = 2;
                expectedSize = symbol;
                
            }
            else if (state == 2)
            {
                expectedSize = expectedSize * 256 + symbol;
            }
            else
            {
                message.messageData += symbol;
                if (receivedSize == expectedSize)
                {
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

        public void Send(String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            writeToLog(">>>>>>>>>>>>" + data);
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
}
