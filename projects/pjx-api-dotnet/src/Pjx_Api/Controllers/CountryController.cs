using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Pjx_Api
{
    /// <summary>
    /// Simple API for demo purpose.
    /// </summary>
    [Authorize]
    public class CountryController : ControllerBase
    {
        /// <summary>
        /// The very first API, returning few hardcoded country names.
        /// </summary>
        /// <returns></returns>
        [Route("api/country/getall")]
        [HttpGet]
        public IActionResult GetAll()
        {
            return new JsonResult(new string[] { "USA", "CA", "MEX", "UK" });
        }
    }
}
