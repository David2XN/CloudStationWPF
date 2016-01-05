using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CloudStationWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Thread server;
        string folder = "data0";
        public static MainWindow self;
        public MainWindow()
        {
            //Environment.GetCommandLineArgs();

            InitializeComponent();
            self = this;
        }

        Dictionary<string, ClientConnection> clients = new Dictionary<string, ClientConnection>();

        List<MessageLIS> toSendCritical = new List<MessageLIS>();

        List<CriticalRequest> criticalRequests = new List<CriticalRequest>();
        public string stringId = "";
        private bool inCriticalSection = false;
        private int lamportCounter = 1;

        public void sendMessageToAll(MessageLIS message)
        {
            foreach (var client in clients){
                client.Value.sendMessage(message);
            }
        }
        

        public void accessCriticalSection()
        {
            writeToLog("I want enter Critical Section");
            CriticalRequest newRequest = new CriticalRequest(++lamportCounter, stringId);
            addRequestToQueue(newRequest);
            MessageLIS message = new MessageLIS('L', "");
            message.setLamportCounter(lamportCounter);
            sendMessageToAll(message);
        }

        private void addRequestToQueue(CriticalRequest request)
        {
            writeToLog("Added new CriticalRequest to queue");
            criticalRequests.Add(request);
            criticalRequests = criticalRequests.OrderBy(c => c.counter).ThenBy(c => c.stringId).ToList();//.ThenBy(c => c.ackSend);
        }

        public void enteredCriticalSection()
        {
            writeToLog("Entered Critical Section");
            inCriticalSection = true;
            leaveCriticalSection();
        }

        public void leaveCriticalSection()
        {
            writeToLog("Leaved Critical Section");
            inCriticalSection = false;
            sendMessageToAll(new MessageLIS('F',""));
        }

        private void sendACKToNextNode()
        {
            if (!criticalRequests.Any()) //Empty
                return;
            CriticalRequest request = criticalRequests.ElementAt(0);
        }

        public void receivedMessage(MessageLIS message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                ClientConnection client = clients[message.stringId];
                if (message.messageType == 'J') // RequestJoin
                {
                    writeToLog("New client wants to joins");
                    client.sendMessage('C', configurationAsString());
                }
                else if (message.messageType == 'C') // SendConfiguration
                {
                    writeToLog("Sending Conf to new client");
                }
                else if (message.messageType == 'L') // Lamport Request CS
                {
                    writeToLog(string.Format("{0} ({1}) CS Requested", message.stringId, message.getLamportCounter()));
                    CriticalRequest newRequest = new CriticalRequest(message.getLamportCounter(), message.stringId);
                    addRequestToQueue(newRequest);
                    lamportCounter = Math.Max(lamportCounter, message.getLamportCounter());

                    if (inCriticalSection)
                        return;


                    if (criticalRequests.ElementAt(0).ackSend == false) // Should be received message
                    {
                        writeToLog(string.Format("{0} ({1}) CS Access granted to node", message.stringId, message.getLamportCounter()));
                        client.sendMessage(new MessageLIS('A', "")); //Acknowledge CS
                    }
                }
                else if (message.messageType == 'A') // Acknowledged CS
                {
                    writeToLog(string.Format("{0} CS Access granted to me", message.stringId));
                    client.ackCriticalSection = true;
                    if (clients.All(c => c.Value.ackCriticalSection))
                    {
                        enteredCriticalSection();
                    }
                    //criticalRequests.Find()
                }
                else if (message.messageType == 'F') // Release CS
                {
                    writeToLog(string.Format("{0} ({1}) CS Released", message.stringId));
                    int requestIndex = criticalRequests.FindIndex(c => c.stringId == message.stringId);
                    criticalRequests.RemoveAt(requestIndex);
                }
                else if (message.messageType == 'T') // Transfer File (Text Only)
                {
                    writeToLog(string.Format("Transfer file requested"));
                }
            }));
        }

        public String configurationAsString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var client in clients)
            {
                builder.Append(client.Key + ";"); 
            }
            return builder.ToString();
        }

        volatile int runningPort;
        volatile int connectingPort;
        volatile string connectIP = "";
        private void btnServer_Click(object sender, RoutedEventArgs e)
        {
            runningPort = int.Parse(txbServerPort.Text);
            stringId = "127.0.0.1:" + runningPort;
            if (server != null)
                server.Abort();

            server = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                /* run your code here */
                startServer();
            });
            server.Start();
        }

        public void writeToLog(string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                string formText = DateTime.Now.ToString() + ":" + text + Environment.NewLine; //
                Debug.WriteLine(formText);
                txbLog.AppendText(formText);
                txbLog.ScrollToEnd();
            }));

        }


        ClientConnection client;
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            ClientConnection client = new ClientConnection();
            client.stringId = client.host + ":" + client.port;
            this.client = client;
            client.id = 1;
            client.host = txpIPAddress.Text;
            client.port = int.Parse(txbConnectPort.Text);

            client.startConnecting();
            clients.Add(client.stringId, client);
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            accessCriticalSection();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            String[] args = Environment.GetCommandLineArgs();
            if (args.Length == 1)
                return;
            if (args.Length >= 3)
            {
                folder = args[1];
                txbServerPort.Text = args[2];
                btnServer_Click(null, null);
            }
            if (args.Length >= 6)
            {
                txpIPAddress.Text = args[3];
                txbConnectPort.Text = args[4];
                Thread.Sleep(int.Parse(args[5])*1000);
                btnStart_Click(null, null);
            }
        }
    }
}
