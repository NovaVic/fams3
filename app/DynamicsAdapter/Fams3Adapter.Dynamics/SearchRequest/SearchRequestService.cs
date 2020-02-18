﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fams3Adapter.Dynamics.Address;
using Fams3Adapter.Dynamics.Employment;
using Fams3Adapter.Dynamics.Identifier;
using Fams3Adapter.Dynamics.Name;
using Fams3Adapter.Dynamics.Person;
using Fams3Adapter.Dynamics.PhoneNumber;
using Simple.OData.Client;

namespace Fams3Adapter.Dynamics.SearchRequest
{
    public interface ISearchRequestService
    {
        Task<SSG_Identifier> CreateIdentifier(SSG_Identifier identifier, CancellationToken cancellationToken);
        Task<SSG_Address> CreateAddress(SSG_Address address, CancellationToken cancellationToken);
        Task<SSG_PhoneNumber> CreatePhoneNumber(SSG_PhoneNumber phoneNumber, CancellationToken cancellationToken);
        Task<SSG_Aliase> CreateName(SSG_Aliase name, CancellationToken cancellationToken);

        Task<SSG_Person> SavePerson(PersonEntity person, CancellationToken cancellationToken);
        Task<SSG_Employment> CreateEmployment(SSG_Employment employment, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The SearchRequestService expose Search Request Functionality
    /// </summary>
    public class SearchRequestService : ISearchRequestService
    {
        private readonly IODataClient _oDataClient;

        public SearchRequestService(IODataClient oDataClient)
        {
            this._oDataClient = oDataClient;
        }

        /// <summary>
        /// Gets all the search request in `Ready for Search` status
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<SSG_Identifier> CreateIdentifier(SSG_Identifier identifier, CancellationToken cancellationToken)
        {
            return await this._oDataClient.For<SSG_Identifier>().Set(identifier).InsertEntryAsync(cancellationToken);
        }

        public async Task<SSG_Person> SavePerson(PersonEntity person, CancellationToken cancellationToken)
        {
            return await this._oDataClient.For<SSG_Person>().Set(person).InsertEntryAsync(cancellationToken);
        }

        public async Task<SSG_PhoneNumber> CreatePhoneNumber(SSG_PhoneNumber phone, CancellationToken cancellationToken)
        {
            return await this._oDataClient.For<SSG_PhoneNumber>().Set(phone).InsertEntryAsync(cancellationToken);
        }

        public async Task<SSG_Address> CreateAddress(SSG_Address address, CancellationToken cancellationToken)
        {
            string countryName = address.CountryText;
            var country = await _oDataClient.For<SSG_Country>()
                                         .Filter(x => x.Name == countryName)
                                         .FindEntryAsync(cancellationToken);
            address.Country = country;

            string subdivisionName = address.CountrySubdivisionText;
            var subdivision = await _oDataClient.For<SSG_CountrySubdivision>()
                                         .Filter(x => x.Name == subdivisionName)
                                         .FindEntryAsync(cancellationToken);
            address.CountrySubdivision = subdivision;

            return await this._oDataClient.For<SSG_Address>().Set(address).InsertEntryAsync(cancellationToken);
        }

        public async Task<SSG_Aliase> CreateName(SSG_Aliase name, CancellationToken cancellationToken)
        {
            return await this._oDataClient.For<SSG_Aliase>().Set(name).InsertEntryAsync(cancellationToken);
        }

        public async Task<SSG_Employment> CreateEmployment(SSG_Employment employment, CancellationToken cancellationToken)
        {
            var country = await _oDataClient.For<SSG_Country>()
                                            .Filter(x => x.Name == employment.CountryText)
                                            .FindEntryAsync(cancellationToken);
            employment.Country = country;

            var subdivision = await _oDataClient.For<SSG_CountrySubdivision>()
                                      .Filter(x => x.Name == employment.CountrySubdivisionText)
                                      .FindEntryAsync(cancellationToken);
            employment.CountrySubdivision = subdivision;


            return await this._oDataClient.For<SSG_Employment>().Set(employment).InsertEntryAsync(cancellationToken);
        }
    }
}