﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace BcGov.Fams3.SearchApi.Contracts.Person
{
    public class Employer
    {
        [Description("The name of the employer")]
        public string Name { get; set; }

        [Description("The name of the owner of the employer")]
        public string OwnerName { get; set; }

        public IEnumerable<Phone> Phones {get;set;}

        [Description("The address of the employer")]
        public Address Address { get; set; }

        [Description("The full name  of the contact")]
        public string ContactPerson  { get; set; }
    }
}
