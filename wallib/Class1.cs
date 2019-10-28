using dsmodels;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace wallib
{
    public class Class1
    {
        /// <summary>
        /// Give a product url, parse the detail
        /// </summary>
        /// <param name="url"></param>
        /// <returns>WalItem object</returns>
        public static async Task<WalItem> GetDetail(string url)
        {
            var item = new WalItem();
            string itemNo = null;
            string descr = null;
            var images = new List<string>();

            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(url))
                using (HttpContent content = response.Content)
                {
                    // ... Read the string.
                    string result = await content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        // https://stackoverflow.com/questions/4182594/grab-all-text-from-html-with-html-agility-pack
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(result);
                        string output = null;
                        foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//text()"))
                        {
                            output += node.InnerText;
                        }

                        itemNo = parseItemNo(result);
                        item.ItemId = itemNo;

                        string marker = "\"product-short-description-wrapper\" itemprop=\"description\">";
                        descr = parseDescr(result, marker, "<");
                        if (string.IsNullOrEmpty(descr))
                        {
                            marker = "\"product_long_description\":{\"values\":[\"";
                            descr = parseDescr(result, marker, "}");
                        }
                        item.Description = descr;

                        images = ParseImages(result);
                        if (images.Count == 0)
                        {
                            int stop = 1;
                        }
                        else
                        {
                            item.PictureUrl = dsutil.DSUtil.ListToDelimited(images.ToArray(), ';');
                        }
                        //Console.WriteLine("images: " + images.Count);
                        if (item.Description.Contains("Minnie"))
                        {
                            int stop = 99;
                        }

                        bool outOfStock = false;

                        // NO, GetDetail cannot assume that a Listing record exists
                        //if (!listing.Variation)
                        //{
                        outOfStock = ParseOutOfStock(result);
                        //}
                        //else
                        //{
                        //    outOfStock = ParseOutOfStockVariation(result, listing.VariationDescription);
                        //}
                        item.OutOfStock = outOfStock;

                        string offerPrice = wallib.Class1.getOfferPriceDetail(result, 0);
                        decimal price;
                        bool r = decimal.TryParse(offerPrice, out price);
                        if (!r)
                        {
                            offerPrice = wallib.Class1.getOfferPriceDetail_secondAttempt(result, 0);
                            r = decimal.TryParse(offerPrice, out price);
                            if (r)
                            {
                                item.Price = Convert.ToDecimal(offerPrice);
                            }
                        }
                        else
                        {
                            item.Price = Convert.ToDecimal(offerPrice);
                        }
                        bool shippingNotAvailable = ParseShippingNotAvailable(result);
                        item.ShippingNotAvailable = shippingNotAvailable;

                        item.FulfilledByWalmart = FulfilledByWalmart(result);
                    }
                    else
                    {
                        item = null;
                    }
                }
            }
            catch (Exception exc)
            {
                string err = exc.Message;
            }
            return item;
        }

        protected static string parseItemNo(string html)
        {
            const string marker = "Walmart #";
            const int maxlen = 11;
            string itemNo = null;
            bool done = false;
            int pos = 0;
            int endPos = 0;

            pos = html.IndexOf(marker);
            do
            {
                if (pos > -1)
                {
                    endPos = html.IndexOf("<", pos);
                    if (endPos > -1)
                    {
                        itemNo = html.Substring(pos + marker.Length, endPos - (pos + marker.Length));
                        itemNo = itemNo.Trim();
                        if (itemNo.Length < maxlen)
                            done = true;
                        else
                        {
                            pos = html.IndexOf(marker, endPos);
                            if (pos == -1)
                                done = true;
                        }
                    }
                }
                else
                    done = true;
            }
            while (!done);

            return itemNo;
        }

        protected static List<string> ParseImages(string html)
        {
            const string marker = "assetSizeUrls\":{\"thumb\":\"";
            //const string endMarker = ".jpeg";
            const string endMarker = "odnHeight";
            int endPos;
            var images = new List<string>();
            int pos = html.IndexOf(marker);
            if (pos > -1)
            {
                bool done = false;
                do
                {
                    endPos = html.IndexOf(endMarker, pos + 1);
                    if (endPos > -1)
                    {
                        string img = html.Substring(pos + marker.Length, endPos - (pos + marker.Length) - 1);
                        images.Add(img);
                        pos = html.IndexOf(marker, endPos);
                        if (pos == -1)
                            done = true;
                    }
                    else done = true;
                } while (!done);
            }
            return images;
        }

        protected static string parseDescr(string html, string marker, string endMarker)
        {
            const int maxlen = 11;
            string descr = null;
            bool done = false;
            int pos = 0;
            int endPos = 0;

            pos = html.IndexOf(marker);
            do
            {
                if (pos > -1)
                {
                    endPos = html.IndexOf(endMarker, pos);
                    if (endPos > -1)
                    {
                        descr = html.Substring(pos + marker.Length, endPos - (pos + marker.Length));
                        descr = descr.Trim();
                        if (descr.Length < maxlen)
                            done = true;
                        else
                        {
                            pos = html.IndexOf(marker, endPos);
                            if (pos == -1)
                                done = true;
                        }
                    }
                }
                else
                    done = true;
            }
            while (!done);

            return descr;
        }

        protected static bool FulfilledByWalmart(string html)
        {
            const string marker = "\"sold_by\":{\"values\":[\"Walmart\"]";
            int pos = html.IndexOf(marker);
            if (pos > -1)
            {
                return true;
                // 10.22.2019 not sure this relevent anymore
                const string shippedMarker = "shipped by</span></span><a class=\"seller-name\" href=\"https://help.walmart.com/\"";
                pos = html.IndexOf(shippedMarker);
                return (pos > -1) ? true : false;
            }
            return false;
        }

        protected static bool ParseOutOfStock(string html)
        {
            const string marker = "Get In-Stock Alert";
            int pos = html.IndexOf(marker);
            return (pos > -1) ? true : false;
        }

        protected static int OutOfStockFound(string html)
        {
            const string marker = "Out of stock";
            int pos = 0;
            int cnt = 0;
            do
            {
                pos = html.IndexOf(marker, pos + 5);
                ++cnt;
            } while (pos > -1);
            return cnt - 1;

        }

        protected static bool ParseOutOfStockVariation(string html, string variationDescription)
        {
            string marker = variationDescription + ",  is Not available";
            int pos = html.IndexOf(marker);
            if (pos == -1)
            {
                marker = variationDescription + ",  is Out of stock";
                pos = html.IndexOf(marker);
            }
            return (pos > -1) ? true : false;
        }

        protected static bool ParseShippingNotAvailable(string html)
        {
            const string marker = "Shipping not available";
            int pos = html.IndexOf(marker);
            return (pos > -1) ? true : false;
        }

        public static string getOfferPrice(string html, int startSearching, string title)
        {
            int start = html.IndexOf(title);
            const string priceMarker = "\"offerPrice\":";
            int endPricePos = 0;
            string offerPrice = null;

            int pricePos = html.IndexOf(priceMarker, start);
            endPricePos = html.IndexOf(",", pricePos + priceMarker.Length);
            offerPrice = html.Substring(pricePos + priceMarker.Length, endPricePos - (pricePos + priceMarker.Length));
            if (offerPrice == "0")
            {
                int x = 99;
            }
            return offerPrice;
        }

        public static string getOfferPriceDetail(string html, int startSearching)
        {
            string priceMarker = "class=\"price-group\" role=\"text\" aria-label=\"$";
            priceMarker = "\"CURRENT\":{\"price\":";
            int endPricePos = 0;
            string offerPrice = null;

            int pricePos = html.IndexOf(priceMarker, startSearching);
            endPricePos = html.IndexOf(",", pricePos + priceMarker.Length);
            offerPrice = html.Substring(pricePos + priceMarker.Length, endPricePos - (pricePos + priceMarker.Length));
            return offerPrice;
        }
        public static string getOfferPriceDetail_secondAttempt(string html, int startSearching)
        {
            string priceMarker = "class=\"price-group\" role=\"text\" aria-label=\"$";
            priceMarker = "\"currentPrice\":";
            int endPricePos = 0;
            string offerPrice = null;

            int pricePos = html.IndexOf(priceMarker, startSearching);
            endPricePos = html.IndexOf(",", pricePos + priceMarker.Length);
            offerPrice = html.Substring(pricePos + priceMarker.Length, endPricePos - (pricePos + priceMarker.Length));
            return offerPrice;
        }

        public static decimal reprice(decimal price, double px_mult)
        {
            //const decimal px_mult = 1.28m;
            const decimal shippingCost = 6.0m;
            const decimal freeShipping = 35.0m;

            if (price < freeShipping) price += shippingCost;
            decimal newprice = price * (decimal)px_mult;
            return newprice;
        }

    }
}
