using dsmodels;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using wallib.Models;

namespace wallib
{
    public class wmUtility
    {
        /// <summary>
        /// Give a product url, parse the detail
        /// </summary>
        /// <param name="url"></param>
        /// <returns>WalItem object, null if could not fetch item</returns>
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
                    string html = await content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        item.IsVariation = IsVariation(html);
                        item.MPN = GetMPN(html);
                        item.Brand = GetBrand(html);
                        item.Description = GetDescr(html);
                        itemNo = parseItemNo(html);
                        item.ItemId = itemNo;

                        //string marker = "\"product-short-description-wrapper\" itemprop=\"description\">";
                        //descr = parseDescr(html, marker, "<");
                        //if (string.IsNullOrEmpty(descr))
                        //{
                        //    marker = "\"product_long_description\":{\"values\":[\"";
                        //    descr = parseDescr(html, marker, "}");
                        //}
                        //item.Description = descr;

                        images = GetImages(html);

                        // images = ParseImages(result);
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
                        outOfStock = ParseOutOfStock(html);
                        //}
                        //else
                        //{
                        //    outOfStock = ParseOutOfStockVariation(result, listing.VariationDescription);
                        //}
                        item.OutOfStock = outOfStock;

                        string offerPrice = wallib.wmUtility.getOfferPriceDetail(html, 0);
                        decimal price;
                        bool r = decimal.TryParse(offerPrice, out price);
                        if (!r)
                        {
                            offerPrice = wallib.wmUtility.getOfferPriceDetail_secondAttempt(html, 0);
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
                        bool shippingNotAvailable = ParseShippingNotAvailable(html);
                        item.ShippingNotAvailable = shippingNotAvailable;

                        item.FulfilledByWalmart = FulfilledByWalmart(html);
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
        protected static bool IsVariation(string html)
        {
            int pos = html.IndexOf("Choose an option");
            if (pos == -1)
            {
                pos = html.IndexOf("Actual Color:");
                if (pos == -1)
                {
                    pos = html.IndexOf("Size:");
                    if (pos == -1)
                    {
                        pos = html.IndexOf("Count:");
                        return (pos > -1) ? true : false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
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

        protected static List<string> GetImages_xpath(string url)
        {
            HtmlAgilityPack.HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument doc = web.Load(url);
            string x = "/html[@class='wf-myriadpro-n4-loading wf-myriadpro-n6-loading wf-myriadpro-n7-loading wf-loading']/body/div[@class='js-content']/div[@class='page-wrapper']/div[@class='page-full-wrapper']/div[@class='js-body-content']/div[@class='ProductPage-verticalTheme-standard ProductPage-verticalId-standard']/div[@class='atf-content']/div/div[1]/div[@class='ResponsiveContainer ny-atf-container mweb-enhanced-atf']/div/div[@class='TempoZoneLayout TempoZoneLayout-navbar']/div/div[@class='Grid']/div[@class='product-atf']/div[@class='Grid bot-dweb']/div[@class='Grid-col u-size-1-2-m'][2]/div[@class='hf-Bot']/h1[@class='prod-ProductTitle font-normal']";
            var r = doc.DocumentNode.SelectSingleNode(x);
            var t = r.InnerText;

            return null;
        }

        protected static List<string> GetImages(string html)
        {
            var images = new List<string>();
            string startMarker = "Brand Link";
            string endMarker = "personalizationData";
            int startPos = 0;
            int endPos = 0;

            startPos = html.IndexOf(startMarker);
            endPos = html.IndexOf(endMarker, startPos);
            string toSearch = html.Substring(startPos, endPos - startPos);

            int nextPos = 0;
            bool done = false;
            do
            {
                int pos = toSearch.IndexOf("https://i5.walmartimages.com/asr/", nextPos);
                if (pos > -1)
                {
                    int stop = toSearch.IndexOf("jpeg", pos + 1);
                    if (stop > -1)
                    {
                        string pic = toSearch.Substring(pos, stop - pos + 4);
                        images.Add(pic);
                        nextPos = stop + 1;
                    }
                    else
                    {
                        stop = toSearch.IndexOf("png", pos + 1);
                        if (stop > -1)
                        {
                            string pic = toSearch.Substring(pos, stop - pos + 3);
                            images.Add(pic);
                            nextPos = stop + 1;
                        }
                        else
                        {
                            // had weird case where the image url was a png file but the image was a black square - some glitch on their site
                            done = true;
                        }
                    }
                }
                else
                {
                    done = true;
                }
            } while (!done);

            return images;
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
        public static string GetMPN(string html)
        {
            /*
             * Find div with class 'Specification-container'
             * Then look for text, 'Manufacturer Part Number', which will be td element.
             * Take text of next td element.
             */
            string MPN = null;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            var node = doc.DocumentNode.SelectSingleNode("//div[@class='Specification-container']");
            if (node != null)
            {
                var part = node.SelectSingleNode("//*[text()[contains(., 'Manufacturer Part Number')]]");
                if (part != null)
                {
                    var match = part.NextSibling;
                    if (match != null)
                    {
                        MPN = match.InnerText;
                    }
                }
            }
            return MPN;
        }
        /// <summary>
        /// https://html-agility-pack.net/knowledge-base/30873180/csharp-htmlagilitypack-htmlnodecollection-selectnodes-not-working
        /// https://stackoverflow.com/questions/30861203/html-agility-pack-cannot-find-element-using-xpath-but-it-is-working-fine-with-we
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string GetBrand(string html)
        {
            /*
             * Find div with class 'Specification-container'
             * Then look for text, 'Manufacturer Part Number', which will be td element.
             * Take text of next td element.
             */
            string brand = null;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            HtmlNode node;

            node = doc.DocumentNode.SelectSingleNode("//a[@class='prod-brandName']");
            if (node == null)
            {
                node = doc.DocumentNode.SelectSingleNode("//div[@id='specifications']");
                if (node == null)
                {
                    node = doc.DocumentNode.SelectSingleNode("//div[@class='HomeSpecifications text-left']");
                    node = doc.DocumentNode.SelectSingleNode("//table[@class='table table-striped-odd specification']");
                    if (node == null)
                    {
                        node = doc.DocumentNode.SelectSingleNode("//div[@class='Specification-containter']");
                        if (node == null)
                        {
                            node = doc.DocumentNode.SelectSingleNode("//div[@class='AboutDescriptionWrapper']");
                        }
                    }
                }
                if (node != null)
                {
                    var part = node.SelectSingleNode("//*[text()[contains(., 'Brand')]]");
                    part = node.SelectSingleNode("//td[text()='Brand']");
                    if (part != null)
                    {
                        var match = part.NextSibling;
                        if (match != null)
                        {
                            brand = match.InnerText;
                        }
                    }
                }
            }
            else
            {
                brand = node.InnerText;
            }
            
            return brand;
        }
        public static string GetDescr(string html)
        {
            string result = null;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            //var node = doc.DocumentNode.SelectSingleNode("//div[@class='about-desc']");
            var node = doc.DocumentNode.SelectSingleNode("//div[@id='about-product-section']");
            if (node != null)
            {
                result = node.InnerHtml;
            }
            return result;
        }

        protected static string SearchUrl(string search)
        {
            string url = string.Format("https://www.walmart.com/search/?query={0}", search);
            return url;
        }

        public static bool SearchMatch(string url)
        {
            return false;
        }

        /// <summary>
        /// Give a search term like a UPC or MPN, search walmart
        /// </summary>
        /// <param name="search"></param>
        public static WalmartSearchProdIDResponse SearchProdID(string search)
        {
            var searchResponse = new WalmartSearchProdIDResponse();
            string url = SearchUrl(search);

            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage response = client.GetAsync(url).Result)
                {
                    using (HttpContent content = response.Content)
                    {
                        string result = content.ReadAsStringAsync().Result;
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(result);

                        var nodes = doc.DocumentNode.SelectNodes("//a[@class='product-title-link line-clamp line-clamp-2']");
                        if (nodes != null)
                        {
                            searchResponse.Count = (byte)nodes.Count;
                            var h = nodes[0].GetAttributeValue("href", "");
                            string detailUrl = "https://www.walmart.com/" + h;
                            searchResponse.URL = detailUrl;
                        }
                    }
                }
            }
            return searchResponse;
        }
    }
}
