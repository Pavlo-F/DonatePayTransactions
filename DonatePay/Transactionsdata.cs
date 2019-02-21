using System;
namespace DonatePayStat
{
	public class Transactionsdata
    {
		public int id { get; set; }
        public string what { get; set; }
        public string sum { get; set; }
        public string status { get; set; }
        public string type { get; set; }
        public vars vars { get; set; }
        public time created_at { get; set; }
    }
}
