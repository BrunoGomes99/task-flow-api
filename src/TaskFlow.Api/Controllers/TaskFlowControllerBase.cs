using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Http;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Api.Controllers;

public abstract class TaskFlowControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result) =>
        result.ToActionResult(HttpContext);

    protected IActionResult FromResult<T>(Result<T> result) =>
        result.ToHttpActionResult<T>(HttpContext);
}
