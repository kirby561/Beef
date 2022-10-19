
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Beef.BeefApi {
    public class ApiServer {
        private String _configFilePath;
        private SynchronizationContext _mainContext;
        private BeefUserConfigManager _beefUserManager;
        private PresentationManager _ladderManager;
        private WebApplication _application;
        private Thread _thread;
        private object _lock = new object();

        public ApiServer(String configFilePath, SynchronizationContext mainContext, PresentationManager ladderManager, BeefUserConfigManager beefUserManager) {
            _configFilePath = configFilePath;
            _mainContext = mainContext;
            _ladderManager = ladderManager;
            _beefUserManager = beefUserManager;
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
            lock (_lock) {
                var builder = WebApplication.CreateBuilder();
                builder.Configuration.AddJsonFile(_configFilePath);
                
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

            _application.Run();
        }
    }
}
