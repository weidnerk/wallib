using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wallib.Models
{
    [Table("WalItems")]
    public class WalItem
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string DetailUrl { get; set; }
        public int CategoryID { get; set; }
        public string ItemId { get; set; }

        public string PictureUrl { get; set; }
    }

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
}
