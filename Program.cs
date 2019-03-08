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
using System.Globalization;

namespace DonatePayStat
{
    class MainClass
    {
        private static int _lastId = 0;

        private static string _apiKeyFile = "ApiKey.config";

        private static readonly string _lastIdFile = "LastId.config";

        private static string _dir = @"DonateSpawn";

        private static DateTime _lastDate = DateTime.Now.Date;

        private static Dictionary<double, string> _donatConfigs = new Dictionary<double, string>();

        public static void Main(string[] args)
        {
            _donatConfigs = GetDonateConfigs();

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

            while (true)
            {
                Transactions trans = GetTransactions(apiKey, after: _lastId, type: "donation", limit: 10, status: "success");


                if (trans != null && trans.data != null && trans.data.Count > 0)
                {
                    var last = trans.data.First();
                    _lastId = last.id;

                    trans.data = trans.data.Where(d => d.created_at.date > _lastDate).ToList();
                    _lastDate = last.created_at.date;

                    GenerateSpawns(trans.data);

                    File.WriteAllText(_lastIdFile, _lastId.ToString());
                }

                Thread.Sleep(20000);
            }
        }


        private static Dictionary<double, string> GetDonateConfigs()
        {
            Dictionary<double, string> donatConfigs = new Dictionary<double, string>();

            var keys = ConfigurationManager.AppSettings.AllKeys;

            foreach (var key in keys)
            {
                double donateKey = ParseDouble(key);

                if (donateKey > 0 && donatConfigs.ContainsKey(donateKey))
                {
                    Console.WriteLine($"Duplicate key: {key}. Exist value: {donatConfigs[donateKey]}");
                }
                else
                {
                    donatConfigs.Add(donateKey, ConfigurationManager.AppSettings[key]);
                }
            }

            return donatConfigs;
        }


        private static string DeleteLastSaveGame()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var saveGameDir = Path.Combine(documents, @"Documents\My Games\Skyrim\Saves");

            if (!Directory.Exists(saveGameDir))
            {
                return null;
            }

            DirectoryInfo di = new DirectoryInfo(saveGameDir);
            if (di != null)
            {
                FileInfo[] subFiles = di.GetFiles("*.skse");
                if (subFiles.Length > 0)
                {

                    var lastSave = subFiles.FirstOrDefault(d => d.LastWriteTime == subFiles.Max(f => f.LastWriteTime));
                    var secondFile = lastSave.Name.Replace(".skse", ".ess");

                    try
                    {
                        File.Delete(lastSave.FullName);

                        var pathToSecondFile = Path.Combine(lastSave.Directory.FullName, secondFile);
                        if (File.Exists(pathToSecondFile))
                            File.Delete(pathToSecondFile);

                        return secondFile;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            return null;
        }


        private static void GenerateSpawns(List<Transactionsdata> transactions)
        {
            if (!Directory.Exists(_dir))
            {
                Directory.CreateDirectory(_dir);
            }

            transactions.Reverse();
            foreach (var tran in transactions)
            {
                var str = $"{tran.created_at.date.ToString()} Status: {tran.status}. Received {tran.sum} by {tran.what}";
                Console.WriteLine(str);
                WriteLog(str);

                _donatConfigs.TryGetValue(ParseDouble(tran.sum), out string sValue);

                if (string.IsNullOrEmpty(sValue))
                {
                    continue;
                }

                if (sValue == "DeleteLastSave")
                {
                    var deletedFile = DeleteLastSaveGame();
                    if (!string.IsNullOrEmpty(deletedFile))
                    {
                        Console.WriteLine($"Save {deletedFile} was deleted by {tran.vars.name}");
                    }

                    continue;
                }

                var allValuesByKey = sValue.Split(',');

                foreach (var item in allValuesByKey)
                {
                    var command = "";
                    var countAndComand = item.Split('=');
                    int comandCount = 0;

                    if (countAndComand.Length == 2)
                    {
                        if (!int.TryParse(countAndComand[0], out comandCount))
                        {
                            comandCount = 1;
                        }

                        command = countAndComand[1];
                    }
                    else if (countAndComand.Length == 1)
                    {
                        command = countAndComand[0];
                        comandCount = 1;
                    }

                    for (int i = 0; i < comandCount; i++)
                    {
                        string fileName = Path.Combine(_dir, DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss_ffff") + $"{i}.spawn");
                        File.WriteAllText($"{fileName}", command, Encoding.ASCII);
                    }

                    if (comandCount > 0)
                    {
                        var createdStr = $"Spawn created, command: {command}, count: {comandCount}";
                        Console.WriteLine(createdStr);
                        WriteLog(createdStr);
                    }
                }

                Console.WriteLine();
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


        private static double ParseDouble(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return 0;

            double.TryParse(str, NumberStyles.Any, CultureInfo.GetCultureInfo(1033), out double result);

            return result;
        }

        private static void WriteLog(string str)
        {
            try
            {
                File.AppendAllText("Log.txt", str + "\r\n");
            }
            catch (Exception e)
            {

            }
        }
    }


}
