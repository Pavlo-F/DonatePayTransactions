using System.Collections.Generic;

namespace DonatePayStat
{
	public class Transactions
    {
        public string status { get; set; }
        public time time { get; set; }
        public string sum { get; set; }
        public int count { get; set; }
        public List<Transactionsdata> data { get; set; }
    }
}
