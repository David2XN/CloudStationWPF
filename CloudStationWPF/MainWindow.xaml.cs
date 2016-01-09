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
using System.Net.Sockets;
using System.Net;
using System.IO;


namespace CloudStationWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Thread server;
        string folder = "datax";
        string folderPath = "";
        public static MainWindow self;
        public MainWindow()
        {
            //Environment.GetCommandLineArgs();

            InitializeComponent();
            self = this;
        }

        Dictionary<string, ClientConnection> clients = new Dictionary<string, ClientConnection>();

        Dictionary<string, ClientConnection> clientsToConnect = new Dictionary<string, ClientConnection>();

        List<FileTask> toSendCritical = new List<FileTask>();

        List<CriticalRequest> criticalRequests = new List<CriticalRequest>();
        public string stringId = "";
        private bool inCriticalSection = false;
        private bool requestToCriticalSectionSend = false;
        private int lamportCounter = 1;
        private int waitingForConnectionsNumber = 0;

        public void sendMessageToAll(MessageLIS message)
        {
            foreach (var client in clients){
                client.Value.sendMessage(message);
            }
        }
        

        public void accessCriticalSection()
        {
            if (!(wantsToEnterCriticalSection() && isReady()))
                return;
            if (requestToCriticalSectionSend)
                return;
            requestToCriticalSectionSend = true;
            writeToLog("I want enter Critical Section");

            if(clients.Count == 0)
            {
                writeToLog("Entering critical section because no nodes to notify");
                enteredCriticalSection();
                return;
            }

            foreach (var client in clients)
            {
                client.Value.ackCriticalSection = false;
            }

            CriticalRequest newRequest = new CriticalRequest(++lamportCounter, stringId);
            newRequest.ackSend = true;
            addRequestToQueue(newRequest);
            MessageLIS message = new MessageLIS('L', "");
            message.setLamportCounter(lamportCounter);
            sendMessageToAll(message);

            
        }

        private void addRequestToQueue(CriticalRequest request)
        {
            writeToLog("Added new CriticalRequest to queue " + request.stringId + " " + "(" + request.counter + ")");
            criticalRequests.Add(request);
            criticalRequests = criticalRequests.OrderByDescending(c => c.counter).ThenBy(c => c.stringId).ToList();//.ThenBy(c => c.ackSend);
        }

        private void updateGUICounters()
        {
            txbNewConnections.Text = "" + clientsToConnect.Count;
            txbNodeCount.Text = "" + (clients.Count + 1);
            txbTest.Text = "Wait: " + waitingForConnectionsNumber + ", Req: " + requestToCriticalSectionSend;
            txbClients.Document.Blocks.Clear();
            var clientBuilder = new StringBuilder();
            clientBuilder.Append(stringId + " (me)" + Environment.NewLine);
            foreach (var item in clients)
            {
                clientBuilder.Append(String.Format("{0} " + Environment.NewLine, item.Value.stringId));
            }
            txbClients.AppendText(clientBuilder.ToString());
        }

        public void enteredCriticalSection()
        {
            writeToLog("Entered Critical Section");
            inCriticalSection = true;

            foreach (var client in clientsToConnect.Where(c => c.Value.requestedToJoin).ToList())
            {
                writeToLog("Sending new connection details for "+ client.Value.stringId);
                /*foreach (string file in Directory.EnumerateFiles(folderPath))
                {
                    string contents = File.ReadAllText(file);
                }*/

                client.Value.sendMessage('C', configurationAsString());
                sendMessageToAll(new MessageLIS('N', client.Value.stringId));
                addClient(client.Value);
            }
            clientsToConnect = clientsToConnect.Where(c => !c.Value.requestedToJoin).ToDictionary(i => i.Key, i => i.Value);
            //clientsToConnect.Clear();

            foreach (var file in toSendCritical)
            {
                var path = Path.Combine(folderPath, file.fileName);
                writeToLog("Sending file " + path);
                sendMessageToAll(new MessageLIS('T', file.fileName + "|" + File.ReadAllText(path)));
            }
            toSendCritical.Clear();




            updateGUICounters();
            leaveCriticalSection();
        }

        public void leaveCriticalSection()
        {
            writeToLog("CS Leaved by me");
            inCriticalSection = false;
            requestToCriticalSectionSend = false;
            criticalRequests.RemoveAll(c => c.stringId == stringId);
            sendMessageToAll(new MessageLIS('F',""));
        }

        private void sendACKToNextNode()
        {
            if (!criticalRequests.Any()) //Empty
                return;
            CriticalRequest request = criticalRequests.ElementAt(0);

            if (request.ackSend == false) // Should be received message
            {
                writeToLog(string.Format("CS Access granted from me to node {0} ({1}) ", request.stringId, request.counter));
                request.ackSend = true;
                clients[request.stringId].sendMessage(new MessageLIS('A', "")); //Acknowledge CS
            }
        }

        public bool isReady()
        {
            return waitingForConnectionsNumber == 0; // Should not be less than zero
        }

        public bool wantsToEnterCriticalSection()
        {
            return toSendCritical.Count > 0 || clientsToConnect.Where(c => c.Value.requestedToJoin).ToList().Count > 0;
        }

        /*
            Lamport
            A = Ack CS
            F = Release CS
            L = Lamport Request CS


            Nodes manipulation
            C = Conf recieved
            D = Remove node
            K = Connect to known node 
            J = Join request
            N = new client to connect

            Files manipulation
            T = file request
         */
        public void receivedMessage(MessageLIS message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                //writeToLog("Recieved message:" + message.messageType);
                ClientConnection client;
                if (clients.ContainsKey(message.stringId))
                    client = clients[message.stringId];
                else if (clientsToConnect.ContainsKey(message.stringId))
                    client = clientsToConnect[message.stringId];
                else
                {
                    writeToLog("Recieved message from unknown node !!!" + message.stringId);
                    return;
                }

                if (message.messageType == 'J') // RequestJoin
                {
                    writeToLog("New client wants to join " + message.messageData);
                    client.requestedToJoin = true;
                    client.stringId = message.messageData;
                }
                else if (message.messageType == 'K')
                {
                    writeToLog("New clients wants to only connect " + message.messageData);
                    client.stringId = message.messageData;
                    clientsToConnect.Remove(message.stringId);
                    addClient(client);
                    waitingForConnectionsNumber--;
                }
                else if (message.messageType == 'C') // Configuration
                {
                    writeToLog("Recieved configuration - "+ message.messageData);
                    var configs = message.messageData.Split('|');
                    lamportCounter = int.Parse(configs[0]);
                    waitingForConnectionsNumber += configs[1].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Length;
                }
                else if (message.messageType == 'N') // New client to connect
                {
                    var connectionInfo = message.messageData.Split(':');
                    writeToLog("Connecting to NODE " + message.messageData);
                    
                    addNewClientConnection(connectionInfo[0], int.Parse(connectionInfo[1]), true);
                }
                else if (message.messageType == 'D')
                {
                    writeToLog("Removing NODE (D) " + message.messageData);
                    removeClient(message.messageData);
                }
                else if (message.messageType == 'L') // Lamport Request CS
                {
                    writeToLog(string.Format("CS Requested {0} ({1})", message.stringId, message.getLamportCounter()));
                    CriticalRequest newRequest = new CriticalRequest(message.getLamportCounter(), message.stringId);
                    addRequestToQueue(newRequest);
                    lamportCounter = Math.Max(lamportCounter, message.getLamportCounter());
                }
                else if (message.messageType == 'A') // Acknowledged CS
                {
                    writeToLog(string.Format("CS Access granted to me from {0}", message.stringId));
                    client.ackCriticalSection = true;
                    //criticalRequests.Find()
                }
                else if (message.messageType == 'F') // Release CS
                {
                    writeToLog(string.Format("CS Released {0} ", message.stringId));
                    criticalRequests.RemoveAll(c => c.stringId == message.stringId);
                    /*int requestIndex = criticalRequests.FindIndex(c => c.stringId == message.stringId);
                    if(requestIndex >= 0)
                    {
                        criticalRequests.RemoveAt(requestIndex);
                    }*/
                    
                }
                else if (message.messageType == 'T') // Transfer File (Text Only)
                {
                    writeToLog(string.Format("Transfer file requested"));
                    string[] conf = message.messageData.Split(new char[] { '|' }, 2);
                    var path = Path.Combine(folderPath, conf[0]);
                    File.WriteAllText(path, conf[1]);
                }


                checkActions();
            }));
        }

        private void checkActions()
        {
            if (requestToCriticalSectionSend && clients.All(c => c.Value.ackCriticalSection))
            {
                enteredCriticalSection();
            }
            accessCriticalSection();
            sendACKToNextNode();
            updateGUICounters();
        }

        internal void handleClientException(ClientConnection client, Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                if (ex is SocketException || ex is ObjectDisposedException)
                {
                    disconnectClient(client, true);
                    checkActions();
                }
            }));
        }

        private void removeClient(string stringId)
        {
            if(clients.ContainsKey(stringId))
            {
                var client = clients[stringId];
                try
                {
                    client.socket.Close();
                }
                catch(Exception)
                {

                }
                
            }
            clients.Remove(stringId);
            criticalRequests.RemoveAll(c => c.stringId == stringId);
        }

        private void disconnectClient(ClientConnection client, bool notifyOthers = false)
        {
            removeClient(client.stringId);
            if (notifyOthers)
            {
                sendMessageToAll(new MessageLIS('D', client.stringId));
            }
        }

        public String configurationAsString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(lamportCounter.ToString()+"|");
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
            string currentPath = Directory.GetCurrentDirectory();
            folderPath = Path.Combine(currentPath, folder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            CreateFileWatcher(folderPath);

            runningPort = int.Parse(txbServerPort.Text);
            stringId = txpMyIPAddress.Text + ":" + runningPort;
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
            if (text == null)
                return;
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                string formText = DateTime.Now.ToString("HH:mm:ss.fff") + ":" + text + "\n"; //
                //txbLog.Cr
                Debug.WriteLine(formText);
                txbLog.AppendText(formText);
                txbLog.ScrollToEnd();
            }));

        }


        //ClientConnection client;
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            addNewClientConnection(txpIPAddress.Text, int.Parse(txbConnectPort.Text));
        }

        private void addNewClientConnection(string host, int port, bool onlyConnect = false)
        {
            ClientConnection client = new ClientConnection();
            client.stringId = client.host + ":" + client.port;
            //this.client = client;
            client.id = 1;
            client.host = host;
            client.port = port;
            client.onlyConnect = onlyConnect;

            client.startConnecting();
            addClient(client);
        }

        private void addClient(ClientConnection client)
        {
            if(clients.ContainsKey(client.stringId))
            {
                clients.Remove(client.stringId);
            }
            clients.Add(client.stringId, client);
            updateGUICounters();


        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            accessCriticalSection();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            txbLog.AppendText(Environment.NewLine);
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

        private void reinit()
        {
            clients.Clear();
            clientsToConnect.Clear();
            toSendCritical.Clear();
            criticalRequests.Clear();
            inCriticalSection = false;
            requestToCriticalSectionSend = false;
            lamportCounter = 1;
            waitingForConnectionsNumber = 0;
    }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                server.Abort();
            }
            catch (Exception ex)
            {
                writeToLog(ex.ToString());
            }
            try
            {
                sendMessageToAll(new MessageLIS('D', stringId));
            }
            catch (Exception ex)
            {
                writeToLog(ex.ToString());
            }
            reinit();
        }



        // File Watcher
        FileSystemWatcher watcher;
        public void CreateFileWatcher(string path)
        {
            // Create a new FileSystemWatcher and set its properties.
            watcher = new FileSystemWatcher();
            watcher.Path = path;
            /* Watch for changes in LastAccess and LastWrite times, and 
               the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
       | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            // Only watch text files.
            //watcher.Filter = "*.txt";

            // Add event handlers.
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            // Begin watching.
            watcher.EnableRaisingEvents = true;

        }

        private void addFileTask(FileTask task)
        {
            toSendCritical.RemoveAll(c => c.fileName == task.fileName);
            toSendCritical.Add(task);
        }

        // Define the event handlers.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // Specify what is done when a file is changed, created, or deleted.
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                writeToLog("Added task file: " + e.Name + " " + e.ChangeType);
                writeToLog("Added task file: " + e.Name + " " + e.ChangeType);
                addFileTask(new FileTask(e.Name));
                accessCriticalSection();
            }));
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            // Specify what is done when a file is renamed.
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => {
                Debug.WriteLine("Added two tasks, file: {0} renamed to {1}", e.OldName, e.Name);
                writeToLog(String.Format("Added two tasks, file: {0} renamed to {1}", e.OldName, e.Name));
                addFileTask(new FileTask(e.OldName));
                addFileTask(new FileTask(e.Name));
                accessCriticalSection();
            }));
        }

    }
}
