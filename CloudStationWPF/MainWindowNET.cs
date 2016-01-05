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
    partial class MainWindow
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        private void startServer()
        {
            // Set the TcpListener on port 13000.
            Int32 port = runningPort;


            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    //Debug.WriteLine("Waiting for a connection...");
                    writeToLog("Waiting for a connection...");
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            allDone.Set();

            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            ClientConnection connection = new ClientConnection();
            connection.socket = handler;
            connection.setInfoFromSocket();


            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                clientsToConnect.Add(connection.stringId, connection);
                txbNewConnections.Text = "" + clientsToConnect.Count;
            }));



            connection.Receive();

        }

    }

    public class StateObject
    {
        // Client  socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }
}
