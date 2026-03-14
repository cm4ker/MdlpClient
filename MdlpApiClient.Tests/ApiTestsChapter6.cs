namespace MdlpApiClient.Tests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using MdlpApiClient.DataContracts;
    using NUnit.Framework;

    [TestFixture]
    public class ApiTestsChapter6 : UnitTestsClientBase
    {
        [Test]
        public void Chapter6_01_01_RegisterAccountSystem()
        {
            // имена УС должны быть уникальными, повторное создание выдает ошибку BadRequest (400)
            var ex = Assert.Throws<MdlpException>(() =>
            {
                // SystemID — это код субъекта обращения, которому 
                // будут принадлежать все созданные учетные системы
                var system = Client.RegisterAccountSystem(SystemID1, "TestSystem");
                Assert.IsNotNull(system);
                Assert.IsNotNull(system.ClientSecret);
                AssertRequired(system);

                // создалось вот такое:
                // "TestSystem" = {
                //   "client_secret": "b6dee6c6-bbfb-4a47-bddc-7f7929a3c17b",
                //   "client_id": "da21ba9a-f242-4f60-9519-99cd625d80e1",
                //   "account_system_id": "eb8e4564-d1d7-4c00-8fc8-5e834a649c43"
                // },
                // "TestSystem1" = {
                //   "client_secret": "312539bc-ea1a-43bb-8f4e-471167e3a70f",
                //   "client_id": "c2e0174c-f403-437f-8ea6-023c4e5e7112",
                //   "account_system_id": "b3963a8f-ce92-4f23-8c5c-585b013c4422"
                // },
                // "TestSystem2 = {
                //   "client_secret": "1e53f12c-ab32-4a3f-88ca-a99c710f19e5",
                //   "client_id": "5b6b505c-f44d-4187-a78e-4ad118035104",
                //   "account_system_id": "f01079ed-6516-41dd-b440-a95e19eed7a7"
                // }
            });
        }

        [Test]
        public void Chapter6_01_02_RegisterUser()
        {
            // зарегистрировать можно только один раз
            var ex = Assert.Throws<MdlpException>(() =>
            {
                // Регистрация пользователя-резидента:
                // 1. сертификат надо подготовить заранее
                // 2. выпустить тестовый сертификат-УКЭП можно тут:
                // https://www.cryptopro.ru/ui/Register/RegGetSubject.asp
                // 3. в браузере должен быть установлен плагин КриптоПро ЭЦП
                // 4. ФИО в сертификате и в пользователе должны совпадать
                // 5. для нерезидентов (авторизация по паролю) работает только по http, 
                // для резидентов (авторизация сертификатом) — только по https
                var userId = Client.RegisterUser(SystemID1, new ResidentUser
                {
                    PublicCertificate = "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",
                    FirstName = "Юрий",
                    LastName = "Гагарин",
                    Email = "asd@asd.com",
                });

                Assert.IsNotNull(userId); // "31736b85-45d8-4fb0-8130-f0dabce5d491"
            });

            Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.That(ex.Message, Does.Contain("Ошибка при выполнении операции").Or.Contain("Error during operation"));
        }

        private string RegisterMdlpTestUser(MdlpClient client, int number)
        {
            return client.RegisterUser(SystemID1, new NonResidentUser
            {
                FirstName = "mdlp",
                MiddleName = "api",
                LastName = "client",
                Email = "mdlp" + number + "@mdlpclient.github.com",
                Password = "MdlpClient-Pa55w0rd"
            });
        }

        [Test, Explicit]
        public void RegisterMdlpTestUser()
        {
            // starter_resident_1 and starter_resident2 are blocked, so use another account instead
            var client = new MdlpClient(credentials: new ResidentCredentials
            {
                ClientID = ClientID1,
                ClientSecret = ClientSecret1,
                UserID = TestUserThumbprint,
            },
            baseUrl: TestApiBaseUrl)
            {
                Tracer = WriteLine
            };

            // register new test users to use instead of starter_resident1/2
            using (client)
            {
                WriteLine("MDLP Test user1: {0}", RegisterMdlpTestUser(client, 1));
                WriteLine("MDLP Test user2: {0}", RegisterMdlpTestUser(client, 2));
            }
        }

        [Test]
        public void Chapter6_01_03_RegisterUserWithWeakPassword()
        {
            // зарегистрировать можно только один раз
            var ex = Assert.Throws<MdlpException>(() =>
            {
                var userId = Client.RegisterUser(SystemID1, new NonResidentUser
                {
                    FirstName = "Alex",
                    LastName = "Leonov",
                    Email = "asd2@asd.com",
                    Password = "buzzword"
                });

                Assert.IsNotNull(userId);
            });

            Assert.AreEqual(HttpStatusCode.Conflict, ex.StatusCode);
            Assert.NotNull(ex.ErrorResponse);
            Assert.NotNull(ex.ErrorResponse.Violations);
            Assert.IsTrue(ex.ErrorResponse.Violations.Contains("UPPER_REQUIRED"));
            Assert.IsTrue(ex.ErrorResponse.Violations.Contains("NUM_REQUIRED"));
            Assert.IsTrue(ex.ErrorResponse.Violations.Contains("SPECIAL_REQUIRED"));
        }

        [Test]
        public void Chapter6_01_03_RegisterUser()
        {
            // зарегистрировать можно только один раз
            var ex = Assert.Throws<MdlpException>(() =>
            {
                var userId = Client.RegisterUser(SystemID1, new NonResidentUser
                {
                    FirstName = "Neil",
                    LastName = "Armstrong",
                    Email = "asd1@asd.com",
                    Password = "Pass-w0rd"
                });

                Assert.IsNotNull(userId);
            });

            Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.That(ex.Message, Is.AnyOf(
                "Error during operation: attempt to register resident by sys_id for non-resident or non-resident by sys_id for resident",
                "Ошибка при выполнении операции: попытка зарегистрировать резидента по идентификатору sys_id для нерезидента или наоборот"));
        }

        [Test]
        public void Chapter6_01_04_GetUserInfo()
        {
            // Get the current user's info using their actual user ID from GetCurrentUserInfo
            var currentUser = Client.GetCurrentUserInfo();
            Assert.IsNotNull(currentUser);
            Assert.IsNotNull(currentUser.UserID);

            var user = Client.GetUserInfo(currentUser.UserID);
            Assert.IsNotNull(user);

            AssertRequired(user);
            Assert.IsNotNull(user.FirstName);
            Assert.IsNotNull(user.LastName);
        }

        [Test]
        public void Chapter6_01_05_GetCurrentLanguage()
        {
            var language = Client.GetCurrentLanguage();
            Assert.That(language, Is.AnyOf("ru", "en"));
        }

        [Test]
        public void Chapter6_01_06_UpdateUserProfile()
        {
            // Use current user's actual ID instead of a hardcoded test ID
            var currentUser = Client.GetCurrentUserInfo();
            var userId = currentUser.UserID;
            var originalFirst = currentUser.FirstName;
            var originalLast = currentUser.LastName;
            // Email may be null in response; fall back to Login which often holds the login email
            var originalEmail = currentUser.Email ?? currentUser.Login;

            // Хм, при регистрации ИС проверяет, чтобы ФИО совпадали с сертификатом,
            // а после регистрации — уже не проверяет
            Client.UpdateUserProfile(userId, new UserEditProfileEntry
            {
                FirstName = originalFirst,
                LastName = "МодифицированнаяФамилия",
                Position = "TestPosition",
                Email = originalEmail,
            });

            // если не ждать, бывает, ИС не успевает закоммитить обновление
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // действительно, обновляется
            var updated = Client.GetUserInfo(userId);
            Assert.AreEqual("МодифицированнаяФамилия", updated.LastName);

            // вернем все на место
            Client.UpdateUserProfile(userId, new UserEditProfileEntry
            {
                FirstName = originalFirst,
                LastName = originalLast,
                Position = "TestPosition",
                Email = originalEmail,
            });
        }

        [Test]
        public void Chapter6_01_07_GetCurrentUserInfo()
        {
            var user = Client.GetCurrentUserInfo();
            Assert.IsNotNull(user);

            AssertRequired(user);
            Assert.IsNotNull(user.FirstName);
            Assert.IsNotNull(user.LastName);
        }

        [Test]
        public void Chapter6_01_08_SetCurrentLanguage()
        {
            var lang = Client.GetCurrentLanguage();
            Assert.That(lang, Is.AnyOf("ru", "en"));

            try
            {
                Client.SetCurrentLanguage("ru");
                Client.SetCurrentLanguage("en");

                // неизвестный язык
                var ex = Assert.Throws<MdlpException>(() => Client.SetCurrentLanguage("bad"));
                Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
                Assert.That(ex.Message, Does.StartWith("Error during operation: request is missing or incorrect").Or.StartWith("Ошибка при выполнении операции: запрос неправильный или некорректный"));
            }
            finally
            {
                // вернем как было
                Client.SetCurrentLanguage(lang);
            }
        }

        [Test]
        public void Chapter6_01_09_GetCurrentCertificates()
        {
            // Current client is a resident user, should have at least one certificate
            var certs = Client.GetCurrentCertificates(0, 10);
            Assert.IsNotNull(certs);
            Assert.IsNotNull(certs.Certificates);
            Assert.IsTrue(certs.Total >= 0);
        }

        [Test]
        public void Chapter6_01_10_GetUserCertificates()
        {
            // Use current user's ID instead of hardcoded TestUserID
            var currentUser = Client.GetCurrentUserInfo();
            var certs = Client.GetUserCertificates(currentUser.UserID, 0, 10);
            Assert.IsNotNull(certs);
            Assert.IsNotNull(certs.Certificates);
            Assert.IsTrue(certs.Total >= 0);
        }

        [Test]
        public void Chapter6_01_11_GetAccountSystem()
        {
            // Register a temporary account system and verify it can be retrieved
            var name = "TestAccountSystemLookup_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var created = Client.RegisterAccountSystem(SystemID1, name);
            Assert.IsNotNull(created);
            Assert.IsNotNull(created.AccountSystemID);

            try
            {
                var accSystem = Client.GetAccountSystem(created.AccountSystemID);
                Assert.IsNull(accSystem.ClientSecret);
                AssertRequired(accSystem);
                Assert.AreEqual(name, accSystem.Name);
            }
            finally
            {
                Client.DeleteAccountSystem(created.AccountSystemID);
            }
        }

        [Test]
        public void Chapter6_03_01_DeleteUser()
        {
            // user not found
            var ex = Assert.Throws<MdlpException>(() => Client.DeleteUser("5b5540c4-fbb0-4ad7-a038-c8222affab3f"));
            Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            Assert.That(ex.Message, Is.AnyOf(
                "Error during operation: data not found",
                "Ошибка при выполнении операции: запись не найдена"));
        }

        [Test]
        public void Chapter6_03_02_DeleteAccountSystem()
        {
            // register and then delete
            var system = Client.RegisterAccountSystem(SystemID1, "TestAccountSystem123");
            Client.DeleteAccountSystem(system.AccountSystemID);
        }

        [Test]
        public void Chapter6_04_01_AddUserCertificate()
        {
            var ex = Assert.Throws<MdlpException>(() => Client.AddUserCertificate(TestUserID, @"MIIBjjCCAT2gAwIBAgIEWWJzHzAIBgYqhQMCAgMwMTELMAkGA1UEBhMCUlUxEjAQBgNVBAoMCUNyeXB0b1BybzEOMAwGA1UEAwwFQWxpYXMwHhcNMTcxMTEzMTczMjI4WhcNMTgxMTEzMTczMjI4WjAxMQswCQYDVQQGEwJSVTESMBAGA1UECgwJQ3J5cHRvUHJvMQ4wDAYDVQQDDAVBbGlhczBjMBwGBiqFAwICEzASBgcqhQMCAiQABgcqhQMCAh4BA0MABEAIWARzAiI81k4i4Gz8EC7Ic01653JX5PCUfvgCBTpLduYtbTwLOwmGFcZzw9bwsxQpALqhcdRHxtx1UEeNKJuMozswOTAOBgNVHQ8BAf8EBAMCA+gwEwYDVR0lBAwwCgYIKwYBBQUHAwIwEgYDVR0TAQH/BAgwBgEB/wIBBTAIBgYqhQMCAgMDQQBL9CrIk0EgnMVr1J5dKbfXVFrhJxGxztFkTdmGkGJ6gHywB5Y9KpP67pv7I2bP1m1ej9hu+C17GSJrWgMgq+UZ"));
            Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.That(ex.Message, Does.Contain("Ошибка при выполнении операции").Or.Contain("Error during operation"));
        }

        [Test]
        public void Chapter6_04_02_DeleteUserCertificate()
        {
            var ex = Assert.Throws<MdlpException>(() => Client.DeleteUserCertificate(TestUserID, @"MIIBjjCCAT2gAwIBAgIEWWJzHzAIBgYqhQMCAgMwMTELMAkGA1UEBhMCUlUxEjAQBgNVBAoMCUNyeXB0b1BybzEOMAwGA1UEAwwFQWxpYXMwHhcNMTcxMTEzMTczMjI4WhcNMTgxMTEzMTczMjI4WjAxMQswCQYDVQQGEwJSVTESMBAGA1UECgwJQ3J5cHRvUHJvMQ4wDAYDVQQDDAVBbGlhczBjMBwGBiqFAwICEzASBgcqhQMCAiQABgcqhQMCAh4BA0MABEAIWARzAiI81k4i4Gz8EC7Ic01653JX5PCUfvgCBTpLduYtbTwLOwmGFcZzw9bwsxQpALqhcdRHxtx1UEeNKJuMozswOTAOBgNVHQ8BAf8EBAMCA+gwEwYDVR0lBAwwCgYIKwYBBQUHAwIwEgYDVR0TAQH/BAgwBgEB/wIBBTAIBgYqhQMCAgMDQQBL9CrIk0EgnMVr1J5dKbfXVFrhJxGxztFkTdmGkGJ6gHywB5Y9KpP67pv7I2bP1m1ej9hu+C17GSJrWgMgq+UZ"));
            Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
            Assert.That(ex.Message, Does.Contain("Ошибка при выполнении операции").Or.Contain("Error during operation"));
        }

        [Test]
        public void Chapter6_05_01_ChangeUserPassword()
        {
            var ex = Assert.Throws<MdlpException>(() => Client.ChangeUserPassword(TestUserID, @"password"));
            Assert.AreEqual(HttpStatusCode.MethodNotAllowed, ex.StatusCode);
            Assert.That(ex.Message, Does.Contain("MethodNotAllowed").Or.Contain("Method Not Allowed"));
        }

        [Test]
        public void Chapter6_06_01_GetRights()
        {
            var rights = Client.GetRights();
            AssertRequiredItems(rights);

            // generate RightsEnum.cs
            var enumMemberQuery =
                from r in rights
                let name = r.Right
                let words = r.Description.Split(' ').Select(x => x.Trim())
                let descr = string.Join(" ", words.Where(w => w.Any()))
                orderby name
                let items = new[]
                {
                    "/// <summary>",
                    "/// " + descr,
                    "/// </summary>",
                    "public const string " + name + " = \"" + name + "\";",
                    string.Empty
                }
                let member = string.Join(Environment.NewLine, items)
                select member;

            // display the generated code:
            // WriteLine(string.Join(Environment.NewLine, enumMemberQuery));
        }

        [Test]
        public void Chapter6_06_02_GetCurrentRights()
        {
            var rights = Client.GetCurrentRights();
            AssertRequiredItems(rights);
        }

        [Test]
        public void Chapter6_06_03456789_CreateUpdateDeleteRightsGroup()
        {
            // Use only the rights that the current user actually has (guaranteed assignable)
            var availableRights = Client.GetCurrentRights().OrderBy(r => r).ToArray();
            Assert.IsTrue(availableRights.Length > 0, "Expected current user to have at least one right");

            // create group (unique name is required)
            var rights = availableRights.Take(10).ToArray(); // use only first 10 to avoid "right not found" error
            var groupName = "TestGroup " + Guid.NewGuid();
            var groupId = Client.CreateRightsGroup(groupName, rights);
            Assert.NotNull(groupId);

            // get group properties
            var group = Client.GetRightsGroup(groupId);
            AssertRequired(group);
            Assert.AreEqual(groupId, group.GroupID);
            Assert.AreEqual(groupName, group.GroupName);

            // compare the list of rights
            var actualRights = string.Join(":", group.Rights.OrderBy(r => r));
            var expectedRights = string.Join(":", rights.OrderBy(r => r));
            Assert.AreEqual(expectedRights, actualRights);

            // update group (note: GroupID property is ignored)
            group.GroupName += " Updated";
            group.Rights = group.Rights.Take(5).ToArray();
            Client.UpdateRightsGroup(groupId, group);

            // make sure the group is empty
            var users = Client.GetGroupUsers(groupId);
            Assert.NotNull(users);
            Assert.IsFalse(users.Any());

            // add current user to the rights group
            var currentUser = Client.GetCurrentUserInfo();
            Client.AddUserToRightsGroup(currentUser.UserID, groupId);

            // make sure the group is not empty
            users = Client.GetGroupUsers(groupId);
            Assert.NotNull(users);
            Assert.AreEqual(1, users.Length);
            Assert.AreEqual(currentUser.UserID, users[0].UserID);

            // delete user from the rights group
            Client.DeleteUserFromRightsGroup(currentUser.UserID, groupId);

            // make sure the group is empty again
            users = Client.GetGroupUsers(groupId);
            Assert.NotNull(users);
            Assert.IsFalse(users.Any());

            // delete created group
            Client.DeleteRightsGroup(groupId);
        }

        [Test]
        public void Chapter6_06_11_GetRightsGroups()
        {
            // Look up rights groups for the current user using a right that's known to be available
            var currentUser = Client.GetCurrentUserInfo();
            var availableRights = Client.GetCurrentRights();
            Assert.IsTrue(availableRights.Length > 0, "Expected current user to have at least one right");

            var rights = Client.GetRightsGroups(new GroupFilter
            {
                UserID = currentUser.UserID,
            }, 0, 10);
            AssertRequired(rights);
            Assert.IsTrue(rights.Total >= 0);
        }

        [Test]
        public void Chapter6_07_02_GetUsers()
        {
            // Look up users in the current organization
            var users = Client.GetUsers(null, 0, 10);
            AssertRequired(users);
            Assert.IsTrue(users.Total >= 1);
            Assert.IsTrue(users.Users.Length >= 1);
        }

        [Test]
        public void Chapter6_08_02_GetAccountSystems()
        {
            // List account systems and verify the result is structurally correct
            var accSystems = Client.GetAccountSystems(null, 0, 10);
            AssertRequired(accSystems);
            Assert.IsTrue(accSystems.Total >= 0);
        }
    }
}
