
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace Beef.BeefApi {
    /// <summary>
    /// The ApiServer hosts a REST endpoint at /beef-ladder that returns a json representation
    /// of the current ladder rankings. It also provides a socket that can be connected to at
    /// the configured port in order to be notified realtime when the ladder has been changed.
    /// </summary>
    public class ApiServer {
        private String _configFilePath;
        private SynchronizationContext _mainContext;
        private BeefUserConfigManager _beefUserManager;
        private PresentationManager _ladderManager;
        private WebApplication _application;
        private Thread _thread;
        private SynchronizationContext _threadContext;
        private int _eventSocket; // set from the config file
        private List<Socket> _eventClients = new List<Socket>();
        private object _lock = new object();

        public ApiServer(String configFilePath, SynchronizationContext mainContext, PresentationManager ladderManager, BeefUserConfigManager beefUserManager) {
            _configFilePath = configFilePath;
            _mainContext = mainContext;
            _ladderManager = ladderManager;
            _beefUserManager = beefUserManager;

            // Subscribe to the ladder changed event
            _ladderManager.LadderChanged += OnLadderChanged;
        }

        public void Start() {
            lock (_lock) {
                if (_application != null) {
                    Console.WriteLine("API is already running!");
                    return;
                }

                _thread = new Thread(ThreadStart);
                _thread.Start();

                while (_application == null) {
                    try { Monitor.Wait(_lock); } catch (Exception) { }
                }
            }
        }

        public void Stop() {
            lock (_lock) {
                if (_application == null) {
                    Console.WriteLine("API is not running!");
                    return;
                }

                // ?? TODO: Need to get the other thread's SynchronizationContext
            }
        }

        private void ThreadStart() {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            _threadContext = SynchronizationContext.Current;

            lock (_lock) {
                var builder = WebApplication.CreateBuilder();
                builder.Configuration.AddJsonFile(_configFilePath);
                _eventSocket = builder.Configuration.GetValue<int>("LadderChangedEventPort", 5002);
                
                _application = builder.Build();
                Monitor.PulseAll(_lock);
            }

            _application.MapGet("/beef-ladder", async (HttpContext context) => {
                List<BeefEntry> currentLadder = null;
                List<BeefUserConfig> userConfigs = null;

                // This needs to be posted to the main context, so have a threadpool
                // thread wait for the result so we don't block.
                await Task.Run(() => {
                    // The main context owns the ladder state so post to that thread to
                    // get the latest version and signal when it's done.
                    bool done = false;
                    object signal = new object();
                    _mainContext.Post((state) => {
                        currentLadder = _ladderManager.ReadBracket();
                        userConfigs = new List<BeefUserConfig>();
                        foreach (BeefEntry entry in currentLadder) {
                            BeefUserConfig userConfig = _beefUserManager.GetUserByName(entry.PlayerName);
                            userConfigs.Add(userConfig);
                        }

                        lock (signal) {
                            done = true;
                            Monitor.PulseAll(signal);
                        }
                    }, null);

                    lock (signal) {
                        while (!done) {
                            try { Monitor.Wait(signal); } catch (Exception) { }
                        }
                    }
                });
                
                // Build the ladder model
                GetLadderResponse response = new GetLadderResponse();
                response.BeefLadder = new GetLadderReponseEntry[currentLadder.Count];
                for (int index = 0; index < currentLadder.Count; index++) {
                    String beefName = currentLadder[index].PlayerName;
                    int rank = currentLadder[index].PlayerRank;
                    BeefUserConfig userConfig = userConfigs[index];
                    String mmr = "";
                    String race = "";
                    if (userConfig != null) {
                        beefName = userConfig.BeefName;
                        mmr = userConfig.LastKnownMmr;
                        race = userConfig.LastKnownMainRace;
                    }

                    GetLadderReponseEntry entry = new GetLadderReponseEntry() {
                        Rank = rank,
                        BeefName = beefName,
                        Race = race,
                        Mmr = mmr
                    };
                    response.BeefLadder[index] = entry;
                }
                await context.Response.WriteAsJsonAsync(response);
            });

            _threadContext.Post(async (object? state) => {
                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, _eventSocket);
                Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listener.Bind(localEndPoint);
                listener.Listen();

                while (true) {
                    Socket client = await listener.AcceptAsync();

                    lock (_eventClients) {
                        _eventClients.Add(client);
                    }
                }
            }, null);

            _application.Run();
        }

        private void NotifyLadderChanged() {
            List<Socket> clientsToRemove = new List<Socket>();
            lock (_eventClients) {
                foreach (Socket client in _eventClients) {
                    if (!client.Connected) {
                        clientsToRemove.Add(client);
                        continue;
                    }

                    try {
                        String eventMessage = "{ \"Message\": \"OnLadderChanged\" }";
                        int messageLength = eventMessage.Length;
                        byte[] lengthBytes = GetBytesInNetworkOrder(messageLength);
                        byte[] messageBytes = Encoding.UTF8.GetBytes(eventMessage);

                        SendBytesOrDie(lengthBytes, client);
                        SendBytesOrDie(messageBytes, client);
                    } catch (Exception) {
                        // Any exception should just close the connection and keep going
                        clientsToRemove.Add(client);
                    }
                }

                // Cleanup anyone that has disconnected
                foreach (Socket client in clientsToRemove) {
                    _eventClients.Remove(client);
                }
            }
        }

        private byte[] GetBytesInNetworkOrder(int number) {
            byte[] numberBytes = BitConverter.GetBytes(number);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(numberBytes);
            return numberBytes;
        }

        private void OnLadderChanged(List<BeefEntry> entries) {
            _threadContext.Post((object? state) => {
                NotifyLadderChanged();
            }, null);
        }

        /// <summary>
        /// Sends the given bytes to the given socket. If all the bytes aren't sent, an
        /// exception is thrown. Note that in the event the buffer was sent in stages and
        /// an error occurs inbetween the stages, some bytes could have been sent prior to the exception.
        /// </summary>
        /// <param name="bytes">The bytes to send to the socket.</param>
        /// <param name="socket">The socket to write to. It is assumed it's already connected.</param>
        private void SendBytesOrDie(byte[] bytes, Socket socket) {
            int bytesSent = 0;
            int index = 0;
            while (bytesSent < bytes.Length) {
                int result = socket.Send(bytes, index, bytes.Length - index, SocketFlags.None);
                if (result <= 0) {
                    throw new SocketException(result);
                } else {
                    bytesSent += result;
                }
            }
        }
    }
}
