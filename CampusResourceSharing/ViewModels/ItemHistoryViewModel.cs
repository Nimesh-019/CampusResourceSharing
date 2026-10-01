using CampusResourceSharing.Models;

namespace CampusResourceSharing.ViewModels
{
    public class ItemHistoryViewModel
    {
        public Item Item { get; set; } = null!;
        public List<Request> Requests { get; set; } = new();

        public int TotalRequests => Requests.Count;
        public int ApprovedRequests => Requests.Count(r => r.Status == "Accepted");
        public int RejectedRequests => Requests.Count(r => r.Status == "Rejected");
        public int PendingRequests => Requests.Count(r => r.Status == "Pending");
    }
}
