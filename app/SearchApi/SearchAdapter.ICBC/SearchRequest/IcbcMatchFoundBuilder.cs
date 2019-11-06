﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SearchApi.Core.Adapters.Contracts;

namespace SearchAdapter.ICBC.SearchRequest
{

    public class IcbcMatchFoundBuilder
    {

        internal IcbcMatchFound _matchFound;

        public IcbcMatchFoundBuilder()
        {
            _matchFound = new IcbcMatchFound();
        }

        public IcbcMatchFoundBuilder WithFirstName(string firstName)
        {
            this._matchFound.FirstName = firstName;
            return this;
        }

        public IcbcMatchFoundBuilder WithLastName(string lastName)
        {
            this._matchFound.LastName = lastName;
            return this;
        }


        public IcbcMatchFoundBuilder WithDateOfBirth(DateTime dateOfBirth)
        {
            this._matchFound.DateOfBirth = dateOfBirth;
            return this;
        }

        public MatchFound Build()
        {
            return this._matchFound;
        }

    }

   
    internal sealed class IcbcMatchFound : MatchFound
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime DateOfBirth { get; set; }
    }
}
