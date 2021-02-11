namespace MdlpApiClient.Tests
{
    using System.Security.Cryptography.X509Certificates;
    using NUnit.Framework;
    using MdlpApiClient.Toolbox;
    using System.Linq;
    using System.Runtime.Serialization;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class UnitTestsBase : IDisposable
    {
        // MDLP test stage data
        public const string SystemID1 = "57663471-2d09-4832-9b76-78095fbd0d43"; // идентификатор субъекта обращения
        public const string ClientID1 = "22d12250-6cf3-4a87-b439-f698cfddc498"; // идентификатор учетной системы
        public const string ClientSecret1 = "3deb0ba1-26f2-4516-b652-931fe832e3ff"; // секретный ключ учетной системы
        public const string UserStarter1 = "starter_resident_1"; // имя тестового пользователя
        public const string UserPassword1 = "password"; // пароль тестового пользователя

        public const string SystemID2 = "86325e0c-9a23-4547-ad8a-219b4fc7fd03";
        public const string ClientID2 = "2cabd9b7-6042-40d8-97c2-8627f5704aa1";
        public const string ClientSecret2 = "1713da9a-2042-465c-80ba-4da4dca3323d";
        public const string UserStarter2 = "starter_resident_2";
        public const string UserPassword2 = "password";
        public const string TestDocumentID = "cdeeb2af-bebc-44d6-ad78-4ceb1709b314"; // "60786bb4-fcb5-4587-b703-d0147e3f9d1c";
        public const string TestDocRequestID = "97dad8f1-ef1d-4339-9938-18f129200e5d"; // "528700e0-f967-4ddb-995d-5c6c7b73bcc9";
        public const string TestTicketID = "9d08e171-9ffc-4dce-b1da-d8e2472540ea"; // "e6afe4b3-4cb3-43af-b94e-83fcc358f4b7", TestDocumentID;

        // Custom test data, feel free to replace with your own certificate information
        public const string TestCertificateSubjectName = @"Тестовый УКЭП им. Юрия Гагарина";
        public const string TestCertificateThumbprint = "0a22506a31c3c0c3c16939213e48cdd5d0c03d90";
        public const string TestUserThumbprint = TestCertificateThumbprint;
        public const string TestUserID = "7ae327e3f8b19c0a1101979b4a4b8772cf52219f"; // получен при регистрации

        static UnitTestsBase()
        {
            // detect CI runner environment
            var ci = Environment.GetEnvironmentVariable("GITLAB_CI") != null;
            if (ci)
            {
                TestContext.Progress.WriteLine("Running unit tests on CI server.");
            }

            // // register old ServiceStack library that has limits
            // if (typeof(ServiceStack.Text.JsonSerializer).Assembly.GetName().Version <= new Version("4.0.33.0"))
            // {
            //     var licenseKey = Environment.GetEnvironmentVariable("SERVICE_STACK4_LICENSE");
            //     ServiceStack.Licensing.RegisterLicense(licenseKey);
            // }

            // for continuous integration: use certificates installed on the local machine
            // for unit tests run inside Visual Studio: use current user's certificates
            GostCryptoHelpers.DefaultStoreLocation = ci ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        }

        public UnitTestsBase()
        {
            WriteLine("====> {0} <====", GetType().Name);
        }

        public virtual void Dispose()
        {
            WriteLine("<==== {0}.Dispose() ====>", GetType().Name);
        }

        [SetUp]
        public void SetupBeforeEachTest()
        {
            WriteLine("------> {0} <------", TestContext.CurrentContext.Test.MethodName);
        }

        protected void WriteLine()
        {
        #if TRACE
            TestContext.Progress.WriteLine();
        #endif
        }

        protected void WriteLine(string format = "", params object[] args)
        {
        #if TRACE
            if (args != null && args.Length == 0)
            {
                // avoid formatting curly braces if no arguments are given
                TestContext.Progress.WriteLine(format);
                return;
            }

            TestContext.Progress.WriteLine(format, args);
        #endif
        }

        /// <summary>
        /// Asserts that all required data members are not empty.
        /// </summary>
        /// <typeparam name="T">The type of the data contract.</typeparam>
        /// <param name="dataContract">The instance of the data contract.</param>
        protected void AssertRequiredItems<T>(IEnumerable<T> dataContract)
        {
            Assert.NotNull(dataContract);
            foreach (var item in dataContract)
            {
                AssertRequired(item);
            }
        }

        private Type[] SkipTypesWhenCheckingForRequiredProperties = new[]
        {
            typeof(int), typeof(long), typeof(short), typeof(sbyte),
            typeof(uint), typeof(ulong), typeof(ushort), typeof(byte),
            typeof(decimal), typeof(float),typeof(double), typeof(bool)
        };

        /// <summary>
        /// Asserts that all required data members are not empty.
        /// </summary>
        /// <typeparam name="T">The type of the data contract.</typeparam>
        /// <param name="dataContract">The instance of the data contract.</param>
        protected void AssertRequired<T>(T dataContract)
        {
            Assert.NotNull(dataContract);

            // numeric properties can be 0, and boolean can be false, that's ok
            var requiredMembers =
                from p in typeof(T).GetProperties()
                let dm = p.GetCustomAttributes(typeof(DataMemberAttribute), false)
                    .OfType<DataMemberAttribute>()
                    .FirstOrDefault()
                where dm != null && dm.IsRequired == true
                where !SkipTypesWhenCheckingForRequiredProperties.Contains(p.PropertyType)
                select p;

            var required = requiredMembers.ToArray();
            if (!required.Any())
            {
                return;
            }

            foreach (var p in required)
            {
                var value = p.GetValue(dataContract);
                var defaultValue = p.PropertyType.IsClass ? null : Activator.CreateInstance(p.PropertyType);
                Assert.AreNotEqual(value, defaultValue, "Property " + p.DeclaringType.Name + "." + p.Name + " is not set");
            }
        }
    }
}
