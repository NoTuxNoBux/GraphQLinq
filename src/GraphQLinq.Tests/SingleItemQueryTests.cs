﻿using System.Collections.Generic;
using System.Linq;
using HSL;
using NUnit.Framework;

namespace GraphQLinq.Tests
{
    [TestFixture]
    [Category("Single item query")]
    [Category("Integration tests")]
    class SingleItemQueryTests
    {
        const string AgencyId = "LINKKI:54836";
        const string TripId = "OULU:0000880601314021";
        
        readonly HslGraphContext hslGraphContext = new HslGraphContext("https://api.digitransit.fi/routing/v1/routers/finland/index/graphql");

        [Test]
        public void SelectingSingleTripIdIsNotNull()
        {
            var tripId = hslGraphContext.Trip(TripId).Select(t => t.gtfsId).ToItem();

            Assert.That(tripId, Is.Not.Null);
        }

        [Test]
        public void SelectingNestedPropertiesOfSingleTripNestedPropertiesAreNotNull()
        {
            var item = hslGraphContext.Trip(TripId)
                            .Select(trip => new TripDetails(trip.gtfsId, trip.route.gtfsId, trip.pattern.geometry, trip.route.agency.name, trip.route.agency.phone))
                            .ToItem();

            Assert.Multiple(() =>
            {
                Assert.That(item.TripId, Is.Not.Null);
                Assert.That(item.RouteId, Is.Not.Null);
                Assert.That(item.Geometry, Is.Not.Null);
                Assert.That(item.Name, Is.Not.Null);
                //Assert.That(item.Phone, Is.Not.Null);
            });
        }

        [Test]
        public void SelectingNestedPropertiesOfSingleTripAndCallingConstructorNestedPropertiesAreNotNull()
        {
            var item = hslGraphContext.Trip(TripId)
                            .Select(trip => new TripDetails(trip.gtfsId, trip.route.gtfsId, trip.pattern.geometry, trip.route.agency.name, trip.route.agency.phone))
                            .ToItem();

            Assert.Multiple(() =>
            {
                Assert.That(item.TripId, Is.Not.Null);
                Assert.That(item.RouteId, Is.Not.Null);
                Assert.That(item.Geometry, Is.Not.Null);
                Assert.That(item.Name, Is.Not.Null);
                //Assert.That(item.Phone, Is.Not.Null);
            });
        }

        [Test]
        public void SelectingThreeLevelNestedPropertyOfSingleTripNestedPropertyIsNotNull()
        {
            var routes = hslGraphContext.Trip(TripId).Select(trip => trip.route.agency.routes).ToItem();

            CollectionAssert.IsNotEmpty(routes);
        }

        [Test]
        public void SelectingSingleTripNestedPropertyIsNull()
        {
            var trip = hslGraphContext.Trip(TripId).ToItem();

            Assert.That(trip.pattern, Is.Null);
        }

        [Test]
        public void SelectingAndIncludingNestedPropertySingleTripNestedPropertyIsNotNull()
        {
            var pattern = hslGraphContext.Trip(TripId).Include(trip => trip.route).ToItem();

            Assert.That(pattern.route, Is.Not.Null);
        }

        [Test]
        public void SelectingListOfListNestedPropertyShouldCheckListTypeRecursively()
        {
            Agency agency = null;

            Assert.DoesNotThrow(() => agency = hslGraphContext.Agency(AgencyId).Include(a => a.routes.Select(route => route.trips.Select(trip => trip.geometry))).ToItem());

            if (agency == null)
            {
                Assert.Inconclusive($"Agency with id {AgencyId} not found");
            }
            else
            {
                Assert.Multiple(() =>
                {
                    CollectionAssert.IsNotEmpty(agency.routes[0].trips[0].geometry);
                    CollectionAssert.IsNotEmpty(agency.routes[1].trips[0].geometry);
                });
            }
        }
    }

    class TripDetails
    {
        public string TripId { get; }
        public string RouteId { get; }
        public List<Coordinates> Geometry { get; }
        public string Name { get; }
        public string Phone { get; }

        internal TripDetails(string tripId, string routeId, List<Coordinates> geometry, string name, string phone)
        {
            TripId = tripId;
            RouteId = routeId;
            Geometry = geometry;
            Name = name;
            Phone = phone;
        }
    }
}
