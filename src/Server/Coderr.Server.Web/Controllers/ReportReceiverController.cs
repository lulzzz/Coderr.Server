﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Security.Claims;
using System.Threading.Tasks;
using Coderr.Server.Abstractions.Config;
using Coderr.Server.Abstractions.Security;
using Coderr.Server.App.Core.Reports.Config;
using Coderr.Server.ReportAnalyzer.Inbound;
using Coderr.Server.Web.Infrastrucutre.Results;
using DotNetCqs.Queues;
using Griffin.Data;
using Griffin.Net.Protocols.Http;
using log4net;
using Microsoft.AspNetCore.Mvc;

namespace Coderr.Server.Web.Controllers
{
    public class ReportReceiverController : Controller
    {
        private readonly ConfigurationStore _configStore;
        private readonly ILog _logger = LogManager.GetLogger(typeof(ReportReceiverController));
        private readonly IMessageQueue _messageQueue;
        private readonly IAdoNetUnitOfWork _unitOfWork;

        public ReportReceiverController(IMessageQueueProvider queueProvider, IAdoNetUnitOfWork unitOfWork,
            ConfigurationStore configStore)
        {
            _unitOfWork = unitOfWork;
            _configStore = configStore;
            _messageQueue = queueProvider.Open("ErrorReports");
        }

        [HttpGet]
        [Route("receiver/report/")]
        public IActionResult Index()
        {
            return Content("Hello world", "text/plain");
        }

        [HttpPost]
        [Route("receiver/report/{appKey}")]
        public async Task<IActionResult> Post(string appKey, string sig)
        {

            if (HttpContext.Request.ContentLength == null || HttpContext.Request.ContentLength < 1)
                return BadRequest("Content is required.");
            if (HttpContext.Request.ContentLength > 2000000)
                return await KillLargeReportAsync(appKey);

            try
            {
                var buffer = new byte[HttpContext.Request.ContentLength.Value];
                Request.Body.Read(buffer, 0, buffer.Length);

                var reportConfig = _configStore.Load<ReportConfig>();
                var handler = new SaveReportHandler(_messageQueue, _unitOfWork, reportConfig);
                var principal = CreateReporterPrincipal();
                var remoteIp = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                await handler.BuildReportAsync(principal, appKey, sig, remoteIp, buffer);
                return NoContent();
            }
            catch (InvalidCredentialException ex)
            {
                return BadRequest(new ErrorMessage("INVALID_APP_KEY"));
            }
            catch (HttpException ex)
            {
                _logger.InfoFormat(ex.Message);
                return new ContentResult
                {
                    Content = ex.Message,
                    StatusCode = ex.HttpCode
                };
            }
            catch (Exception exception)
            {
                _logger.Error("Failed to handle request from " + appKey + " / " + Request.HttpContext.Connection.RemoteIpAddress,
                    exception);
                return new ContentResult
                {
                    Content = exception.Message,
                    StatusCode = (int) HttpStatusCode.InternalServerError
                };
            }
        }

        internal static ClaimsPrincipal CreateReporterPrincipal()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new Claim(ClaimTypes.Name, "ReportReceiver"),
                new Claim(ClaimTypes.NameIdentifier, "0"),
                new Claim(ClaimTypes.Role, CoderrRoles.System)
            }));
            return principal;
        }
        private Task<IActionResult> KillLargeReportAsync(string appKey)
        {
            _logger.Error(appKey + "Too large report: " + Request.ContentLength+ " from " +
                          Request.HttpContext.Connection.RemoteIpAddress);
            //TODO: notify
            return Task.FromResult<IActionResult>(NoContent());
        }

    }
}