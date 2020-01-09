using dsmodels;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace wallib
{
    public class wmUtility
    {
        readonly static string _logfile = "log.txt";

        /// <summary>
        /// Give a product url, parse the detail
        /// </summary>
        /// <param name="url"></param>
        /// <returns>WalItem object, null if could not fetch item</returns>
        public static async Task<SupplierItem> GetDetail(string url)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            var item = new SupplierItem();
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
                        var f = GetFulfillment(html);
                        if (f != null)
                        {
                            var arriveby = ParseArrival(f);
                            item.Arrives = arriveby;
                        }
                        item.ItemURL = url;
                        item.IsVariation = IsVariation(html);
                        item.UPC = GetUPC(html);
                        item.MPN = GetMPN(html);
                        item.SupplierBrand = GetBrand(html);
                        item.IsVERO = db.IsVERO(item.SupplierBrand);
                        item.Description = GetDescr(html);
                        item.Description = ModifyDescr(item.Description);
                        itemNo = parseItemNo(html);
                        item.ItemID = itemNo;

                        images = GetImages(html);

                        if (images.Count == 0)
                        {
                            int stop = 1;
                        }
                        else
                        {
                            item.SupplierPicURL = dsutil.DSUtil.ListToDelimited(images.ToArray(), ';');
                        }
                        bool outOfStock = false;

                        outOfStock = ParseOutOfStock(html);
                        item.OutOfStock = outOfStock;

                        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                        // 01.09.2020
                        // Regarding fetching price - at present seems that getOfferPriceDetail_thirdAttempt() is most accurate.
                        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

                        string offerPrice = wallib.wmUtility.getOfferPriceDetail(html, 0);
                        decimal price;
                        bool r = decimal.TryParse(offerPrice, out price);
                        if (!r)
                        {
                            // 01.09.2020 let's see if thirdAttempt is more accurate than 'secondAttempt'
                            offerPrice = wallib.wmUtility.getOfferPriceDetail_thirdAttempt(html, 0);
                            r = decimal.TryParse(offerPrice, out price);
                            if (r)
                            {
                                item.SupplierPrice = Convert.ToDecimal(offerPrice);
                            }
                            else
                            {
                                offerPrice = wallib.wmUtility.getOfferPriceDetail_secondAttempt(html, 0);
                                r = decimal.TryParse(offerPrice, out price);
                                if (r)
                                {
                                    item.SupplierPrice = Convert.ToDecimal(offerPrice);
                                }
                            }
                        }
                        else
                        {
                            item.SupplierPrice = Convert.ToDecimal(offerPrice);
                        }
                        bool shippingNotAvailable = ParseShippingNotAvailable(html);
                        item.ShippingNotAvailable = shippingNotAvailable;

                        item.SoldAndShippedBySupplier = FulfilledByWalmart(html);
                        if (!item.SoldAndShippedBySupplier.Value)
                        {
                            item.SoldAndShippedBySupplier = FulfilledByWalmart_method2(html);
                        }
                    }
                    else
                    {
                        item = null;
                    }
                }
            }
            catch (Exception exc)
            {
                string header = string.Format("wm GetDetail: {0}", url);
                string ret = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
                return null;
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
            bool isPNG;
            bool isJPEG;
            do
            {
                isPNG = false;
                isJPEG = false;
                int pos = toSearch.IndexOf("https://i5.walmartimages.com/asr/", nextPos);
                if (pos > -1)
                {
                    int stop = -1;
                    int stop_jpeg = toSearch.IndexOf("jpeg", pos + 1);
                    int stop_png = toSearch.IndexOf("png", pos + 1);

                    if (stop_jpeg > -1 && stop_png > -1) {
                        if (stop_jpeg < stop_png)
                        {
                            stop = stop_jpeg;
                            isJPEG = true;
                        }
                        else
                        {
                            stop = stop_png;
                            isPNG = true;
                        }
                    }
                    else if (stop_jpeg > -1)
                    {
                        stop = stop_jpeg;
                        isJPEG = true;
                    }
                    else if (stop_png > -1)
                    {
                        stop = stop_png;
                        isPNG = true;
                    }
                    if (stop > -1)
                    {
                        int offset = 0;
                        if (isJPEG)
                        {
                            offset = 4;
                        }
                        if (isPNG)
                        {
                            offset = 3;
                        }
                        string pic = toSearch.Substring(pos, stop - pos + offset);
                        images.Add(pic);
                        nextPos = stop + 1;
                    }
                    else
                    {
                        done = true;
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
            string str = dsutil.DSUtil.HTMLToString(html);
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
        protected static bool FulfilledByWalmart_method2(string html)
        {
            string str = dsutil.DSUtil.HTMLToString(html);
            const string marker = "Sold &amp; shipped byWalmart";
            int pos = str.IndexOf(marker);
            if (pos > -1)
            {
                return true;
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
            string priceMarker = "\"CURRENT\":{\"price\":";
            int endPricePos = 0;
            string offerPrice = null;

            int pricePos = html.IndexOf(priceMarker, startSearching);
            endPricePos = html.IndexOf(",", pricePos + priceMarker.Length);
            offerPrice = html.Substring(pricePos + priceMarker.Length, endPricePos - (pricePos + priceMarker.Length));
            return offerPrice;
        }
        public static string getOfferPriceDetail_secondAttempt(string html, int startSearching)
        {
            string priceMarker = "\"currentPrice\":";
            int endPricePos = 0;
            string offerPrice = null;

            int pricePos = html.IndexOf(priceMarker, startSearching);
            endPricePos = html.IndexOf(",", pricePos + priceMarker.Length);
            offerPrice = html.Substring(pricePos + priceMarker.Length, endPricePos - (pricePos + priceMarker.Length));
            return offerPrice;
        }
        public static string getOfferPriceDetail_thirdAttempt(string html, int startSearching)
        {
            string priceMarker = "itemprop=\"price\" content=\"";
            int endPricePos = 0;
            string offerPrice = null;

            int pricePos = html.IndexOf(priceMarker, startSearching);
            endPricePos = html.IndexOf("\"", pricePos + priceMarker.Length);
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
        public static string GetUPC(string html)
        {
            string UPC = null;
            string marker = "\"upc\":\"";
            try
            {
                int pos = html.IndexOf(marker);
                UPC = html.Substring(pos + marker.Length, 12);
                if (!IsValidUPC(UPC))
                {
                    return null;
                }
            }
            catch (Exception exc)
            {
                string ret = dsutil.DSUtil.ErrMsg("GetUPC", exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
            }
            return UPC;
        }
        public static bool IsValidUPC(string UPC)
        {
            if (UPC.Length != 12)
            {
                return false;
            }
            for(int i = 0; i < UPC.Length; i++)
            {
                if (!Char.IsDigit(UPC, i))
                {
                    return false;
                }
            }
            return true;
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
                            brand = WebUtility.HtmlDecode(match.InnerText);
                        }
                    }
                }
            }
            else
            {
                brand = WebUtility.HtmlDecode(node.InnerText);
            }
            return brand;
        }
        public static string GetFulfillment(string html)
        {
            string result = null;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            var node = doc.DocumentNode.SelectSingleNode("//div[@class='fulfillment-shipping-text']");
            if (node != null)
            {
                var span = node.SelectSingleNode(".//span");
                if (span != null)
                {
                    var delType = span.InnerText;
                }

                var p = node.SelectSingleNode(".//p");
                if (p != null)
                {
                    result = p.InnerText;
                }

                // result = node.InnerText;
            }
            return result;
        }

        // Arrives by Tue, Jan 7
        protected static DateTime? ParseArrival(string str)
        {
            DateTime? arrivalDate = null;
            int pos = str.IndexOf(",");
            if (pos > -1)
            {
                int month = ParseArrivalMonth(str.Substring(pos + 1));
                int year = ParseArrivalYear(month);
                int day = ParseArrivalDay(str.Substring(pos + 1));
                arrivalDate = new DateTime(year, month, day);
            }
            return arrivalDate;
        }
        protected static int ParseArrivalYear(int month)
        {
            bool inDec = (DateTime.Now.Month == 12);
            if (!inDec)
            {
                return DateTime.Now.Year;
            }
            if (month == 1)
            {
                return DateTime.Now.Year + 1;
            }
            return 0;
        }
        protected static int ParseArrivalDay(string str)
        {
            int pos = str.LastIndexOf(" ");
            if (pos > -1)
            {
                string dayStr = str.Substring(pos + 1);
                int day;
                bool success = Int32.TryParse(dayStr, out day);
                if (success)
                {
                    return day;
                }
            }
            return 0;
        }
        protected static int ParseArrivalMonth(string str)
        {
            int pos = str.IndexOf("Jan");
            if (pos > -1) return 1;

            pos = str.IndexOf("Feb");
            if (pos > -1) return 2;

            return 0;
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

        public static string ModifyDescr(string descr)
        {
            string marker = @"<div class=""product-description-disclaimer""";
            string endMarker = @"See our disclaimer </span></button></span></div></div>";

            int pos = descr.IndexOf(marker);
            int endPos = descr.IndexOf(endMarker, pos + 1) + endMarker.Length;
            string toRemove = descr.Substring(pos, endPos - pos);
            string output = descr.Replace(toRemove, "");
            return output;
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
