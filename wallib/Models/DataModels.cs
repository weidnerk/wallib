using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace wallib.Models
{
    [Table("WalMap")]
    public class WalMap
    {
        public int ID { get; set; }
        public string EbaySeller { get; set; }
        public string EbayItemId { get; set; }
        public string WalItemId { get; set; }
        public string EbayUrl { get; set; }
        public string Title { get; set; }
        public int CategoryId { get; set; }
        public long FeedbackScore { get; set; }
        public string ImageUrl { get; set; }
        public bool IsMultiVariationListing { get; set; }
    }

    public class WalmartSearchProdIDResponse
    {
        [JsonProperty(PropertyName = "count")]
        public int Count { get; set; }
        [JsonProperty(PropertyName = "url")]
        public string URL { get; set; }
    }
}
