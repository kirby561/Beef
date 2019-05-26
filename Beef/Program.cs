using Google.Apis.Auth.OAuth2;
using Google.Apis.Slides.v1;
using Google.Apis.Slides.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Beef {
    class BeefEntry {
        public String ObjectId { get; set; }
        public String PlayerName { get; set; }
        public int PlayerRank { get; set; }
    }

    class Program {
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/slides.googleapis.com-dotnet-quickstart.json
        static string[] Scopes = { SlidesService.Scope.Presentations };
        static string ApplicationName = "Team Gosu Beef Bot";

        static String _presentationId = "1IEoWpZmpTAxx_HdAHYiEnpAX1cSfT-TqZYNwwIAxeTA";
        static SlidesService _service;
        static List<BeefEntry> _entries = new List<BeefEntry>();

        static String GetStringFromElements(IList<TextElement> elements) {
            if (elements == null)
                return "";

            String result = "";
            foreach (TextElement element in elements) {
                String content = element?.TextRun?.Content?.Trim();
                if (content != null)
                    result += content;
            }

            return result;
        }

        static Boolean TestDeleteFirst() {
            if (String.IsNullOrEmpty(_entries[0].PlayerName))
                return true; // Already deleted

            DeleteTextRequest deleteRequest = new DeleteTextRequest();
            deleteRequest.TextRange = new Range();
            deleteRequest.TextRange.Type = "ALL";
            deleteRequest.ObjectId = _entries[0].ObjectId;
            var requestListUpdate = new BatchUpdatePresentationRequest();
            IList<Request> requestList = new List<Request>();
            var actualRequest = new Request();
            actualRequest.DeleteText = deleteRequest;
            requestList.Add(actualRequest);
            requestListUpdate.Requests = requestList;

            PresentationsResource.BatchUpdateRequest batchRequest = _service.Presentations.BatchUpdate(requestListUpdate, _presentationId);
            BatchUpdatePresentationResponse response = batchRequest.Execute();
            return true;
        }

        static Boolean TestAddFirst() {
            InsertTextRequest insertRequest = new InsertTextRequest();
            insertRequest.InsertionIndex = 0;
            insertRequest.Text = "gamerrichy2";
            insertRequest.ObjectId = _entries[0].ObjectId;
            var requestListUpdate = new BatchUpdatePresentationRequest();
            IList<Request> requestList = new List<Request>();
            var actualRequest = new Request();
            actualRequest.InsertText = insertRequest;
            requestList.Add(actualRequest);
            requestListUpdate.Requests = requestList;

            PresentationsResource.BatchUpdateRequest batchRequest = _service.Presentations.BatchUpdate(requestListUpdate, _presentationId);
            BatchUpdatePresentationResponse response = batchRequest.Execute();
            return true;
        }

        static void Main(string[] args) {
            UserCredential credential;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read)) {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Google Slides API service.
            _service = new SlidesService(new BaseClientService.Initializer() {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Define request parameters.
            PresentationsResource.GetRequest request = _service.Presentations.Get(_presentationId);

            // Prints all the beef participants
            Presentation presentation = request.Execute();
            IList<Page> slides = presentation.Slides;
            for (var i = 0; i < slides.Count; i++) {
                Page slide = slides[i];
                IList<PageElement> elements = slide.PageElements;
                foreach (PageElement element in elements) {
                    // Check if this is a player entry. 
                    // A group of any 2 things with the second entry an integer is assumed to be a player entry.
                    if (element?.ElementGroup?.Children?.Count == 2) {
                        // Check that the second one is a number
                        IList<PageElement> children = element.ElementGroup.Children;
                        if (children[1]?.Shape?.Text?.TextElements?.Count > 0) {
                            IList<TextElement> textElements = children[1].Shape.Text.TextElements;
                            String numberInBeefStr = GetStringFromElements(textElements);
                            int rank = -1;
                            if (Int32.TryParse(numberInBeefStr, out rank)) {
                                // We have the number, now get the player name, if any
                                if (children[0]?.Shape != null) {                                    
                                    IList<TextElement> playerElements = children[0].Shape?.Text?.TextElements;
                                    String playerName = GetStringFromElements(playerElements);

                                    var entry = new BeefEntry();
                                    entry.ObjectId = children[0].ObjectId;
                                    entry.PlayerName = playerName;
                                    entry.PlayerRank = rank;
                                    _entries.Add(entry);
                                }
                            }
                        }
                    }
                }
            }

            foreach (BeefEntry entry in _entries) {
                Console.WriteLine(entry.PlayerRank + ". " + entry.PlayerName);
            }
            Console.Read();
            TestDeleteFirst();
            Console.Read();
            TestAddFirst();
        }
    }
}