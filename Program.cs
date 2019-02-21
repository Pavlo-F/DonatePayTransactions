using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Configuration;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace DonatePayStat
{
    class MainClass
    {
		private static int _lastId = 0;

		private static string _apiKeyFile = "ApiKey.config";

		private static string _lastIdFile = "LastId.config";

        private static string _dir = @"DonateSpawn";

        private static DateTime _startDate = DateTime.Now;


        public static void Main(string[] args)
        {
            if (File.Exists(_lastIdFile))
            {
                var id = File.ReadAllText(_lastIdFile);
                int.TryParse(id, out _lastId);
            }

            if (!File.Exists(_apiKeyFile))
            {
                throw new Exception($"{_apiKeyFile} not found. Create file end write API key.");
            }

            string apiKey = File.ReadAllText(_apiKeyFile);

            Transactions trans;

            while (true)
			{
				trans = GetTransactions(apiKey, after: _lastId, type: "donation");


                if (trans != null && trans.data != null && trans.data.Count > 0)
				{
                    foreach (var item in trans.data)
				    {
				        Console.WriteLine($"{DateTime.Now.ToString()} Received { item.sum } by {item.vars.name}");
                    }
                    

				    _lastId = trans.data.First().id;

                    trans.data = trans.data.Where(d => d.created_at.date.Date >= _startDate.Date).ToList();

                    GenerateSpawns(trans.data);

                    File.WriteAllText(_lastIdFile, _lastId.ToString());
				}

				Thread.Sleep(30000);
			}
        }


		private static void GenerateSpawns(List<Transactionsdata> transactions)
		{

		    if (!Directory.Exists(_dir))
		    {
		        Directory.CreateDirectory(_dir);
		    }

			foreach (var tran in transactions)
			{
				string sValue = ConfigurationManager.AppSettings[tran.sum];

				if (!string.IsNullOrEmpty(sValue))
				{
					string fileName = Path.Combine(_dir, DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss_ffff") + ".spawn");
					File.WriteAllText($"{fileName}", sValue, Encoding.ASCII);
					Console.WriteLine($"file {fileName} created");
				}
			}

		}

		public static Transactions GetTransactions(string apikey, int limit = 25, int before = 0, int after = 0, int skip = 0, string order = "DESC", string type = "all", string status = "all")
        {
            string url = "http://donatepay.ru/api/v1/transactions";
            string parameters = "";

            if (limit != 25)
                parameters += "&limit=" + limit;
            if (before != 0)
                parameters += "&before=" + before;
            if (after != 0)
                parameters += "&after=" + after;
            if (skip != 0)
                parameters += "&skip=" + skip;
            if (order != "DESC")
                parameters += "&order=" + order;
            if (type != "all")
                parameters += "&type=" + type;
            if (status != "all")
                parameters += "&status=" + status;

            string resp = GetResponse(url + "?access_token=" + apikey + parameters);

            var format = "yyyy-MM-dd HH:mm:ss.ffffff";
            var dateTimeConverter = new IsoDateTimeConverter { DateTimeFormat = format };

            Transactions u = JsonConvert.DeserializeObject<Transactions>(resp, dateTimeConverter);

            return u;
        }


		private static string GetResponse(string url)
        {
			string data = string.Empty;

			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
				request.AutomaticDecompression = DecompressionMethods.GZip;
				request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";
				using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
				using (Stream stream = response.GetResponseStream())
				using (StreamReader reader = new StreamReader(stream))
				{
					data = reader.ReadToEnd();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error GetResponse. Exception: {ex.ToString()}");
			}

			return data;
        }
    }
   

}
