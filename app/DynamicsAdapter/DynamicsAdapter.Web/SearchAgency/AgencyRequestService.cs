using AutoMapper;
using BcGov.Fams3.Utils.Object;
using Fams3Adapter.Dynamics.Update;
using DynamicsAdapter.Web.SearchAgency.Models;
using Fams3Adapter.Dynamics.Address;
using Fams3Adapter.Dynamics.Employment;
using Fams3Adapter.Dynamics.Identifier;
using Fams3Adapter.Dynamics.Name;
using Fams3Adapter.Dynamics.Notes;
using Fams3Adapter.Dynamics.Person;
using Fams3Adapter.Dynamics.PhoneNumber;
using Fams3Adapter.Dynamics.RelatedPerson;
using Fams3Adapter.Dynamics.SearchRequest;
using Fams3Adapter.Dynamics.Types;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Fams3Adapter.Dynamics.Agency;

namespace DynamicsAdapter.Web.SearchAgency
{
    public interface IAgencyRequestService
    {
        Task<SSG_SearchRequest> ProcessSearchRequestOrdered(SearchRequestOrdered searchRequestOrdered);
        Task<SSG_SearchRequest> ProcessCancelSearchRequest(SearchRequestOrdered cancelSearchRequest);
        Task<SSG_SearchRequest> ProcessUpdateSearchRequest(SearchRequestOrdered updateSearchRequest);
    }

    public class AgencyRequestService : IAgencyRequestService
    {
        private readonly ILogger<AgencyRequestService> _logger;
        private readonly ISearchRequestService _searchRequestService;
        private readonly IMapper _mapper;
        private Person _personSought;
        private SSG_Person _uploadedPerson;
        private SSG_SearchRequest _uploadedSearchRequest;
        private CancellationToken _cancellationToken;
        private static int SEARCH_REQUEST_CANCELLED = 867670009;
        private static int SEARCH_REQUEST_CLOSED = 2;

        public AgencyRequestService(ISearchRequestService searchRequestService, ILogger<AgencyRequestService> logger, IMapper mapper)
        {
            _searchRequestService = searchRequestService;
            _logger = logger;
            _mapper = mapper;
            _personSought = null;
            _uploadedPerson = null;
            _uploadedSearchRequest = null;
        }

        public async Task<SSG_SearchRequest> ProcessSearchRequestOrdered(SearchRequestOrdered searchRequestOrdered)
        {
            _personSought = searchRequestOrdered.Person;
            var cts = new CancellationTokenSource();
            _cancellationToken = cts.Token;

            SearchRequestEntity searchRequestEntity = _mapper.Map<SearchRequestEntity>(searchRequestOrdered);
            searchRequestEntity.CreatedByApi = true;
            searchRequestEntity.SendNotificationOnCreation = true;
            _uploadedSearchRequest = await _searchRequestService.CreateSearchRequest(searchRequestEntity, cts.Token);
            _logger.LogInformation("Create Search Request successfully");

            PersonEntity personEntity = _mapper.Map<PersonEntity>(_personSought);
            personEntity.SearchRequest = _uploadedSearchRequest;
            personEntity.InformationSource = InformationSourceType.Request.Value;
            personEntity.IsCreatedByAgency = true;
            personEntity.IsPrimary = true;
            _uploadedPerson = await _searchRequestService.SavePerson(personEntity, _cancellationToken);
            _logger.LogInformation("Create Person successfully");

            await UploadIdentifiers();
            await UploadAddresses();
            await UploadPhones();
            await UploadEmployment();
            await UploadRelatedPersons();
            await UploadRelatedApplicant(_uploadedSearchRequest.ApplicantFirstName, _uploadedSearchRequest.ApplicantLastName);
            await UploadAliases();
            await SubmitToQueue();
            return _uploadedSearchRequest;
        }

        public async Task<SSG_SearchRequest> ProcessCancelSearchRequest(SearchRequestOrdered searchRequestOrdered)
        {
            var cts = new CancellationTokenSource();
            _cancellationToken = cts.Token;
            SSG_SearchRequest ssgSearchRequest = await _searchRequestService.GetSearchRequest(searchRequestOrdered.SearchRequestKey, _cancellationToken);

            if (ssgSearchRequest == null)
            {
                _logger.LogInformation("the cancelling search request does not exist.");
                return null;
            }
            if (ssgSearchRequest.Agency.AgencyCode != searchRequestOrdered?.Person?.Agency?.Code)
            {
                throw new Exception("the cancelling search request cannot be processed as wrong agency code.");
            }
            return await _searchRequestService.CancelSearchRequest(searchRequestOrdered.SearchRequestKey, _cancellationToken);
        }

        public async Task<SSG_SearchRequest> ProcessUpdateSearchRequest(SearchRequestOrdered searchRequestOrdered)
        {
            var cts = new CancellationTokenSource();
            _cancellationToken = cts.Token;
            //get existedSearchRequest
            SSG_SearchRequest existedSearchRequest = await _searchRequestService.GetSearchRequest(searchRequestOrdered.SearchRequestKey, _cancellationToken);
            if (existedSearchRequest == null)
            {
                _logger.LogInformation("the updating search request does not exist.");
                return null;
            }
            if (existedSearchRequest.StatusCode == SEARCH_REQUEST_CANCELLED || existedSearchRequest.StatusCode == SEARCH_REQUEST_CLOSED)
            {
                throw new Exception($"Search Request {searchRequestOrdered.SearchRequestKey} is already closed or cancelled.");
            }
            existedSearchRequest.IsDuplicated = true;
            _uploadedSearchRequest = existedSearchRequest;

            //get existedPersonSought
            SSG_Person existedSoughtPerson = existedSearchRequest?.SSG_Persons?.FirstOrDefault(
                    m => m.FirstName == existedSearchRequest.PersonSoughtFirstName
                    && m.LastName == existedSearchRequest.PersonSoughtLastName
                    && m.InformationSource == InformationSourceType.Request.Value
                    && m.IsCreatedByAgency);
            if (existedSoughtPerson == null)
            {
                _logger.LogError("the updating personSought does not exist. something is wrong.");
                return null;
            }
            existedSoughtPerson = await _searchRequestService.GetPerson(existedSoughtPerson.PersonId, _cancellationToken);
            existedSoughtPerson.IsDuplicated = true;
            _uploadedPerson = existedSoughtPerson;


            SearchRequestEntity newSearchRequest = _mapper.Map<SearchRequestEntity>(searchRequestOrdered);
            if (newSearchRequest == null)
            {
                _logger.LogError("cannot do updating as newSearchRequest is null");
                return null;
            }
            //cannot update search request with different AgencyCode
            if (newSearchRequest?.AgencyCode != _uploadedSearchRequest?.Agency?.AgencyCode)
            {
                _logger.LogError("cannot updating to a different Agency code. Not a vaild update request.");
                return null;
            }

            //update searchRequestEntity
            await UpdateSearchRequest(newSearchRequest);

            //update notesEntity
            if (!String.IsNullOrEmpty(newSearchRequest.Notes)
                && !String.Equals(existedSearchRequest.Notes, newSearchRequest.Notes, StringComparison.InvariantCultureIgnoreCase))
            {
                await UploadNotes(newSearchRequest);
            }

            //update PersonEntity
            if (searchRequestOrdered.Person == null)
            {
                _logger.LogError("the searchRequestOrdered does not contain Person. The request is wrong.");
                return null;
            }
            _personSought = searchRequestOrdered.Person;

            await UpdatePersonSought();

            //update RelatedPerson applicant
            await UpdateRelatedApplicant((string.IsNullOrEmpty(newSearchRequest.ApplicantFirstName) && string.IsNullOrEmpty(newSearchRequest.ApplicantLastName)) ? null : new RelatedPersonEntity()
            {
                FirstName = newSearchRequest.ApplicantFirstName,
                LastName = newSearchRequest.ApplicantLastName,
                StatusCode = 1
            });

            //update identifiers
            //await UpdateIdentifiers();
            await UploadIdentifiers();

            //update employment
            await UpdateEmployment();

            //for phones, addresses, relatedPersons, names are same as creation, as if different, add new one, if same, ignore
            await UploadAddresses();
            await UploadPhones();
            await UploadRelatedPersons();
            await UploadAliases();



            return _uploadedSearchRequest;
        }

        private async Task<bool> UploadIdentifiers()
        {
            if (_personSought.Identifiers == null) return true;
            _logger.LogDebug($"Attempting to create identifier records for SearchRequest.");

            foreach (var personId in _personSought.Identifiers.Where(m => m.Owner == OwnerType.PersonSought))
            {
                IdentifierEntity identifier = _mapper.Map<IdentifierEntity>(personId);
                identifier.SearchRequest = _uploadedSearchRequest;
                identifier.InformationSource = InformationSourceType.Request.Value;
                identifier.Person = _uploadedPerson;
                identifier.IsCreatedByAgency = true;
                SSG_Identifier newIdentifier = await _searchRequestService.CreateIdentifier(identifier, _cancellationToken);
            }
            _logger.LogInformation("Create identifier records for SearchRequest successfully");
            return true;
        }

        private async Task<bool> UploadAddresses()
        {
            if (_personSought.Addresses == null) return true;

            _logger.LogDebug($"Attempting to create adddress for SoughtPerson");

            foreach (var address in _personSought.Addresses.Where(m => m.Owner == OwnerType.PersonSought))
            {
                AddressEntity addr = _mapper.Map<AddressEntity>(address);
                addr.SearchRequest = _uploadedSearchRequest;
                addr.InformationSource = InformationSourceType.Request.Value;
                addr.Person = _uploadedPerson;
                addr.IsCreatedByAgency = true;
                SSG_Address uploadedAddr = await _searchRequestService.CreateAddress(addr, _cancellationToken);
            }
            _logger.LogInformation("Create addresses records for SearchRequest successfully");
            return true;
        }

        private async Task<bool> UploadPhones()
        {
            if (_personSought.Phones == null) return true;

            _logger.LogDebug($"Attempting to create Phones for SoughtPerson");

            foreach (var phone in _personSought.Phones.Where(m => m.Owner == OwnerType.PersonSought))
            {
                PhoneNumberEntity ph = _mapper.Map<PhoneNumberEntity>(phone);
                ph.SearchRequest = _uploadedSearchRequest;
                ph.InformationSource = InformationSourceType.Request.Value;
                ph.Person = _uploadedPerson;
                ph.IsCreatedByAgency = true;
                SSG_PhoneNumber uploadedPhone = await _searchRequestService.CreatePhoneNumber(ph, _cancellationToken);
            }
            _logger.LogInformation("Create phones records for SearchRequest successfully");
            return true;
        }

        private async Task<bool> UploadEmployment()
        {
            if (_personSought.Employments == null) return true;

            _logger.LogDebug($"Attempting to create employment records for PersonSought.");

            foreach (var employment in _personSought.Employments)
            {
                EmploymentEntity e = _mapper.Map<EmploymentEntity>(employment);
                e.SearchRequest = _uploadedSearchRequest;
                e.InformationSource = InformationSourceType.Request.Value;
                e.Person = _uploadedPerson;
                e.IsCreatedByAgency = true;
                SSG_Employment ssg_employment = await _searchRequestService.CreateEmployment(e, _cancellationToken);

                if (employment.Employer != null)
                {
                    foreach (var phone in employment.Employer.Phones)
                    {
                        EmploymentContactEntity p = _mapper.Map<EmploymentContactEntity>(phone);
                        p.Employment = ssg_employment;
                        await _searchRequestService.CreateEmploymentContact(p, _cancellationToken);
                    }
                }
            }

            _logger.LogInformation("Create employment records for SearchRequest successfully");
            return true;
        }

        private async Task<bool> UploadRelatedPersons()
        {
            if (_personSought.RelatedPersons == null) return true;

            _logger.LogDebug($"Attempting to create related person records person sought.");

            foreach (var relatedPerson in _personSought.RelatedPersons)
            {
                RelatedPersonEntity n = _mapper.Map<RelatedPersonEntity>(relatedPerson);
                n.SearchRequest = _uploadedSearchRequest;
                n.InformationSource = InformationSourceType.Request.Value;
                n.Person = _uploadedPerson;
                n.IsCreatedByAgency = true;
                SSG_Identity relate = await _searchRequestService.CreateRelatedPerson(n, _cancellationToken);
            }
            _logger.LogInformation("Create RelatedPersons records for SearchRequest successfully");
            return true;
        }

        private async Task<bool> UploadRelatedApplicant(string firstName, string lastName)
        {
            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName)) return true;

            _logger.LogDebug($"Attempting to create related applicant.");

            RelatedPersonEntity n = new RelatedPersonEntity
            {
                FirstName = firstName,
                LastName = lastName,
                SearchRequest = _uploadedSearchRequest,
                InformationSource = InformationSourceType.Request.Value,
                Person = _uploadedPerson,
                IsCreatedByAgency = true,
                PersonType = RelatedPersonPersonType.Applicant.Value,
                StatusCode = 1
            };

            SSG_Identity relate = await _searchRequestService.CreateRelatedPerson(n, _cancellationToken);
            _logger.LogInformation("Create Related Applicant for SearchRequest successfully");
            return true;
        }

        private async Task<bool> UploadAliases()
        {
            if (_personSought.Names == null) return true;

            _logger.LogDebug($"Attempting to create aliases for SoughtPerson");

            foreach (var name in _personSought.Names.Where(m => m.Owner == OwnerType.PersonSought))
            {
                AliasEntity aliasEntity = _mapper.Map<AliasEntity>(name);
                aliasEntity.SearchRequest = _uploadedSearchRequest;
                aliasEntity.InformationSource = InformationSourceType.Request.Value;
                aliasEntity.Person = _uploadedPerson;
                aliasEntity.IsCreatedByAgency = true;
                await _searchRequestService.CreateName(aliasEntity, _cancellationToken);
            }
            _logger.LogInformation("Create alias records for SearchRequest successfully");
            return true;
        }

        private async Task<bool> UpdateSearchRequest(SearchRequestEntity newSR)
        {
            string originNotes = _uploadedSearchRequest.Notes;
            SSG_SearchRequest clonedSR = _uploadedSearchRequest.Clone();

            Dictionary<string, object> updatedFields = (Dictionary<string, object>)clonedSR.GetUpdateEntries(newSR);

            if (newSR.SearchReasonCode != null && !newSR.SearchReasonCode.Equals(_uploadedSearchRequest.SearchReason?.ReasonCode, StringComparison.InvariantCultureIgnoreCase))
            {
                SSG_SearchRequestReason reason = await _searchRequestService.GetSearchReason(newSR.SearchReasonCode, _cancellationToken);
                updatedFields.Add("ssg_RequestCategoryText", reason);
            }

            if (newSR.AgencyOfficeLocationText != null && !newSR.AgencyOfficeLocationText.Equals(_uploadedSearchRequest.AgencyLocation.LocationCode, StringComparison.InvariantCultureIgnoreCase))
            {
                SSG_AgencyLocation location = await _searchRequestService.GetSearchAgencyLocation(
                                                        newSR.AgencyOfficeLocationText,
                                                        newSR.AgencyCode,
                                                        _cancellationToken);
                updatedFields.Add("ssg_AgencyLocation", location);
            }

            if (updatedFields.Count > 0) //except notes, there is something else changed.
            {
                await _searchRequestService.UpdateSearchRequest(_uploadedSearchRequest.SearchRequestId, updatedFields, _cancellationToken);
                _logger.LogInformation("Update Search Request successfully");
            }


            return true;
        }

        private async Task<bool> UpdatePersonSought()
        {
            PersonEntity newPersonEntity = _mapper.Map<PersonEntity>(_personSought);

            Dictionary<string, object> updatedFields = (Dictionary<string, object>)_uploadedPerson.Clone().GetUpdateEntries(newPersonEntity);
            if (updatedFields.Count > 0)
            {
                await _searchRequestService.UpdatePerson(_uploadedPerson.PersonId, updatedFields, newPersonEntity, _cancellationToken);
                _logger.LogInformation("Update Person successfully");
            }
            return true;
        }

        private async Task<bool> UpdateRelatedApplicant(RelatedPersonEntity newApplicantEntity)
        {
            if (newApplicantEntity == null) return true;

            //update or add relation relatedPerson
            SSG_Identity originalRelatedApplicant = _uploadedPerson.SSG_Identities?.FirstOrDefault(
            m => m.InformationSource == InformationSourceType.Request.Value
            && m.PersonType == RelatedPersonPersonType.Applicant.Value);

            if (originalRelatedApplicant == null)
            {
                await UploadRelatedApplicant(newApplicantEntity.FirstName, newApplicantEntity.LastName);
                _logger.LogInformation("Create Related Applicant for SearchRequest successfully");
            }
            else
            {
                Dictionary<string, object> updatedFields = (Dictionary<string, object>)originalRelatedApplicant.Clone().GetUpdateEntries(newApplicantEntity);
                if (updatedFields.Count > 0)
                {
                    await _searchRequestService.UpdateRelatedPerson(originalRelatedApplicant.RelatedPersonId, updatedFields, _cancellationToken);
                    _logger.LogInformation("Update Related Applicant records for SearchRequest successfully");
                }
            }

            return true;
        }

        private async Task<bool> UpdateEmployment()
        {
            if (_personSought.Employments == null) return true;

            _logger.LogDebug($"Attempting to update employment records for PersonSought.");

            SSG_Employment originalEmployment = _uploadedPerson.SSG_Employments?.FirstOrDefault(
                    m => m.InformationSource == InformationSourceType.Request.Value
                    && m.IsCreatedByAgency);

            if (_personSought.Employments.Count() > 0)
            {
                EmploymentEntity employ = _mapper.Map<EmploymentEntity>(_personSought.Employments.ElementAt(0));
                if (originalEmployment == null)
                {
                    await UploadEmployment();
                }
                else
                {
                    IDictionary<string, object> updatedFields = originalEmployment.Clone().GetUpdateEntries(employ);
                    if (updatedFields.ContainsKey("ssg_countrytext")) //country changed
                    {
                        SSG_Country country = await _searchRequestService.GetEmploymentCountry(employ.CountryText, _cancellationToken);
                        updatedFields.Add("ssg_LocationCountry", country);
                    }

                    if (updatedFields.ContainsKey("ssg_countrysubdivision_text")) //subdivision changed
                    {
                        SSG_CountrySubdivision subdivision = await _searchRequestService.GetEmploymentSubdivision(employ.CountrySubdivisionText, _cancellationToken);
                        updatedFields.Add("ssg_CountrySubDivision", subdivision);
                    }

                    if (updatedFields.Count > 0)
                    {
                        await _searchRequestService.UpdateEmployment(originalEmployment.EmploymentId, updatedFields, _cancellationToken);
                        _logger.LogInformation("Update Employment records for SearchRequest successfully");
                    }

                    Employer employer = _personSought.Employments.ElementAt(0).Employer;
                    if (employer != null && employer.Phones != null && employer.Phones.Count() > 0)
                    {
                        SSG_Employment existedEmployment = await _searchRequestService.GetEmployment(originalEmployment.EmploymentId, _cancellationToken);
                        existedEmployment.IsDuplicated = true;
                        foreach (var phone in employer.Phones)
                        {
                            EmploymentContactEntity p = _mapper.Map<EmploymentContactEntity>(phone);
                            p.Employment = existedEmployment;
                            await _searchRequestService.CreateEmploymentContact(p, _cancellationToken);
                        }
                    }
                }
            }
            return true;
        }

        //for identifier, we should not do update, as it links to the search result. 
        private async Task<bool> UpdateIdentifiers()
        {
            if (_personSought.Identifiers == null) return true;

            _logger.LogDebug($"Attempting to update identifier records for PersonSought.");

            foreach (PersonalIdentifier pi in _personSought.Identifiers.Where(m => m.Owner == OwnerType.PersonSought))
            {
                IdentifierEntity identifierEntity = _mapper.Map<IdentifierEntity>(pi);
                SSG_Identifier originalIdentifier = _uploadedPerson.SSG_Identifiers?.FirstOrDefault(
                   m => m.InformationSource == InformationSourceType.Request.Value
                        && m.IdentifierType == identifierEntity.IdentifierType
                        && m.IsCreatedByAgency);
                if (originalIdentifier == null)
                {
                    await UploadIdentifiers();
                }
                else
                {
                    identifierEntity.IsCreatedByAgency = true;
                    IDictionary<string, object> updatedFields = originalIdentifier.Clone().GetUpdateEntries(identifierEntity);
                    if (updatedFields.Count > 0)
                    {
                        await _searchRequestService.UpdateIdentifier(originalIdentifier.IdentifierId, updatedFields, _cancellationToken);
                        _logger.LogInformation("Update Identifier records for SearchRequest successfully");
                    }
                }
            }

            return true;
        }

        private async Task<bool> UploadNotes(SearchRequestEntity newSearchRequestEntity)
        {
            NotesEntity note = new NotesEntity
            {
                StatusCode = 1,
                Description = newSearchRequestEntity.Notes,
                InformationSource = InformationSourceType.Request.Value,
                SearchRequest = _uploadedSearchRequest
            };
            SSG_Notese ssgNote = await _searchRequestService.CreateNotes(note, _cancellationToken);

            if (ssgNote == null)
            {
                _logger.LogError("Create new notes failed.");
                return false;
            }
            _logger.LogInformation("Create new notes successfully.");
            return true;
        }

        private async Task<bool> SubmitToQueue()
        {
            await _searchRequestService.SubmitToQueue(_uploadedSearchRequest.SearchRequestId);
            return true;
        }

    }



}
