using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Job_Bot.Models
{
    internal class Job
    {
        public string Id { get; set; }
        public string JobSiteId { get; set; }
        public string JobTitle { get; set; }
        public string JobDescription { get; set; }
        public string CompanyName { get; set; }
        public string JobUrl { get; set; }
        public string DatePublished { get; set; }
        public string Location { get; set; }
        public string JobHash { get; set; }
        public string JobSource { get; set; }
    }
}
