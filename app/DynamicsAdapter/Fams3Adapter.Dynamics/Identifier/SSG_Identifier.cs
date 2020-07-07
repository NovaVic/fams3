using Fams3Adapter.Dynamics.Person;
using Fams3Adapter.Dynamics.SearchRequest;
using Newtonsoft.Json;
using System;

namespace Fams3Adapter.Dynamics.Identifier
{
    public class IdentifierEntity : DynamicsEntity
    {
        [JsonProperty("ssg_identification")]
        public string Identification { get; set; }

        [JsonProperty("ssg_suppliertypecode")]
        public string SupplierTypeCode { get; set; }

        [JsonProperty("ssg_identificationcategorytext")]
        public int? IdentifierType { get; set; }

        [JsonProperty("ssg_notes")]
        public string Notes { get; set; }

        [JsonProperty("ssg_description")]
        public string Description { get; set; }

        [JsonProperty("ssg_issuedby")]
        public string IssuedBy { get; set; }

        [JsonProperty("ssg_SearchRequest")]
        public virtual SSG_SearchRequest SearchRequest { get; set; }

 

    
        public virtual PersonEntity PersonEntity { get; set; }

        [JsonProperty("ssg_informationsourcetext")]
        public int? InformationSource { get; set; }

        public override string ToResultName()
        {
            return $"Duplicate | Identifier | {Identification} {IdentifierType}";
        }
    }

    public class SSG_Identifier : IdentifierEntity
    {
        [JsonProperty("ssg_identifierid")]
        public Guid IdentifierId { get; set; }
    }

}
