using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pjx.CalendarEntity.Models;
using Pjx.CalendarLibrary.ConflictChecks;
using Pjx.CalendarLibrary.Repositories;
using Pjx_Api.Data;

namespace Pjx_Api.Controllers.Calendar
{
    /// <summary>
    /// Controller to handle CRUD of user calendar event(s).
    /// </summary>
    [Route("api/calendar")]
    [ApiController]
    public class EventController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EventController> _logger;
        private IConflictCheck _conflictCheck;

        public EventController(ILogger<EventController> logger
            , IUnitOfWork unitOfWork
            , IConflictCheck conflictCheck)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _conflictCheck = conflictCheck;
        }

        [Route("healthcheck")]
        [HttpGet]
        [AllowAnonymous]
        public IActionResult HealthCheck()
        {
            _logger.LogInformation("EventController.HealthCheck()");
            return new JsonResult("okay");
        }

        /// <summary>
        /// Create an user event.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [Route("event/create")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(EventCreateBindingModel model)
        {
            _logger.LogInformation("EventController.Create(EventCreateBindingModel model)");

            ClaimsPrincipal currentUser = this.User;
            string userId = currentUser.FindFirst(ClaimTypes.NameIdentifier).Value;

            CalendarEvent ce = new CalendarEvent
            {
                UserId = userId,
                Title = model.Title,
                Start = model.Start,
                End = model.End,
                DepartmentId = 1
            };

            if (_conflictCheck.DoCheck(ce))
            {
                _unitOfWork.CalendarEvents.Add(ce);

                int updated = _unitOfWork.Complete();

                if (updated > 0)
                {
                    return new JsonResult(ce);
                }
                else
                {
                    return base.Problem("Failed to commit.");
                }
            }
            else
            {
                return base.Problem("Failed conflict check.");
            }
        }


        /// <summary>
        /// Update an existing user event.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [Route("event/update")]
        [HttpPut]
        [Authorize]
        public async Task<IActionResult> Update(EventUpdateBindingModel model)
        {
            _logger.LogInformation("EventController.Update(EventUpdateBindingModel model)");

            ClaimsPrincipal currentUser = this.User;
            string userId = currentUser.FindFirst(ClaimTypes.NameIdentifier).Value;

            CalendarEvent ce = _unitOfWork.CalendarEvents.GetById(model.Id);

            if (ce == null) return NotFound();

            ce.Title = model.Title;
            ce.Start = model.Start;
            ce.End = model.End;

            if (_conflictCheck.DoCheck(ce))
            {
                _unitOfWork.CalendarEvents.Update(ce);
                int updated = _unitOfWork.Complete();

                if (updated > 0)
                {
                    return new JsonResult(ce);
                }
                else
                {
                    return base.Problem("Failed to commit.");
                }
            }
            else
            {
                return base.Problem("Failed conflict check.");
            }
        }


        /// <summary>
        /// Delete an existing user event.
        /// </summary>
        /// <param name="eventId">CalendarEvent.EventId</param>
        /// <returns></returns>
        [Route("event/delete")]
        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> Delete(int eventId)
        {
            _logger.LogInformation("EventController.Delete({0})", eventId);

            ClaimsPrincipal currentUser = this.User;
            string userId = currentUser.FindFirst(ClaimTypes.NameIdentifier).Value;

            CalendarEvent toDel = _unitOfWork.CalendarEvents.GetById(eventId);

            if (toDel != null && toDel.UserId == userId)
            {
                _unitOfWork.CalendarEvents.Remove(toDel);
                int updated =_unitOfWork.Complete();

                if (updated > 0) 
                {
                    return new JsonResult(true);
                }
                else
                {
                    return base.Problem ("Failed to commit.");
                } 
            } 
            else 
            {
                return new JsonResult(false);
            }
        }


        /// <summary>
        /// Return all events belonging to a particular user.
        /// </summary>
        /// <param name="start">Calendar start date, inclusive</param>
        /// <param name="end">Calendar end date, exclusive</param>
        /// <returns></returns>
        [Route("event/readAll")]
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ReadAll(DateTimeOffset start, DateTimeOffset end)
        {
            _logger.LogInformation("EventController.ReadAll('{0}', '{1}')", start, end);

            ClaimsPrincipal currentUser = this.User;
            string userId = currentUser.FindFirst(ClaimTypes.NameIdentifier).Value;

            _logger.LogInformation(userId);

            IEnumerable<CalendarEvent> results = _unitOfWork.CalendarEvents.GetAllBetweenByUser(userId, start, end);

            return new JsonResult(results);
        }


        [Route("event/test")]
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Test()
        {
            _logger.LogInformation("EventController.Test()");

            CalendarEvent ce = new CalendarEvent
            {
                UserId = "testuser",
                Title = "whatever",
                Start = new DateTimeOffset(2020, 1, 2, 0, 0, 0, new TimeSpan()),
                DepartmentId = 1
            };

            if (_conflictCheck.DoCheck(ce))
            {
                _unitOfWork.CalendarEvents.Add(ce);

                int updated = _unitOfWork.Complete();

                if (updated > 0)
                {
                    return new JsonResult(ce);
                }
                else
                {
                    return base.Problem("Failed to commit.");
                }
            }
            else
            {
                return base.Problem("Failed conflict check.");
            }
        }
    }
}
