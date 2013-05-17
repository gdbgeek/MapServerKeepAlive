using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Configuration;
using System.Threading;
using System.Linq;

namespace MapServerKeepALive
{
    class Program
    {
        static void Main(string[] args)
        {
            string home = Directory.GetCurrentDirectory();

            string Tserver = "";
            string server = "";

            string TTokenURL = "";
            string TUser = "";
            string TDomain = "";
            string TPassword = "";

            string Tretry = "";
            int retry = 1;

            string Txy = "";
            string Tx = "";
            string Ty = "";

            double x = 0;
            double y = 0;

            bool export_map = false;

            int c = args.GetUpperBound(0);

            // Loop through arguments
            for (int n = 0; n < c; n++)
            {
                string thisKey = args[n].ToLower();
                string thisVal = args[n + 1].TrimEnd().TrimStart();

                // eval the key
                switch (thisKey)
                {
                    case "-server":
                        Tserver = thisVal;
                        break;
                    case "-retry":
                        Tretry = thisVal;
                        break;
                    case "-exportmap":
                        Txy = thisVal;
                        if (Txy != "") export_map = true;
                        string[] coord = thisVal.Split(';');
                        Tx = coord[0];
                        Ty = coord[1];                        
                        break;
                    case "-user":
                        TUser = thisVal;
                        break;
                    case "-password":
                        TPassword = thisVal;
                        break;
                    case "-tokenurl":
                        TTokenURL = thisVal;
                        break;
                    case "-domain":
                        TDomain = thisVal;
                        break;
                    default:
                        break;
                }
            }

            if (Tserver == "") return;

            server = Tserver;

            if (Tretry != "") retry = Convert.ToInt32(Tretry);

            if (Tx != "" && Ty != "")
            {
                x = Convert.ToDouble(Tx);
                y = Convert.ToDouble(Ty);
            }

            string Token = "";

            if (TTokenURL != "" && TPassword != "" && TUser != "")
            {
                Token = GetToken(TTokenURL, TUser, TPassword);
                if (Token.Contains("Token Error:"))
                {
                    Console.WriteLine(Token);
                    Environment.Exit(-1);
                }
            }

            NetworkCredential webcredentials = new NetworkCredential();

            if (TPassword != "" && TUser != "" && TDomain != "")
            {
                webcredentials.UserName = TUser;
                webcredentials.Password = TPassword;
                webcredentials.Domain = TDomain;
            } 

            WebClient client = new WebClient();
            if (webcredentials.Domain != "") client.Credentials = webcredentials;          

            string json = "";

            try
            {
                if (Token != "")
                {
                    json = client.DownloadString(new Uri(server + "?f=json&token=" + Token));
                }
                else
                {                    
                    json = client.DownloadString(new Uri(server + "?f=json"));
                }
            }            

            catch (WebException webEx)
            {
                if (webEx.Message.Contains("401")) Console.WriteLine("Server returned a 401, did you forget to include your windows login details?");
                Console.WriteLine(webEx.Message);
                Environment.Exit(-1);
            }

            if (json.ToLower().Contains("error"))
            {
                Console.WriteLine(json);
                Environment.Exit(-1);
            }

            client.Dispose();

            Hashtable root;
            ArrayList folders;
            ArrayList services;

            root = (Hashtable)Procurios.Public.JSON.JsonDecode(json);
            folders = (ArrayList)root["folders"];
            services = (ArrayList)root["services"];

            ProcessFolder(server, retry, services, export_map, x, y, Token, webcredentials);

            int folder_count = folders.Count;

            for (int n = 0; n < folder_count; n++)
            {
                string folder = (string)folders[n];

                client = new WebClient();
                if (webcredentials.Domain != "") client.Credentials = webcredentials;

                json = "";

                try
                {
                    if (Token != "")
                    {
                        json = client.DownloadString(new Uri(server + "/" + folder + "?f=json&token=" + Token));
                    }
                    else
                    {
                        json = client.DownloadString(new Uri(server + "/" + folder + "?f=json"));
                    }
                }
                catch (WebException webEx)
                {
                    Console.WriteLine(webEx.Message);
                }

                if (json.ToLower().Contains("error"))
                {
                    Console.WriteLine(json);
                }

                client.Dispose();

                root = (Hashtable)Procurios.Public.JSON.JsonDecode(json);
                services = (ArrayList)root["services"];

                ProcessFolder(server, retry, services, export_map, x, y, Token, webcredentials);
            }

            Console.WriteLine("Done!");
        }

        public static bool ProcessFolder(string server, int retry, ArrayList services, bool export_map, double x, double y, string Token, NetworkCredential webcredentials)
        {
            int service_count = services.Count;

            for (int n = 0; n < service_count; n++)
            {
                Hashtable service = (Hashtable)services[n];

                string mstype = (string)service["type"];
                string msname = (string)service["name"];

                if (mstype == "MapServer")
                {
                    int c = 0;
                    bool result = false;

                    while (result == false && c <= retry)
                    {
                        if (c > 0)
                        {
                            Console.WriteLine("Retry #" + c.ToString() + " in 5 seconds");
                            Thread.Sleep(5000);
                        }
                        result = PingMapServer(server + "/" + msname + "/" + mstype, Token, webcredentials);

                        if (export_map == true) result = ExportMapServer(server + "/" + msname + "/" + mstype, x, y, Token, webcredentials);

                        c++;                    
                    }
                }
            }

            return true;

        }

        public static bool PingMapServer(string mapserver, string Token, NetworkCredential webcredentials)
        {
            WebClient client = new WebClient();
            if (webcredentials.Domain != "") client.Credentials = webcredentials;

            Stopwatch sw = Stopwatch.StartNew();

            string json = "";

            try
            {
                if (Token != "")
                {
                    json = client.DownloadString(new Uri(mapserver + "?f=json&token=" + Token));
                }
                else
                {
                    json = client.DownloadString(new Uri(mapserver + "?f=json"));
                }
            }
            catch (WebException webEx)
            {
                Console.WriteLine("Failed to ping " + mapserver + "");  
                Console.WriteLine(webEx.Message);
                return false;
            }

            if (json.ToLower().Contains("error"))
            {
                Console.WriteLine("Failed to ping " + mapserver + ""); 
                Console.WriteLine(json);
                return false;
            }            

            sw.Stop();

            client.Dispose();

            double seconds = sw.ElapsedMilliseconds;

            Console.WriteLine("Successfully pinged " + mapserver + " in " + seconds.ToString() + "ms");            

            return true;
        }

        public static bool ExportMapServer(string mapserver, double x, double y, string Token, NetworkCredential webcredentials)
        {
            string home = Directory.GetCurrentDirectory();

            string ExeFriendlyName = System.AppDomain.CurrentDomain.FriendlyName;
            string[] ExeNameBits = ExeFriendlyName.Split('.');
            string ExeName = ExeNameBits[0];

            WebClient client = new WebClient();
            if (webcredentials.Domain != "") client.Credentials = webcredentials;

            double XMin = x - 1000;
            double XMax = x + 1000;

            double YMin = y - 1000;
            double YMax = y + 1000;

            string imgurl = mapserver + "/export?";

            imgurl = imgurl + "dpi=96";
            imgurl = imgurl + "&size=500,500";
            imgurl = imgurl + "&f=image";
            imgurl = imgurl + "&format=png32";
            imgurl = imgurl + "&bbox=" + XMin.ToString() + "," + YMin.ToString() + "," + XMax.ToString() + "," + YMax.ToString();

            if (Token != "")
            {
                imgurl = imgurl + "&token=" + Token;
            }

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                client.DownloadFile(new Uri(imgurl), home + "\\TempImage.png");
            }
            catch (WebException webEx)
            {
                Console.WriteLine(imgurl + " " + webEx.Message);
            }

            sw.Stop();

            double seconds = sw.ElapsedMilliseconds;

            client.Dispose();

            try
            {
                //Check if the file is bigger than 1K
                FileInfo FI = new FileInfo(home + "\\TempImage.png");
                long FL = FI.Length;
                if (FL < 1000)
                {
                    seconds = 99999; //Probably timeout or error
                    Console.WriteLine("Failed to export map " + mapserver + " in " + seconds.ToString() + "ms");
                    return false;
                }
                else
                {
                    Console.WriteLine("Successfully exported map " + mapserver + " in " + seconds.ToString() + "ms");
                    return true;
                }
            }
            catch
            {
                seconds = 99999; //Probably timeout or error
                Console.WriteLine("Failed to export map " + mapserver + " in " + seconds.ToString() + "ms");
                return false;
            }           
        }

        public static string GetToken(string tokenurl, string username, string password)
        {
            string url = tokenurl + "?request=getToken&username=" + username + "&password=" + password;

            System.Net.WebRequest request = System.Net.WebRequest.Create(url);

            string myToken = "";

            try
            {
                System.Net.WebResponse response = request.GetResponse();
                System.IO.Stream responseStream = response.GetResponseStream();
                System.IO.StreamReader readStream = new System.IO.StreamReader(responseStream);

                myToken = readStream.ReadToEnd();
            }

            catch (WebException we)
            {
                myToken = "Token Error: " + we.Message;
            }

            return myToken;
        }


    }
}
