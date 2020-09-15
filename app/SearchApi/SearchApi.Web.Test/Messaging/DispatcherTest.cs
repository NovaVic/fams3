﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BcGov.Fams3.SearchApi.Contracts.Person;
using BcGov.Fams3.SearchApi.Contracts.PersonSearch;
using BcGov.Fams3.SearchApi.Core.Configuration;
using MassTransit;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using SearchApi.Web.Controllers;
using SearchApi.Web.DeepSearch;
using SearchApi.Web.Messaging;

namespace SearchApi.Web.Test.Messaging
{
    [TestFixture]
    public class DispatcherTest
    {

        private Dispatcher sut;

        private Mock<ISendEndpointProvider> sendEndpointProviderMock;
        private Mock<IDeepSearchService> deepSearchProviderMock;
        private Mock<IOptions<RabbitMqConfiguration>> rabbitMqConfigurationMock;

        private Mock<ISendEndpoint> sendEndpointMock;

        [SetUp]
        public void SetUp()
        {

            sendEndpointProviderMock = new Mock<ISendEndpointProvider>();
            deepSearchProviderMock = new Mock<IDeepSearchService>();
            rabbitMqConfigurationMock = new Mock<IOptions<RabbitMqConfiguration>>();

            sendEndpointMock = new Mock<ISendEndpoint>();

            RabbitMqConfiguration rabbitMqConfiguration = new RabbitMqConfiguration();
            rabbitMqConfiguration.Host = "localhost";
            rabbitMqConfiguration.Port = 15672;
            rabbitMqConfiguration.Username = "username";
            rabbitMqConfiguration.Password = "password";

            sendEndpointProviderMock
                .Setup(x => x.GetSendEndpoint(It.IsAny<Uri>()))
                .Returns(Task.FromResult(sendEndpointMock.Object));
            deepSearchProviderMock.Setup(x => x.SaveRequest(It.IsAny<PersonSearchRequest>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            rabbitMqConfigurationMock.Setup(x => x.Value).Returns(rabbitMqConfiguration);

            sendEndpointMock.Setup(x => x.Send<PersonSearchOrdered>(It.IsAny<PersonSearchOrdered>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(sendEndpointMock));

            sut = new Dispatcher(sendEndpointProviderMock.Object, rabbitMqConfigurationMock.Object, deepSearchProviderMock.Object);

        }

        [Test]
        public async Task with1ProviderShouldSendTo1Queue()
        {

            await sut.Dispatch(new PersonSearchRequest(
                "firstName",
                "lastName",
                DateTime.Now,
                new List<PersonalIdentifier>(),
                new List<Address>(),
                new List<Phone>(),
                new List<Name>(),
                new List<RelatedPerson>(),
                new List<Employment>(),
                new List<DataProvider>
                {
                    new DataProvider()
                    {
                        Name = "TEST",
                        Completed = false,
                        NumberOfRetries = 3,
                        TimeBetweenRetries = 30
                    }
                },
                "SearchRequestKey"), Guid.NewGuid());


            sendEndpointMock.Verify(x => x.Send<PersonSearchOrdered>(It.IsAny<PersonSearchOrdered>(), It.IsAny<CancellationToken>()),
                () => { return Times.Once(); });

        }

        [Test]
        public async Task with5ProviderShouldSendTo5Queue()
        {

            await sut.Dispatch(new PersonSearchRequest(
                "firstName",
                "lastName",
                DateTime.Now,
                new List<PersonalIdentifier>(),
                new List<Address>(),
                new List<Phone>(),
                new List<Name>(),
                new List<RelatedPerson>(),
                new List<Employment>(),
                new List<DataProvider>
                {
                    new DataProvider() {Name = "TEST1", Completed = false, NumberOfRetries = 5, TimeBetweenRetries=10},
                    new DataProvider() {Name = "TEST2", Completed = false, NumberOfRetries = 3, TimeBetweenRetries=50},
                    new DataProvider() {Name = "TEST3", Completed= true, NumberOfRetries = 6, TimeBetweenRetries=70},
                    new DataProvider() {Name = "TEST4", Completed =false, NumberOfRetries = 3, TimeBetweenRetries=45},
                    new DataProvider() {Name = "TEST5", Completed=false, NumberOfRetries = 5, TimeBetweenRetries=34}
                },
                "SearchRequestKey"), Guid.NewGuid());


            sendEndpointMock.Verify(x => x.Send<PersonSearchOrdered>(It.IsAny<PersonSearchOrdered>(), It.IsAny<CancellationToken>()),
                () => { return Times.Exactly(5); });

        }

        [Test]
        public async Task with0ProviderShouldSendTo0Queue()
        {

            await sut.Dispatch(new PersonSearchRequest(
                "firstName",
                "lastName",
                DateTime.Now,
                new List<PersonalIdentifier>(),
                new List<Address>(),
                new List<Phone>(),
                new List<Name>(),
                new List<RelatedPerson>(),
                new List<Employment>(),
                new List<DataProvider>(), "SearchRequestKey"), Guid.NewGuid());


            sendEndpointMock.Verify(x => x.Send<PersonSearchOrdered>(It.IsAny<PersonSearchOrdered>(), It.IsAny<CancellationToken>()),
                () => { return Times.Never(); });

        }

        [Test]
        public async Task withNullProviderShouldSendTo0Queue()
        {

            await sut.Dispatch(new PersonSearchRequest(
                "firstName",
                "lastName",
                DateTime.Now,
                new List<PersonalIdentifier>(),
                new List<Address>(),
                new List<Phone>(),
                new List<Name>(),
                new List<RelatedPerson>(),
                new List<Employment>(),
                null, "SearchRequestKey"), Guid.NewGuid());


            sendEndpointMock.Verify(x => x.Send<PersonSearchOrdered>(It.IsAny<PersonSearchOrdered>(), It.IsAny<CancellationToken>()),
                () => { return Times.Never(); });

        }

        [Test]
        public void withNullRequestShouldSendThrowArgumentNullException()
        {

            Assert.ThrowsAsync<ArgumentNullException>(() => sut.Dispatch(null, Guid.NewGuid()));

        }



        [Test]
        public void withDefaultRequestIdShouldSendThrowArgumentNullException()
        {

            Assert.ThrowsAsync<ArgumentNullException>(() => sut.Dispatch(new PersonSearchRequest(
                "firstName",
                "lastName",
                DateTime.Now,
                new List<PersonalIdentifier>(),
                new List<Address>(),
                new List<Phone>(),
                new List<Name>(),
                new List<RelatedPerson>(),
                new List<Employment>(),
                null, "SearchRequestKey"), new Guid()));

        }


    }
}