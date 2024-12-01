using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Job_Bot.Models
{
    public class UserDisplayDto
    {
        public string Id { get; set; }

        public string Email { get; set; }
        public string Token { get; set; }
    }
    internal class JobFinderApiResponse
    {

        public string Message { get; set; }
            public UserDisplayDto Data { get; set; }
    }

    internal class JobFinderApiResponseForJobs
    {
        public string Message { get; set; }
        public int Count { get; set; }
        public List<Job> Jobs { get; set; }
    }
}
