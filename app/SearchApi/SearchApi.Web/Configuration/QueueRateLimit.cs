using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SearchApi.Web.Configuration
{
    public class QueueRateLimit
    {
        public QueueRateLimit()
        {
            this.PersonSearchCompleted_RateLimit = 1;
            this.PersonSearchCompleted_RateInterval = 5;
            this.PersonSearchCompletedJCA_RateLimit = 1;
            this.PersonSearchCompletedJCA_RateInterval = 5;
            this.PersonSearchFailed_RateLimit = 1;
            this.PersonSearchFailed_RateInterval = 1;
            this.PersonSearchInformation_RateLimit = 1;
            this.PersonSearchInformation_RateInterval = 5;
        }

        /// <summary>
        /// RabbitMq Host
        /// </summary>
        public int PersonSearchCompleted_RateLimit { get; set; }
        public int PersonSearchCompleted_RateInterval { get; set; }
        public int PersonSearchCompletedJCA_RateLimit { get; set; }
        public int PersonSearchCompletedJCA_RateInterval { get; set; }
        public int PersonSearchFailed_RateLimit { get; set; }
        public int PersonSearchFailed_RateInterval { get; set; }
        public int PersonSearchInformation_RateLimit { get; set; }
        public int PersonSearchInformation_RateInterval { get; set; }

    }
}
