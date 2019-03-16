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
        private static string _apiKeyFile = "ApiKey.config";

        private static string _dir = @"DonateSpawn";

        private static Dictionary<double, string> _donatConfigs = new Dictionary<double, string>();

        private static string _logFile = $"Log_{DateTime.Now.Date.ToString("dd_MM_yyyy")}.txt";

        private static int _limit = 25;

        private static Stack<string> _saveQueue = new Stack<string>();

        public static void Main(string[] args)
        {
            Console.WriteLine("DonatePay Skyrim integration started.");
#if DEBUG
            string donateStatus = "all";
#else
            string donateStatus = "success";
#endif

            if (args.Length > 0)
            {
                donateStatus = args[0].Trim();
            }

            _donatConfigs = GetDonateConfigs();

            if (!File.Exists(_apiKeyFile))
            {
                throw new Exception($"{_apiKeyFile} not found. Create file end write API key.");
            }

            string apiKey = File.ReadAllText(_apiKeyFile);
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var saveGameDir = Path.Combine(documents, @"Documents\My Games\Skyrim\Saves");

            string movedDir = Path.Combine(saveGameDir, "MovedSaves");
            if (!Directory.Exists(movedDir))
            {
                Directory.CreateDirectory(movedDir);
            }

            Transactions prevTrans = GetTransactions(apiKey, type: "donation", limit: _limit, status: donateStatus);

            while (true)
            {
                Thread.Sleep(20000);
                Transactions currentTrans = GetTransactions(apiKey, type: "donation", limit: _limit, status: donateStatus);

                if (currentTrans != null && currentTrans.data != null && currentTrans.data.Count > 0)
                {
                    List<Transactionsdata> data = GetNewTransactions(currentTrans, prevTrans);
                    prevTrans = currentTrans;

                    if (data.Any())
                    {
                        GenerateSpawns(data, saveGameDir, movedDir);
                    }
                }

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



        private static void GenerateSpawns(List<Transactionsdata> transactions, string saveGameDir, string movedDir)
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

                if (sValue != null && sValue.Contains("DeleteLastSave"))
                {
                    var movedFile = MoveLastSaveGame(saveGameDir, movedDir);

                    if (!string.IsNullOrEmpty(movedFile))
                    {
                        string tmp = $"{DateTime.Now.ToString()}: Save \"{movedFile}\" was deleted by \"{tran.what}\"";
                        Console.WriteLine(tmp);
                        WriteLog(tmp);
                    }

                    continue;
                }

                if (sValue != null && sValue.Contains("RestoreLastSave"))
                {
                    var restoredFile = RestoreLastSaveGame(saveGameDir, movedDir);

                    if (!string.IsNullOrEmpty(restoredFile))
                    {
                        string tmp = $"{DateTime.Now.ToString()}: Save \"{restoredFile}\" was restored by \"{tran.what}\"";
                        Console.WriteLine(tmp);
                        WriteLog(tmp);
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

        private static string RestoreLastSaveGame(string saveGameDir, string movedDir)
        {
            if (!Directory.Exists(saveGameDir) || !Directory.Exists(movedDir))
            {
                WriteLog(DateTime.Now.ToString() + " Restore last save game directory not found");
                return null;
            }

            if (!_saveQueue.Any())
            {
                Console.WriteLine("No save file to restore");
                return null;
            }

            var restoreFileName = _saveQueue.Pop();
            var restoreFileFullPath = Path.Combine(movedDir, restoreFileName);

            if (File.Exists(restoreFileFullPath))
            {
                try
                {
                    var newFullPath = Path.Combine(saveGameDir, restoreFileName);

                    while (File.Exists(newFullPath))
                    {
                        string tempFileName = $"{DateTime.Now.ToString("dd_hh_mm_ss")}_{restoreFileName}";
                        newFullPath = Path.Combine(saveGameDir, tempFileName);
                        Thread.Sleep(1000);
                    }

                    File.Move(restoreFileFullPath, newFullPath);
                    WriteLog(DateTime.Now.ToString() + $" File {restoreFileName} restored");

                    return restoreFileName;
                }
                catch (Exception ex)
                {
                    _saveQueue.Push(restoreFileName);
                    WriteLog(DateTime.Now.ToString() + " Error restore save " + ex.Message + "\r\n" + ex.InnerException);
                    Console.WriteLine(ex);
                }
            }

            return null;
        }

        private static string MoveLastSaveGame(string saveGameDir, string movedDir)
        {
            if (!Directory.Exists(saveGameDir))
            {
                WriteLog(DateTime.Now.ToString() + " Save directory not exists");
                return null;
            }

            DirectoryInfo di = new DirectoryInfo(saveGameDir);
            if (di != null)
            {
                FileInfo[] subFiles = di.GetFiles("*.ess");

                if (subFiles.Length > 0)
                {
                    var lastSave = subFiles.FirstOrDefault(d => d.LastWriteTime == subFiles.Max(f => f.LastWriteTime));

                    try
                    {
                        string tempFileName = lastSave.Name;
                        var newFullPath = Path.Combine(movedDir, lastSave.Name);

                        while (File.Exists(newFullPath))
                        {
                            tempFileName = $"{DateTime.Now.ToString("dd_hh_mm_ss")}_{lastSave.Name}";
                            newFullPath = Path.Combine(movedDir, tempFileName);
                            Thread.Sleep(1000);
                        }

                        lastSave.MoveTo(newFullPath);
                        _saveQueue.Push(tempFileName);

                        WriteLog(DateTime.Now.ToString() + $" File {lastSave.Name} moved");

                        return lastSave.Name;
                    }
                    catch (Exception ex)
                    {
                        WriteLog(DateTime.Now.ToString() + " Error move save " + ex.Message + "\r\n" + ex.InnerException);
                        Console.WriteLine(ex);
                    }
                }
            }

            return null;
        }

        private static string DeleteLastSaveGame()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var saveGameDir = Path.Combine(documents, @"Documents\My Games\Skyrim\Saves");

            if (!Directory.Exists(saveGameDir))
            {
                WriteLog(DateTime.Now.ToString() + " Save directory not exists");
                return null;
            }

            DirectoryInfo di = new DirectoryInfo(saveGameDir);
            if (di != null)
            {
                FileInfo[] subFiles = di.GetFiles("*.ess");

                if (subFiles.Length > 0)
                {
                    var lastSave = subFiles.FirstOrDefault(d => d.LastWriteTime == subFiles.Max(f => f.LastWriteTime));
                    var secondFile = lastSave.Name.Replace(".ess", ".skse");

                    try
                    {
                        File.Delete(lastSave.FullName);
                        var pathToSecondFile = Path.Combine(lastSave.Directory.FullName, secondFile);
                        if (File.Exists(pathToSecondFile))
                        {
                            File.Delete(pathToSecondFile);
                        }

                        return lastSave.Name;
                    }
                    catch (Exception ex)
                    {
                        WriteLog(DateTime.Now.ToString() + " Error delete save " + ex.Message + "\r\n" + ex.InnerException);
                        Console.WriteLine(ex);
                    }
                }
            }

            return null;
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


        public static List<Transactionsdata> GetNewTransactions(Transactions current, Transactions old)
        {
            if (old == null)
            {
                return current.data;
            }

            var oldIds = old.data.Select(d => d.id);
            var result = current.data.Where(d => !oldIds.Contains(d.id)).ToList();

            return result;
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
                File.AppendAllText(_logFile, str + "\r\n");
            }
            catch (Exception e)
            {

            }
        }
    }


}
