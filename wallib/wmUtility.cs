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
        /// But this is a problem since first has to logon to Walmart.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static async Task GetOrder(string url)
        {
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
                    }
                }
            }
            catch (Exception exc)
            {
                string header = string.Format("GetOrder", url);
                string ret = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
            }
        }

        /// <summary>
        /// Give a product url, parse the detail
        /// </summary>
        /// <param name="URL"></param>
        /// <returns>WalItem object, null if could not fetch item</returns>
        public static async Task<SupplierItem> GetDetail(string URL, int imgLimit)
        {
            // freight delivery
            // URL = "https://www.walmart.com//ip/Carolina-Chair-Table-Tavern-Pub-Bar-Table-44-Espresso/869432422";

            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            var item = new SupplierItem();
            string itemNo = null;
            string descr = null;
            var images = new List<string>();

            try
            {
                URL = CleanURL(URL);
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(URL))
                using (HttpContent content = response.Content)
                {
                    // ... Read the string.
                    string html = await content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        //if (URL == "https://www.walmart.com//ip/Mainstays-8-x-10-Black-Linear-Frame-Set-of-6/42338175")
                        //{
                        //    int stop = 99;
                        //}
                        var f = GetArrivesBy(html);
                        if (f != null)
                        {
                            var arriveby = ParseArrival(f);
                            item.Arrives = arriveby;
                        }
                        item.SourceID = 1;
                        item.ItemURL = URL;
                        images = GetImages(html, imgLimit);
                        item.IsVariation = IsVariation(html);
                        if (item.IsVariation.Value)
                        {
                            item.VariationName = GetVariationName(html);
                            item.usItemId = Collect_usItemId(URL, html);
                            item.SupplierVariation = InitSupplierVariations(URL, item.usItemId);
                            item.VariationPicURL = GetVariationImages(html, item.SupplierVariation.Count, URL);
                            FetchAndFillVariations(item.SupplierVariation, item.VariationPicURL);
                        }
                        item.IsFreightShipping = IsFreightShipping(html);
                        item.UPC = GetUPC(html);
                        item.MPN = GetMPN(html);
                        item.SupplierBrand = GetBrand(html);
                        item.IsVERO = db.IsVERO(item.SupplierBrand);
                        item.Description = GetDescr(html);
                        if (!string.IsNullOrEmpty(item.Description))
                        {
                            item.Description = ModifyDescr(item.Description);
                        }
                        else
                        {
                            string ret = "ERROR GetDetail - no description parsed for " + URL;
                            dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
                        }
                        itemNo = parseItemNo(html);
                        item.ItemID = itemNo;

                        if (images != null)
                        {
                            if (images.Count == 0)
                            {
                                int stop = 1;
                            }
                            else
                            {
                                item.SupplierPicURL = dsutil.DSUtil.ListToDelimited(images.ToArray(), ';');
                            }
                        }
                        else
                        {
                            string ret = "ERROR GetDetail - no images parsed for " + URL;
                            dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
                        }
                        bool outOfStock = false;

                        outOfStock = ParseOutOfStock(html);
                        item.OutOfStock = outOfStock;

                        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                        // 01.09.2020
                        // Regarding fetching price - at present seems that getOfferPriceDetail_thirdAttempt() is most accurate.
                        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                        item.SupplierPrice = GetPrice(html);
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
                string header = string.Format("wm GetDetail: {0}", URL);
                string ret = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
                return null;
            }
            return item;
        }
        protected static void FetchAndFillVariations(List<SupplierVariation> supplierVariation, List<string> pics)
        {
            int x = supplierVariation.Count;
            int y = pics.Count;
            int variationPicCount = y / (x + 1);
            //int variationPicCount = z / x;
            int offset = 0;

            foreach (var sv in supplierVariation)
            {
                string url = sv.URL;
                HttpClient var_client = new HttpClient();
                using (HttpResponseMessage var_response = var_client.GetAsync(url).Result)
                {
                    using (HttpContent var_content = var_response.Content)
                    {
                        string result = var_content.ReadAsStringAsync().Result;
                        var variation_price = getVariationPrice(result, sv.ItemID);
                        sv.Price = variation_price;
                        sv.Variation = GetVariation(result);
                        sv.Images = BuildVarPicList(pics, offset, variationPicCount);
                        offset += variationPicCount;
                        //GetVariationImages(result);
                    }
                }
            }
        }
        protected static List<string> BuildVarPicList(List<string> varPics, int offset, int groupNum)
        {
            var varPicList = new List<string>();
            for(int i = offset; i< (offset + groupNum); i++)
            {
                varPicList.Add(varPics[i]);
            }
            return varPicList;
        }
        protected static List<SupplierVariation> InitSupplierVariations(string URL, List<string> itemIDs)
        {
            var supplierVariation = new List<SupplierVariation>();
            foreach(string itemID in itemIDs)
            {
                var variation = new SupplierVariation();
                variation.URL = CreateVariationURL(URL, itemID);
                variation.ItemID = itemID;
                supplierVariation.Add(variation);
            }
            return supplierVariation;
            
        }
        protected static decimal? GetPrice(string html)
        {
            string offerPrice = wallib.wmUtility.getOfferPriceDetail(html, 0);
            decimal price;
            bool r = decimal.TryParse(offerPrice, out price);
            if (!r)
            {
                // 01.09.2020 let's see if thirdAttempt is more accurate than 'secondAttempt'
                offerPrice = wallib.wmUtility.getOfferPriceDetail_thirdAttempt(html, 0);
                r = decimal.TryParse(offerPrice, out price);
                if (!r)
                {
                    offerPrice = wallib.wmUtility.getOfferPriceDetail_secondAttempt(html, 0);
                    r = decimal.TryParse(offerPrice, out price);
                    if (!r)
                    {
                        return null;
                    }
                }
            }
            else
            {
                price = Convert.ToDecimal(offerPrice);
            }
            return price;
        }

        /// <summary>
        /// Collect the walmart itemIds for each variation.
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        protected static List<string> Collect_usItemId(string URL, string html)
        {
            var itemNo = new List<string>();
            string marker = "specificationHighlights";
            string usItemIdMarker = "usItemId\":\"";
            string endMarker = "\"";

            itemNo.Add(GetItemIDFromURL(URL));
            int pos = html.IndexOf(marker);
            bool done = false;
            while (!done)
            {
                int itemMarkerPos = html.IndexOf(usItemIdMarker, pos + 1);
                if (itemMarkerPos > -1)
                {
                    itemMarkerPos += usItemIdMarker.Length;
                    int itemMarkerEndPos = html.IndexOf(endMarker, itemMarkerPos);
                    string usItemId = html.Substring(itemMarkerPos, itemMarkerEndPos - itemMarkerPos);
                    itemNo.Add(usItemId);
                    pos = itemMarkerEndPos + 1;
                }
                else done = true;
            }
            return itemNo;
        }

        /// <summary>
        /// Note that when calculatematch looks for the item and finds a variation, the url returned includes the first itemId,
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        protected static string CleanURL(string url)
        {
            string newURL = url;
            string marker = "?selected=true";
            int pos = url.IndexOf(marker);
            if (pos > -1)
            {
                newURL = url.Remove(pos);
            }
            return newURL;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="URL">Original form of URL as captured by scraper and calculator, meaning has initial itemID but no selection</param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        protected static string CreateVariationURL(string URL, string itemID)
        {
            string newURL = null;
            int pos = URL.LastIndexOf("/");
            if (pos > -1)
            {
                newURL = URL.Substring(0, pos);
                newURL = newURL + "/" + itemID + "?selected=true";
            }
            return newURL; ;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="URL">Original form of URL as captured by scraper and calculator, meaning has initial itemID but no selection</param>
        /// <returns></returns>
        protected static string GetItemIDFromURL(string URL)
        {
            string itemID = null;
            int pos = URL.LastIndexOf("/");
            if (pos > -1)
            {
                itemID = URL.Substring(pos + 1, URL.Length - (pos + 1));
            }
            return itemID;
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

        /// <summary>
        /// Get the names of the variations (like 'Black', 'Brown')
        /// Run off the main page.
        /// Now unused since using GetVariation()
        /// </summary>
        /// <param name="html"></param>
        /// <param name="variationName"></param>
        /// <returns></returns>
        protected static List<string> GetVariations(string html)
        {
            string variationName = null;
            var variations = new List<string>();
            string optionMarker = "Choose an option";
            string variationNameMarker = "id=\"";
            string markerEnd = "\"";
            string variationMarker = "input type=\"radio\" aria-label=\"";

            string sectionMarker = "<div class=\"variants__list\"";
            string sectionEndMarker = "</label></div>";

            try
            {
                // cut the section that has the radio group of variations
                int sectionPos = html.IndexOf(sectionMarker);
                if (sectionPos > -1)
                {
                    int sectionEndPos = html.IndexOf(sectionEndMarker, sectionPos + 1);
                    if (sectionEndPos > -1)
                    {
                        string section = html.Substring(sectionPos, sectionEndPos - sectionPos);

                        int optionPos = html.IndexOf(optionMarker);
                        if (optionPos > -1)
                        {
                            // get the variation name such as 'Actual color'
                            int variationNamePos = html.IndexOf(variationNameMarker, optionPos);
                            if (variationNamePos > -1)
                            {
                                variationNamePos += variationNameMarker.Length;
                                int variationNameEndPos = html.IndexOf(markerEnd, variationNamePos);
                                if (variationNameEndPos > -1)
                                {
                                    variationName = html.Substring(variationNamePos, variationNameEndPos - variationNamePos);
                                    int variationPos = 1;

                                    // get the actual variations
                                    do
                                    {
                                        variationPos = section.IndexOf(variationMarker, variationPos);
                                        if (variationPos > -1)
                                        {
                                            variationPos += variationMarker.Length;
                                            int variationEndPos = section.IndexOf(markerEnd, variationPos);
                                            if (variationEndPos > -1)
                                            {
                                                string variation = section.Substring(variationPos, variationEndPos - variationPos);
                                                variations.Add(variation);
                                                variationPos = variationEndPos + 1;
                                            }
                                            else break;
                                        }
                                        else break;
                                    } while (variationPos > -1);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                string header = string.Format("GetVariations");
                string ret = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
            }
            return variations;
        }
        /// <summary>
        /// Run from selecting the variation.
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        protected static string GetVariationNameVar_notused(string html)
        {
            string marker = "variant-category-container";
            string marker2 = "label=\"";
            string endMarker = "\"";

            int pos = html.IndexOf(marker);
            int pos2 = html.IndexOf(marker2, pos);
            pos2 += marker2.Length;
            int endPos = html.IndexOf(endMarker, pos2);
            
            string variationName = html.Substring(pos2, endPos - pos2);
            return variationName;
        }
        /// <summary>
        /// Get variation from selected variation.
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        protected static string GetVariation(string html)
        {
            string marker = "Current selection is: ";
            string endMarker = "\"";

            int pos = html.IndexOf(marker);
            pos += marker.Length;
            int endPos = html.IndexOf(endMarker, pos);

            string variation = html.Substring(pos, endPos - pos);
            return variation;
        }
        protected static string GetVariationName(string html)
        {
            string variationName = null;
            var variations = new List<string>();
            string optionMarker = "Choose an option";
            string variationNameMarker = "id=\"";
            string markerEnd = "\"";

            string sectionMarker = "<div class=\"variants__list\"";
            string sectionEndMarker = "</label></div>";

            try
            {
                // cut the section that has the radio group of variations
                int sectionPos = html.IndexOf(sectionMarker);
                if (sectionPos > -1)
                {
                    int sectionEndPos = html.IndexOf(sectionEndMarker, sectionPos + 1);
                    if (sectionEndPos > -1)
                    {
                        string section = html.Substring(sectionPos, sectionEndPos - sectionPos);

                        int optionPos = html.IndexOf(optionMarker);
                        if (optionPos > -1)
                        {
                            // get the variation name such as 'Actual color'
                            int variationNamePos = html.IndexOf(variationNameMarker, optionPos);
                            if (variationNamePos > -1)
                            {
                                variationNamePos += variationNameMarker.Length;
                                int variationNameEndPos = html.IndexOf(markerEnd, variationNamePos);
                                if (variationNameEndPos > -1)
                                {
                                    variationName = html.Substring(variationNamePos, variationNameEndPos - variationNamePos);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                string header = string.Format("GetVariationName");
                string ret = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
            }
            return variationName;
        }
        protected static bool IsFreightShipping(string html)
        {
            const string marker = "freight delivery</span>";
            int pos = html.IndexOf(marker);
            if (pos == -1)
            {
                return false;
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

        /// <summary>
        /// TODO: why search for JPEG or PNG?  Just search for comma.
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        protected static List<string> GetImages(string html, int imgLimit)
        {
            var images = new List<string>();
            string startMarker = "Brand Link";
            string endMarker = "personalizationData";
            int startPos = 0;
            int endPos = 0;

            startPos = html.IndexOf(startMarker);
            if (startPos > -1)
            {
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

                        if (stop_jpeg > -1 && stop_png > -1)
                        {
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
            }
            else
            {
                return null;
            }
            if (images.Count > imgLimit)
            {
                do
                {
                    images.RemoveAt(imgLimit);
                } while (images.Count > imgLimit);
            }
            return images;
        }

        protected static List<string> GetVariationImages(string html, int numVariations, string URL)
        {
            var images = new List<string>();
            string endMarker = "Brand Link";
            string startMarker = "AVAILABLE";

            try
            {
                int endMarkerPos = html.IndexOf(endMarker);
                if (endMarkerPos > -1)
                {
                    int startMarkerPos = html.LastIndexOf(startMarker, endMarkerPos);
                    if (startMarkerPos > -1)
                    {
                        int pos = 0;
                        int startPos = startMarkerPos;
                        do
                        {
                            pos = html.IndexOf("https://i5.walmartimages.com/asr/", startPos);
                            if (pos > -1)
                            {
                                int endPos = html.IndexOf(",", pos);
                                if (endPos > -1)
                                {
                                    string imgName = html.Substring(pos, (endPos - pos - 1)).Replace("\"", string.Empty).Replace("}", string.Empty).Replace("]", string.Empty);
                                    images.Add(imgName);
                                    startPos = endPos + 1;
                                }
                            }
                        } while (pos > -1);
                    }
                }
                for (int i = 0; i < numVariations; i++)
                {
                    images.RemoveAt(0);
                }
            }
            catch (Exception exc)
            {
                string header = string.Format("GetVariationImages: " + URL);
                string ret = dsutil.DSUtil.ErrMsg(header, exc);
                dsutil.DSUtil.WriteFile(_logfile, ret, "admin");
            }
            return images;
        }

        /// It looks like this method still holds true for variations
        /// To get the individual variation images such backwards from startMarker for \"swatch\":
        /// After the actual image URL, you'll see a phrase like \"images\":0
        /// When you get to '0' that's the first image.

        protected static List<string> GetVariationImages_notused(string html)
        {
            var images = new List<string>();
            string endMarker = "Brand Link";
            string startMarker = "AVAILABLE";

            int endMarkerPos = html.IndexOf(endMarker);
            if (endMarkerPos > -1)
            {
                int startMarkerPos = html.LastIndexOf(startMarker, endMarkerPos);
                if (startMarkerPos > -1)
                {
                    int pos = 0;
                    int startPos = startMarkerPos;
                    do
                    {
                        pos = html.IndexOf("https://i5.walmartimages.com/asr/", startPos);
                        if (pos > -1)
                        {
                            int endPos = html.IndexOf(",", pos);
                            if (endPos > -1)
                            {
                                string imgName = html.Substring(pos, (endPos - pos - 1)).Replace("\"", string.Empty).Replace("}", string.Empty).Replace("]", string.Empty);
                                images.Add(imgName);
                                startPos = endPos + 1;
                            }
                        }
                    } while (pos > -1);
                }
            }
            return images;
        }

        /// <summary>
        /// Variations have model numbers and then you can construct it's URL.
        /// Note that you have to use GetBaseURL() to get the landing page URL and thus model number.
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        protected static List<string> GetModelNumbers(string html)
        {
            var modelNumbers = new List<string>();
            string startMarker = "specificationHighlights";
            string modelNumberMarker = "usItemId";

            int startPos = html.IndexOf(startMarker);
            int modelNumberPos = 0;
            do
            {
                modelNumberPos = html.IndexOf(modelNumberMarker, startPos);
                if (modelNumberPos > -1)
                {
                    int endPos = html.IndexOf(",", modelNumberPos + 1);
                    string modelNumber = html.Substring(modelNumberPos + modelNumberMarker.Length + 3, endPos - (modelNumberPos + modelNumberMarker.Length + 5));
                    startPos = endPos + 1;
                    modelNumbers.Add(modelNumber);
                }
            } while (modelNumberPos > -1);
            return modelNumbers;
        }

        /// <summary>
        /// The landing page of the product of a variation is on the first variation which this provides (and already parsed by the scraper).
        /// This method gets the "landing page" URL as well so we can then extract the model number.
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        protected static string GetBaseURL(string html)
        {
            string marker = "\"canonicalUrl\":\"";
            int pos = html.IndexOf(marker);
            int endPos = html.IndexOf(",", pos + 1);
            string baseURL = html.Substring(pos + marker.Length, endPos - (pos + marker.Length + 1));
            return baseURL;
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
        public static decimal? getVariationPrice(string html, string itemId)
        {
            string priceMarker = "\"itemId\":\"" + itemId + "\",\"price\":";
            int endPricePos = 0;
            string offerPrice = null;

            int pricePos = html.IndexOf(priceMarker);
            endPricePos = html.IndexOf(",", pricePos + priceMarker.Length);
            offerPrice = html.Substring(pricePos + priceMarker.Length, endPricePos - (pricePos + priceMarker.Length));

            decimal price;
            bool r = decimal.TryParse(offerPrice, out price);
            if (r)
            {
                return price;
            }
            else return null;
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
        public static string GetArrivesBy(string html)
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

            pos = str.IndexOf("Mar");
            if (pos > -1) return 3;

            pos = str.IndexOf("Apr");
            if (pos > -1) return 4;

            pos = str.IndexOf("May");
            if (pos > -1) return 5;

            pos = str.IndexOf("Jun");
            if (pos > -1) return 6;

            pos = str.IndexOf("Jul");
            if (pos > -1) return 7;

            pos = str.IndexOf("Aug");
            if (pos > -1) return 8;

            pos = str.IndexOf("Sep");
            if (pos > -1) return 9;

            pos = str.IndexOf("Oct");
            if (pos > -1) return 10;

            pos = str.IndexOf("Nov");
            if (pos > -1) return 11;

            pos = str.IndexOf("Dec");
            if (pos > -1) return 12;

            return 0;
        }
        public static string GetDescr(string html)
        {
            string marker = @"<div id=""product-about""";
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

        /// <summary>
        /// Clean up description best we can.
        /// </summary>
        /// <param name="descr"></param>
        /// <returns></returns>
        public static string ModifyDescr(string descr)
        {
            // Remove product disclaimer
            string marker = @"<div class=""product-description-disclaimer""";
            string endMarker = @"See our disclaimer </span></span></button></span></div></div>";

            int pos = descr.IndexOf(marker);
            int endPos = descr.IndexOf(endMarker, pos + marker.Length);
            if (endPos > -1)
            {
                endPos += endMarker.Length;
                string toRemove = descr.Substring(pos, endPos - pos);
                string output = descr.Replace(toRemove, "");
                return output;
            }
            return descr;
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

        protected static decimal wmBreakEvenPrice(decimal supplierPrice, decimal minFreeShipping, decimal shipping, double eBayPct)
        {
            if (supplierPrice < minFreeShipping)
            {
                supplierPrice += shipping;
            }
            decimal p = (supplierPrice + 0.30m) / (1m - 0.029m - ((decimal)eBayPct * 0.01m));
            return p;
        }

        /// <summary>
        /// https://community.ebay.com/t5/Selling/Excel-Spreadsheet-formula-to-break-even-with-eBay-sales/qaq-p/23249463
        /// Markup b/e price by pctProfit percent
        /// </summary>
        /// <param name="supplierPrice"></param>
        /// <returns></returns>
        public static PriceProfit wmNewPrice(decimal supplierPrice, double pctProfit, decimal shippingCost, decimal freeShippingMin, double eBayPct)
        {
            decimal breakeven = wmBreakEvenPrice(supplierPrice, freeShippingMin, shippingCost, eBayPct);
            var proposePrice = breakeven * (1m + ((decimal)pctProfit * 0.01m));
            return new PriceProfit { BreakEven = breakeven, ProposePrice = proposePrice };
        }
        /// <summary>
        /// Server side supplier item validation - is this where function belongs?  Yes, bcs supplier like walmart has different return policy for computers, cameras.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="allowedDeliveryDays"></param>
        public static void CanList(SupplierItem item, int allowedDeliveryDays)
        {
            if (item.Arrives.HasValue)
            {
                int days = dsutil.DSUtil.GetBusinessDays(DateTime.Now, item.Arrives.Value);
                if (days > allowedDeliveryDays)
                {
                    item.CanList = "slow shipping and handling";
                }
            }
            if (item.IsFreightShipping.HasValue)
            {
                if(item.IsFreightShipping.Value)
                {
                    item.CanList = "has freight shipping";
                }
            }
            if (item.OutOfStock)
            {
                item.CanList = "out of stock";
            }
            if (item.SoldAndShippedBySupplier.HasValue)
            {
                if (!item.SoldAndShippedBySupplier.Value)
                {
                    item.CanList = "not fulfilled by supplier";
                }
            }
            if (item.IsVERO.HasValue)
            {
                if (item.IsVERO.Value)
                {
                    item.CanList = "VERO branded";
                }
            }
            bool isComputerCamera = IsCameraComputer(item.Description);
            if (isComputerCamera)
            {
                item.CanList = "walmart item is computer/camera";
            }
        }

        /// <summary>
        /// Walmart only offers 14 day returns on computer and cameras, however this does not apply to printers.
        /// We would prefer to start by searching title but presently don't have title so try description.
        /// </summary>
        /// <param name="description"></param>
        /// <returns></returns>
        protected static bool IsCameraComputer(string description)
        {
            bool ret = false;
            string computerMarker = "computer";
            string computerMarker2 = "laptop";
            string cameraMarker = "camera";

            int pos = description.ToUpper().IndexOf(computerMarker.ToUpper());
            if (pos == -1)
            {
                pos = description.ToUpper().IndexOf(computerMarker2.ToUpper());
                if (pos == -1)
                {
                    pos = description.ToUpper().IndexOf(cameraMarker.ToUpper());
                    if (pos > -1)
                    {
                        ret = true;
                    }
                }
                else
                {
                    ret = true;
                }
            }
            else
            {
                ret = true;
            }
            return ret;
        }
    }
}
