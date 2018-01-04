﻿using System;
using System.Collections.Generic;
using System.IO;
using codeRR.Server.SqlServer.Tests.Helpers;
using codeRR.Server.Web.Tests.Helpers;
using codeRR.Server.Web.Tests.Helpers.Selenium;
using codeRR.Server.Web.Tests.Helpers.xUnit;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.Extensions;
using Xunit;

namespace codeRR.Server.Web.Tests
{
    [TestCaseOrderer("codeRR.Server.Web.Tests.Helpers.xUnit.TestCaseOrderer", "codeRR.Server.Web.Tests")]
    //[EnsureTestEnvironment]
    public abstract class WebTest
    {
        private static readonly IisExpressHelper _iisExpress;
        private static readonly DatabaseManager _databaseManager = new DatabaseManager();
        private bool _disposed;

        static WebTest()
        {
            _databaseManager.CreateEmptyDatabase();
            _databaseManager.InitSchema();

            AppDomain.CurrentDomain.DomainUnload += (o, e) =>
            {
                _iisExpress?.Stop();
                _databaseManager.Dispose();
            };

            _iisExpress = new IisExpressHelper();
            EnsureTestEnvironmentAttribute.helper = _iisExpress;
            _iisExpress = new IisExpressHelper
            {
                ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "applicationhost.config"),
                EnvironmentVariables =
                    new Dictionary<string, string> { { "coderr_ConnectionString", _databaseManager.ConnectionString } }
            };
            _iisExpress.Start("codeRR.Server.Web");


            TestData = new TestDataManager(_databaseManager.OpenConnection);
        }

        protected WebTest()
        {
            WebDriver = DriverFactory.Create(BrowserType.Chrome);
            AppDomain.CurrentDomain.DomainUnload += (o, e) => { Dispose(); };

            TestData.ResetDatabase(_iisExpress.BaseUrl);
        }


        public string ServerUrl => _iisExpress.BaseUrl;

        public static TestDataManager TestData { get; }

        public IWebDriver WebDriver { get; private set; }


        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                WebDriver.Quit();
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
            WebDriver.Dispose();
        }

        // ReSharper disable once InconsistentNaming
        public void UITest(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                var screenshot = WebDriver.TakeScreenshot();

                var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"{GetType().Name}.{DateTime.Now:yyyMMdd.HHmmss}.png");

                screenshot.SaveAsFile(fileName, ScreenshotImageFormat.Png);

                throw;
            }
        }
    }
}