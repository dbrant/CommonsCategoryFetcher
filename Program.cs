using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace CommonsCategoryFetcher
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string category = null;
            int maxItems = 100;
            int thumbWidth = 640;
            string outDir = "";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--category") && i < args.Length - 1)
                {
                    category = args[i + 1];
                }
                else if (args[i].Equals("--maxitems") && i < args.Length - 1)
                {
                    maxItems = Convert.ToInt32(args[i + 1]);
                }
                else if (args[i].Equals("--thumbwidth") && i < args.Length - 1)
                {
                    thumbWidth = Convert.ToInt32(args[i + 1]);
                }
                else if (args[i].Equals("--outdir") && i < args.Length - 1)
                {
                    outDir = args[i + 1];
                }
            }
            if (category == null)
            {
                Console.WriteLine("usage: --category [category] --maxitems [100] --thumbwidth [640] --outdir .");
                return;
            }

            try
            {
                string continueStr = null;
                List<Page> pages = new List<Page>();

                Console.WriteLine("Getting titles for category: " + category);
                do
                {
                    string responseStr = getResponseForCategory(category, 50, thumbWidth, continueStr);
                    ApiResponse response = JsonConvert.DeserializeObject<ApiResponse>(responseStr);

                    if (response.cont != null)
                    {
                        continueStr = response.cont.gcmcontinue;
                    }
                    else
                    {
                        continueStr = null;
                    }
                    if (response.query != null && response.query.pages != null)
                    {
                        foreach (Page page in response.query.pages)
                        {
                            if (page.imageinfo == null)
                            {
                                continue;
                            }

                            if (pages.Find(p => p.title.Equals(page.title)) == null)
                            {
                                pages.Add(page);
                            }
                        }
                    }
                    else
                    {
                        throw new ApplicationException("Failed to retrieve content.");
                    }
                } while (pages.Count < maxItems && continueStr != null);


                int fileIndex = 0;

                foreach (Page page in pages)
                {
                    Console.WriteLine("Downloading: " + page.title);
                    
                    saveSingleImage(category, outDir, Guid.NewGuid().ToString() + ".jpg", page.imageinfo[0].thumburl);
                    fileIndex++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        /*
        Sandbox link:
        https://commons.wikimedia.org/wiki/Special:ApiSandbox#action=query&format=json&prop=imageinfo&list=&generator=categorymembers&formatversion=2&iiprop=timestamp%7Cuser%7Curl&iiurlwidth=640&gcmtitle=Category%3Aboletus+edulis&gcmprop=ids%7Ctitle&gcmnamespace=&gcmtype=file&gcmlimit=300
        */

        private static string getResponseForCategory(string category, int maxItems, int thumbWidth, string continueStr)
        {
            string url = "https://commons.wikimedia.org/w/api.php?action=query&format=json&prop=imageinfo&list=&generator=categorymembers&formatversion=2&iiprop=timestamp%7Cuser%7Curl&iiurlwidth="
                + thumbWidth + "&gcmtitle=Category%3A" + Uri.EscapeDataString(category)
                + "&gcmprop=ids%7Ctitle&gcmnamespace=&gcmtype=file&gcmlimit=" + maxItems;

            if (continueStr != null)
            {
                url += "&gcmcontinue=" + continueStr;
            }

            var myReq = (HttpWebRequest)WebRequest.Create(url);
            var myResp = (HttpWebResponse)myReq.GetResponse();
            var reader = new StreamReader(myResp.GetResponseStream());
            return reader.ReadToEnd();
        }

        private static void saveSingleImage(string category, string outDir, string fileTitle, string url)
        {
            string categoryDir = (outDir == null || outDir.Length == 0) ? category.Replace(" ", "") : outDir;
            if (!Directory.Exists(categoryDir))
            {
                Directory.CreateDirectory(categoryDir);
            }
            var myReq = (HttpWebRequest)WebRequest.Create(url);
            var myResp = (HttpWebResponse)myReq.GetResponse();

            string fileName = fileTitle.Replace("File:", "").Replace(" ", "_").Replace(":", "_").Replace("-", "");
            using (FileStream fs = File.Create(categoryDir + Path.DirectorySeparatorChar + fileName))
            {
                byte[] bytes = new byte[myResp.ContentLength];
                int bytesLeft = bytes.Length;
                while (bytesLeft > 0)
                {
                    int bytesRead = myResp.GetResponseStream().Read(bytes, 0, bytes.Length);
                    fs.Write(bytes, 0, bytesRead);
                    bytesLeft -= bytesRead;
                }
            }
        }
    }

    public class ApiResponse
    {
        public Query query;

        [JsonProperty(PropertyName = "continue")]
        public Continue cont;
    }

    public class Query
    {
        public Page[] pages;
    }

    public class Page
    {
        public int pageid;
        public int ns;
        public string title;

        public ImageInfo[] imageinfo;
    }

    public class ImageInfo
    {
        public string thumburl;
    }

    public class Continue
    {
        public string gcmcontinue;
    }
}
