using Microsoft.AspNetCore.Mvc;
using WageringFeedConsumer.Models;
using WageringFeedConsumer.Repositories;
using WageringFeedConsumer.Services;

namespace WageringFeedConsumer.Controllers;

[ApiController]
[Route("")]
public class CustomerController(
    IWageringDataRepository wageringDataRepository,
    ICustomerService customerService) : ControllerBase
{
    // TODO: e.g. caching, rate limiting and so forth at the endpoint / service level
    [HttpGet("customer/{customerId:long}/stats")]
    public async Task<IActionResult> GetCustomerStats(long customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0) return BadRequest();
        
        var customerInfo = await customerService.GetCustomerAsync(customerId, cancellationToken);
        if (customerInfo == null) return NotFound();
        
        if (wageringDataRepository.IsFeedComplete) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        
        var totalStandToWin = wageringDataRepository.GetTotalStandToWin(customerId);
        
        // DEVNOTE: for simplicity, if null is returned, we do not have data for this customer and return a 404
        if (totalStandToWin == null) return NotFound();

        // DEVNOTE: No assumption of currency conversion, assume TotalStandToWin rounding to 2 d.p. (i.e. dollars and cents)
        return Ok(new CustomerStatResponse
        {
            CustomerId = customerId,
            Name = customerInfo.CustomerName,
            TotalStandToWin = Math.Round(totalStandToWin.Value, 2) 
        });
    }
}