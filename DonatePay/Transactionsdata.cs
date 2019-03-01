using System;
namespace DonatePayStat
{
	public class Transactionsdata
    {
        public int id { get; set; }
        public string what { get; set; }
        public string sum { get; set; }
        public object to_cash { get; set; }
        public object to_pay { get; set; }
        public string commission { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public vars vars { get; set; }
        public string comment { get; set; }
        public time created_at { get; set; }
    }
}
