namespace Beef.BeefApi {
    public class GetLadderResponse {
        public GetLadderReponseEntry[] BeefLadder { get; set; }
    }

    public class GetLadderReponseEntry {
        public long Rank { get; set; }
        public String BeefName { get; set; }
        public String Race { get; set; }
        public String Mmr { get; set; }
    }
}
