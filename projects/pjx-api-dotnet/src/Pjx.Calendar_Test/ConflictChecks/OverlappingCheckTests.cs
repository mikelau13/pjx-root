using Autofac;
using Autofac.Extras.Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.Repositories;
using System;
using System.Collections.Generic;

namespace Pjx.CalendarLibrary.ConflictChecks.Tests
{
    [TestClass()]
    public class OverlappingCheckTests
    {
        [TestMethod()]
        public void NoEvent()
        {
            // Assertion: no event at all
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>());
                var checker = mock.Create<ConflictCheck>();

                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if no events.");
            }
        }

        [TestMethod()]
        public void NoOverlappedBefore()
        {
            // Assertion: has event before but not overlapped
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddYears(-1), End = eventToCheck.End.Value.AddYears(-1) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if has event before but not overlapped.");
            }
        }

        [TestMethod()]
        public void NoOverlappedAfter()
        {
            // Assertion: has event after but not overlapped
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddYears(1), End = eventToCheck.End.Value.AddYears(1) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if has event after but not overlapped.");
            }
        }

        [TestMethod()]
        public void ExactlySame()
        {

            // Assertion: has one event exactly same start and end time
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start, End = eventToCheck.End }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if another event exactly same start and end time.");
            }
        }


        [TestMethod()]
        public void OverlapEarlier()
        {
            // Assertion: overlapping with another earlier event
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddMinutes(-60), End = eventToCheck.End.Value.AddMinutes(-60) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if another event is 1 hour earlier than this event.");

                // full/all day event
                eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()) };
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if another full day event is 1 hour earlier than this event.");
            }
        }

        [TestMethod()]
        public void OverlapLater()
        {
            // Assertion: overlapping with another later event
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddMinutes(60), End = eventToCheck.End.Value.AddMinutes(60) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if another event 1 hour later than this event.");

                // full/all day event
                eventToCheck = new CalendarEvent { Start = eventToCheck.Start.AddHours(1) };
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if another full day event is 1 hour later than this event.");
            }
        }


        [TestMethod()]
        public void OverlapJustTouching()
        {
            // Assertion: just touching another later event
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.End.Value, End = eventToCheck.End.Value.AddHours(1) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if another event touching end time of this event.");

                // full/all day event
                eventToCheck = new CalendarEvent { Start = eventToCheck.End.Value.AddHours(1), End = eventToCheck.End.Value.AddHours(2) };
                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if another event end time touching this event.");
            }
        }


        [TestMethod()]
        public void OverlapJustTouchingFullDay()
        {
            // Assertion: just touching another later event
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.End.Value }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if another event touching end time of this event.");

                // full/all day event
                eventToCheck = new CalendarEvent { Start = eventToCheck.End.Value.AddDays(1), End = eventToCheck.End.Value.AddDays(2) };
                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if another event end time touching this event.");
            }
        }

        [TestMethod()]
        public void Including()
        {
            // Assertion: overlapping with another event that start before and end after this event 
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddMinutes(-60), End = eventToCheck.End.Value.AddMinutes(+60) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if overlapping with another event that start before and end after this event.");
            }
        }

        [TestMethod()]
        public void Inside()
        {
            // Assertion: overlapping with another event that start after and end before this event 
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddMinutes(+60), End = eventToCheck.End.Value.AddMinutes(-60) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if overlapping with another event that start after and end before this event.");
            }
        }


        [TestMethod()]
        public void NoOverlappedMultiple()
        {
            // Assertion: none of these events overlapped
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddYears(1) },
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddYears(-1), End = eventToCheck.End.Value.AddYears(-1) },
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddMilliseconds(-2), End = eventToCheck.Start.AddMilliseconds(-1) },
                        new CalendarEvent { EventId = 1, Start = eventToCheck.End.Value.AddMilliseconds(1), End = eventToCheck.End.Value.AddMilliseconds(2) }
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsTrue(checker.DoCheck(eventToCheck), "True if none of these events overlapped.");
            }
        }

        [TestMethod()]
        public void HasOverlappedMultiple()
        {
            // Assertion: few of these events overlapped
            using (var mock = AutoMock.GetLoose(cfg => cfg.RegisterInstance(new OverlappingCheck()).As<IOverlappingCheck>()))
            {
                var eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()), End = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()) };
                mock.Mock<ICalendarEventRepository<CalendarEvent>>()
                    .Setup(x => x.GetAll())
                    .Returns(new List<CalendarEvent>
                    {
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddYears(1), End = eventToCheck.End.Value.AddYears(1) },
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddYears(-1) },
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddMilliseconds(-2), End = eventToCheck.Start.AddMilliseconds(-1) },
                        new CalendarEvent { EventId = 1, Start = eventToCheck.End.Value.AddMilliseconds(1), End = eventToCheck.End.Value.AddMilliseconds(2) },
                        new CalendarEvent { EventId = 1, Start = eventToCheck.Start.AddMilliseconds(-1), End = eventToCheck.End.Value.AddMilliseconds(1) },
                    }
                    );
                var checker = mock.Create<ConflictCheck>();
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if few of these events overlapped.");

                // full/all day event
                eventToCheck = new CalendarEvent { Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, new TimeSpan()) };
                Assert.IsFalse(checker.DoCheck(eventToCheck), "False if few of these full day events overlapped.");
            }
        }
    }
}