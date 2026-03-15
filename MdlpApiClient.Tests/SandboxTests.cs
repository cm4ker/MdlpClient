namespace MdlpApiClient.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Xml.Linq;
    using MdlpApiClient.DataContracts;
    using MdlpApiClient.Serialization;
    using MdlpApiClient.Toolbox;
    using MdlpApiClient.Xsd;
    using NUnit.Framework;

    [TestFixture] // Ignore("Sandbox server is temporarily down")
    public class SandboxTests : UnitTestsClientBase
    {
        private static readonly string[] KnownSsccCandidates =
        {
            "507540413987451236",
            "000000000105900000",
            "147600887000110010",
            "000000111100000097",
            "000000111100000100",
        };

        private const string KnownWorkflowSenderId = "00000000104494";
        private const string KnownWorkflowGtin = "50754041398745";

        private sealed class SsccQueryCandidate
        {
            public SsccQueryCandidate(string senderId, string sscc, int sourceDocType, string sourceDocumentId)
            {
                SenderId = senderId;
                Sscc = sscc;
                SourceDocType = sourceDocType;
                SourceDocumentId = sourceDocumentId;
            }

            public string SenderId { get; }

            public string Sscc { get; }

            public int SourceDocType { get; }

            public string SourceDocumentId { get; }
        }

        private sealed class SentDocumentReference
        {
            public SentDocumentReference(string documentId, string requestId)
            {
                DocumentId = documentId;
                RequestId = requestId;
            }

            public string DocumentId { get; }

            public string RequestId { get; }
        }

        private sealed class WorkflowSeedCandidate
        {
            public WorkflowSeedCandidate(string senderId, string gtin, string source)
            {
                SenderId = senderId;
                Gtin = gtin;
                Source = source;
            }

            public string SenderId { get; }

            public string Gtin { get; }

            public string Source { get; }
        }

        [DataContract]
        private sealed class SendDocumentResult
        {
            [DataMember(Name = "document_id")]
            public string DocumentId { get; set; }
        }

        private readonly List<string> _hierarchy221Diagnostics = new List<string>();

        private static readonly MethodInfo ComputeSignatureMethod = typeof(MdlpClient)
            .GetMethod("ComputeSignature", BindingFlags.Instance | BindingFlags.NonPublic);

        protected override MdlpClient CreateClient()
        {
            // Типография для типографий
            var cred = new ResidentCredentials
            {
                ClientID = ClientID1,
                ClientSecret = ClientSecret1,
                UserID = SandboxUserThumbprint1,
            };

            // подключаемся на этот раз к песочнице
            return new MdlpClient(cred, TestApiBaseUrl)
            {
                ApplicationName = "SandboxTests v1.0",
                Tracer = WriteLine,
            };
        }

        private MdlpClient CreateSecondClient()
        {
            // Автомойка-Чисто
            var cred = new ResidentCredentials
            {
                ClientID = ClientID2,
                ClientSecret = ClientSecret2,
                UserID = SandboxUserThumbprint2,
            };

            // подключаемся на этот раз к песочнице
            return new MdlpClient(cred, TestApiBaseUrl)
            {
                ApplicationName = "SandboxTests v2.0",
                Tracer = WriteLine,
            };
        }

        private MdlpClient CreateHierarchyQueryClient()
        {
            var cred = new ResidentCredentials
            {
                ClientID = ClientID1,
                ClientSecret = ClientSecret1,
                UserID = TestUserThumbprint,
            };

            var client = new MdlpClient(cred, TestApiBaseUrl)
            {
                ApplicationName = "SandboxTests 221 Dynamic",
                LargeDocumentSize = 1024 * 1024,
                Tracer = WriteLine,
            };

            client.Client.RemoteCertificateValidationCallback += (sender, certificate, chain, errors) => true;
            return client;
        }

        private void NoteHierarchy221(string message)
        {
            _hierarchy221Diagnostics.Add(message);
            WriteLine(message);
        }

        private static bool IsFixedLengthDigits(string value, int length)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.Length == length &&
                value.All(char.IsDigit);
        }

        [Test]
        public void SandboxAuthorizationWorks()
        {
            Assert.DoesNotThrow(() =>
            {
                var size = Client.GetLargeDocumentSize();
                Assert.IsTrue(size > 100);
                WriteLine("Large doc size = {0} bytes", size);

                Assert.IsTrue(Client.SignatureSize > 0);
                WriteLine("Signature size = {0} bytes", Client.SignatureSize);
            });
        }

        private Documents CreateDocument311()
        {
            // Создаем документ схемы 311 от имени организации Типография для типографий
            // sessionUi — просто Guid, объединяющий документы в смысловую группу
            var sessionUi = "ca9a64ee-cf25-42af-a939-94d98fa16ab6";
            var doc = new Documents
            {
                // Если не указать версию, загрузка документа не срабатывает:
                // пишет, что тип документа не определен
                Version = "1.34",
                Session_Ui = sessionUi,

                // Окончание упаковки = схема 10311
                Skzkm_Register_End_Packing = new Skzkm_Register_End_Packing
                {
                    // из личного кабинета тестового участника-Типографии
                    // берем код места деятельности, расположенного по адресу:
                    // край Забайкальский р-н Могойтуйский пгт Могойтуй ул Банзарова
                    Subject_Id = "00000000104494",

                    // в этом месте мы сегодня заканчиваем упаковку препаратов
                    Operation_Date = DateTime.Now,

                    // Тип производственного заказа — собственное производство = 1
                    // В сгенерированных XML-классах для числовых кодов
                    // созданы элементы Item: 1 = Item1, 40 = Item40, и т.д.
                    Order_Type = Order_Type_Enum.Item1,

                    // Номер производственной серии, 1-20 символов
                    Series_Number = "100000003",

                    // срок годности выдается в строковом виде, в формате ДД.ММ.ГГГГ
                    Expiration_Date = "30.03.2025",

                    // Код препарата. GTIN – указывается из реестра ЛП тестового участника
                    // из личного кабинета тестового участника-Типографии берем ЛП: Найзин
                    Gtin = "50754041398745"
                }
            };

            // Перечень идентификационных кодов потребительских упаковок.
            // Идентификаторы SGTIN. – формируются путем добавления к GTIN 
            // 13-значного серийного номера. Для каждой отгрузки 
            // необходимо генерировать уникальный серийный номер
            var gtin = doc.Skzkm_Register_End_Packing.Gtin;
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "1234567906123");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "1234567907123");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "1234567908123");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "1234567909123");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "123456790A123");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "123456790B123");

            // Документ загружен, но не обработан: fe4120a9-0485-4b0d-a878-fba3d7a644bd.
            // Похоже, серверу-таки не нравится XML-декларация: <?xml version="1.0" encoding="..." ?>
            // Документ загружен и обработан: 72c55992-83de-4101-919b-20d985f06bb0
            return doc;
        }

        [Test, Explicit("Can't upload the same document more than once")]
        public void SendDocument311ToSandbox()
        {
            // формируем документ для загрузки в ЛК
            var doc = CreateDocument311();
            var docId = Client.SendDocument(doc);
            WriteLine("Uploaded document #311: {0}", docId);
        }

        [Test]
        public void GetDocument311FromSandbox()
        {
            // прежние документы схемы 311:
            // "72c55992-83de-4101-919b-20d985f06bb0" — ок, но его удалили
            // "43e26ea9-7f84-4b92-bd94-37d897ed2a45" — после загрузки была ошибка обработки
            var document = Client.GetDocument("e5d3b7c3-a472-44c4-92c8-4feb3c2632a9");
            Assert.NotNull(document);
            Assert.NotNull(document.Skzkm_Register_End_Packing);
            Assert.NotNull(document.Skzkm_Register_End_Packing.Signs);
            Assert.AreEqual(6, document.Skzkm_Register_End_Packing.Signs.Count);
            Assert.AreEqual("00000000104494", document.Skzkm_Register_End_Packing.Subject_Id);
        }

        [Test]
        public void GetTicketForDocument311FromSandbox()
        {
            // квитанция об обработке документа появляется через какое-то время после загрузки
            // прежние документы схемы 311: 
            // "72c55992-83de-4101-919b-20d985f06bb0" — ок, но его удалили
            // "43e26ea9-7f84-4b92-bd94-37d897ed2a45" — после загрузки была ошибка обработки
            var ticket = Client.GetTicket("e5d3b7c3-a472-44c4-92c8-4feb3c2632a9");
            Assert.NotNull(ticket);
            Assert.NotNull(ticket.Result);
            Assert.AreEqual("311", ticket.Result.Operation);
            Assert.AreEqual("Успешное завершение операции", ticket.Result.Operation_Comment);
        }

        private Documents CreateDocument313()
        {
            // Создаем документ схемы 313 от имени организации Типография для типографий
            // sessionUi — просто Guid, объединяющий документы в смысловую группу
            // оставляем его прежним, чтобы связать с документом завершения упаковки 311
            var sessionUi = "ca9a64ee-cf25-42af-a939-94d98fa16ab6";
            var doc = new Documents
            {
                // Если не указать версию, загрузка документа не срабатывает:
                // пишет, что тип документа не определен
                Version = "1.34",
                Session_Ui = sessionUi,

                // Регистрация сведений о вводе ЛП в оборот (выпуск продукции) = схема 313
                Register_Product_Emission = new Register_Product_Emission
                {
                    // Идентификатор места деятельности (14 знаков) — 
                    // указывается идентификатор из ранее загруженной схемы 311:
                    // где упаковали, там и вводим ЛП в оборот
                    Subject_Id = "00000000104494",

                    // выпускаем препараты сегодня
                    Operation_Date = DateTime.Now,

                    // Реквизиты сведений о вводе ЛП в оборот
                    Release_Info = new Release_Info_Type
                    {
                        // Регистрационный номер документа подтверждения соответствия
                        Doc_Num = "123а",

                        // Дата регистрации документа подтверждения соответствия
                        Doc_Date = DateTime.Today.ToString(@"dd\.MM\.yyyy"),

                        // Номер документа подтверждения соответствия
                        Confirmation_Num = "123b",
                    },

                    // тут надо создать вложенный пустой объект
                    Signs = new Register_Product_EmissionSigns()
                }
            };

            // Перечень идентификационных кодов потребительских упаковок.
            // Идентификаторы SGTIN – указываются
            // номера из ранее загруженной 311 схемы
            var gtin = "50754041398745";
            var sgtins = doc.Register_Product_Emission.Signs.Sgtin;
            sgtins.Add(gtin + "1234567906123");
            sgtins.Add(gtin + "1234567907123");
            sgtins.Add(gtin + "1234567908123");
            sgtins.Add(gtin + "1234567909123");
            sgtins.Add(gtin + "123456790A123");
            sgtins.Add(gtin + "123456790B123");

            // В песочницу документ загружен через API и обработан,
            // получил код ecff5436-9a5d-408f-8d3b-0dd2eb6cad54
            return doc;
        }

        [Test, Explicit("Can't upload the same document more than once")]
        public void SendDocument313ToSandbox()
        {
            var doc = CreateDocument313();
            var docId = Client.SendDocument(doc);
            WriteLine("Uploaded document #313: {0}", docId);
        }

        [Test]
        public void GetDocument313FromSandbox()
        {
            // прежние документы схемы 313:
            // "ecff5436-9a5d-408f-8d3b-0dd2eb6cad54" — ок, но был удален
            var document = Client.GetDocument("728e315c-ee06-418d-82b0-79357eed8eb0");
            Assert.NotNull(document);
            Assert.NotNull(document.Register_Product_Emission);
            Assert.NotNull(document.Register_Product_Emission.Signs);
            Assert.AreEqual(6, document.Register_Product_Emission.Signs.Sgtin.Count);
            Assert.AreEqual("00000000104494", document.Register_Product_Emission.Subject_Id);
        }

        [Test]
        public void GetTicketForDocument313FromSandbox()
        {
            // прежние документы схемы 313:
            // "ecff5436-9a5d-408f-8d3b-0dd2eb6cad54" — ок, но был удален
            var ticket = Client.GetTicket("728e315c-ee06-418d-82b0-79357eed8eb0");
            Assert.NotNull(ticket);
            Assert.NotNull(ticket.Result);
            Assert.AreEqual("313", ticket.Result.Operation);
            Assert.AreEqual("Успешное завершение операции", ticket.Result.Operation_Comment);
        }

        private Documents CreateDocument915()
        {
            // Создаем документ схемы 915 от имени организации Типография для типографий
            // sessionUi оставляем прежним, чтобы связать с документами 
            // завершения упаковки 311 и ввода ЛП в оборот 313
            var sessionUi = "ca9a64ee-cf25-42af-a939-94d98fa16ab6";
            var doc = new Documents
            {
                // Если не указать версию, загрузка документа не срабатывает:
                // пишет, что тип документа не определен
                Version = "1.34",
                Session_Ui = sessionUi,

                // Регистрация в ИС МДЛП сведений об агрегировании во множество
                // третичных (заводских, транспортных) упаковок = схема 915
                Multi_Pack = new Multi_Pack
                {
                    // Идентификатор места деятельности (14 знаков) — 
                    // указывается идентификатор из ранее загруженной схемы 311:
                    // где упаковали и ввели ЛП в оборот, там и пакуем в коробку
                    Subject_Id = "00000000104494",

                    // дата упаковки
                    Operation_Date = DateTime.Now,

                    // вложены только SGTIN
                }
            };

            // в документе схемы 915 можно упаковать либо SGTIN, 
            // либо SSCC (третичную упаковку), но не одновременно
            var sgtinPack = new Multi_PackBy_SgtinDetail
            {
                // Идентификатор SSCC (откуда он берется?)
                // Для тестов: если делать новый документ, номер нужно увеличить на 1
                // Ниже по тексту есть обращение к этому номеру, его тоже поправить
                Sscc = "507540413987451236",
            };

            // Перечень идентификационных кодов потребительских упаковок.
            // Идентификаторы SGTIN – указываем первые четыре номера
            // номера из ранее загруженных схем 311 и 313
            // Первые 4 упакуем, оставшиеся 2 оставим неупакованными
            var gtin = "50754041398745";
            sgtinPack.Content.Add(gtin + "1234567906123");
            sgtinPack.Content.Add(gtin + "1234567907123");
            sgtinPack.Content.Add(gtin + "1234567908123");
            sgtinPack.Content.Add(gtin + "1234567909123");
            doc.Multi_Pack.By_Sgtin.Add(sgtinPack);

            // В песочницу документ загружен через API и обработан,
            // получил код 9534c7a7-7149-466a-aad2-1be19de810d6
            return doc;
        }

        [Test, Explicit("Can't upload the same document more than once")]
        public void SendDocument915ToSandbox()
        {
            var doc = CreateDocument915();
            var docId = Client.SendDocument(doc);
            WriteLine("Uploaded document #915: {0}", docId);
        }

        [Test]
        public void GetDocument915FromSandbox()
        {
            // прежние документы схемы 915:
            // "9534c7a7-7149-466a-aad2-1be19de810d6" — удален
            // "2e41f73b-5310-4cb4-917c-4d7ac0f7ecc3" — отклонен
            var document = Client.GetDocument("423d7e82-62c0-4f23-a421-158ec90f0ee3");
            Assert.NotNull(document);
            Assert.NotNull(document.Multi_Pack);
            Assert.NotNull(document.Multi_Pack.By_Sgtin);
            Assert.AreEqual(1, document.Multi_Pack.By_Sgtin.Count);
            Assert.AreEqual("00000000104494", document.Multi_Pack.Subject_Id);
        }

        [Test]
        public void GetTicketForDocument915FromSandbox()
        {
            // прежние документы схемы 915:
            // "9534c7a7-7149-466a-aad2-1be19de810d6" — удален
            // "2e41f73b-5310-4cb4-917c-4d7ac0f7ecc3" — отклонен
            var ticket = Client.GetTicket("423d7e82-62c0-4f23-a421-158ec90f0ee3");
            Assert.NotNull(ticket);
            Assert.NotNull(ticket.Result);
            Assert.AreEqual("915", ticket.Result.Operation);
            Assert.AreEqual("Успешное завершение операции", ticket.Result.Operation_Comment);
        }

        private Documents CreateDocument415()
        {
            // Создаем документ схемы 415 от имени организации Типография для типографий
            // sessionUi — просто Guid, объединяющий документы в смысловую группу
            var sessionUi = "ca9a64ee-cf25-42af-a939-94d98fa16ab6";
            var doc = new Documents
            {
                // Если не указать версию, загрузка документа не срабатывает:
                // пишет, что тип документа не определен
                Version = "1.34",
                Session_Ui = sessionUi,

                // Перемещение на склад получателя = схема 415
                Move_Order = new Move_Order
                {
                    // из личного кабинета тестового участника-Типографии
                    // берем код места деятельности, расположенного по адресу:
                    // край Забайкальский р-н Могойтуйский пгт Могойтуй ул Банзарова
                    // здесь у нас пока хранятся упакованные ЛП, ждущие отправки
                    Subject_Id = "00000000104494",

                    // из ЛК тестового участника-Автомойки
                    // берем код места деятельности, расположенного по адресу:
                    // Респ Адыгея р-н Тахтамукайский пгт Яблоновский ул Гагарина
                    // сюда мы будем отправлять упакованные ЛП
                    Receiver_Id = "00000000104453",

                    // сегодня отправляем препараты
                    Operation_Date = DateTime.Now,

                    // Реквизиты документа отгрузки: номер и дата документа
                    Doc_Num = "123а",
                    Doc_Date = DateTime.Today.ToString(@"dd\.MM\.yyyy"),

                    // Тип операции отгрузки со склада: 1 - продажа, 2 - возврат
                    Turnover_Type = Turnover_Type_Enum.Item1,

                    // Источник финансирования: 1 - собственные средства, 2-3 - бюджет
                    Source = Source_Type.Item1,

                    // Тип договора: 1 - купля-продажа
                    Contract_Type = Contract_Type_Enum.Item1,

                    // Реестровый номер контракта (договора)
                    // в Единой информационной системе в сфере закупок
                    // нам в данном случае не требуется
                    Contract_Num = null,
                },
            };

            // Список отгружаемой продукции
            var order = doc.Move_Order.Order_Details;
            order.Add(new Move_OrderOrder_DetailsUnion
            {
                // берем зарегистрированный КИЗ, который был в документе 311,
                // введен в оборот документом 313, но не упаковам документом 915
                Sgtin = "50754041398745" + "123456790A123",

                // цена единицы продукции
                Cost = 1000,

                // сумма НДС
                Vat_Value = 100,
            });

            // и еще один — упакованный ящик, полученный в документе схемы 915
            // если у нас несколько уровней упаковки, сведения подаются только
            // по коробу самого верхнего уровня
            var ssccItem = new Move_OrderOrder_DetailsUnion
            {
                // Sgtin отсутствует, зато присутствует Sscc_Detail
                Sscc_Detail = new Move_OrderOrder_DetailsUnionSscc_Detail
                {
                    // код третичной упаковки тот же, что был в документе схемы 915
                    Sscc = "507540413987451236"
                },

                // а вот нужны ли здесь Cost и Vat_Value, непонятно,
                // поскольку они есть в самом Sscc_Detail
                Cost = 1000,
                Vat_Value = 100,
            };

            // Здесь может быть и пусто
            // Тут указываются только те препараты, у которых цена не совпадает с той, что указана на коробе
            // Такое бывает, если в короб вложены несколько разных препаратов
            ssccItem.Sscc_Detail.Detail.Add(new Move_OrderOrder_DetailsUnionSscc_DetailDetail
            {
                // GTIN и номер производственной серии — из документа 311
                Gtin = "50754041398745",
                Series_Number = "100000003",

                // стоимость единицы продукции с учетом НДС и сумма НДС, руб
                Cost = 1000,
                Vat_Value = 100,
            });

            // итого в документе перемещения схемы 415 у нас один SGTIN
            // и один ящик SSCC с четырьмя SGTIN-ами,
            // а еще один неупакованный SGTIN остался в типографии
            order.Add(ssccItem);

            // документ загружен через API и обработан,
            // получил код a8528183-b4d9-4e1c-b1ed-c2d6dab97504
            return doc;
        }

        [Test, Explicit("Can't upload the same document more than once")]
        public void SendDocument415ToSandbox()
        {
            var doc = CreateDocument415();
            var docId = Client.SendDocument(doc);
            WriteLine("Uploaded document #415: {0}", docId);
        }

        [Test]
        public void GetDocument415FromSandbox()
        {
            // прежние документы схемы 415:
            // "667f1d2f-0d4f-43c4-9d56-b5d3da29c4ac" — удален
            var document = Client.GetDocument("a8528183-b4d9-4e1c-b1ed-c2d6dab97504");
            Assert.NotNull(document);
            Assert.NotNull(document.Move_Order);
            Assert.NotNull(document.Move_Order.Order_Details);
            Assert.AreEqual(2, document.Move_Order.Order_Details.Count);
            Assert.AreEqual("00000000104494", document.Move_Order.Subject_Id);
        }

        [Test]
        public void GetTicketForDocument415FromSandbox()
        {
            // прежние документы схемы 415:
            // "667f1d2f-0d4f-43c4-9d56-b5d3da29c4ac" — удален
            var ticket = Client.GetTicket("a8528183-b4d9-4e1c-b1ed-c2d6dab97504");
            Assert.NotNull(ticket);
            Assert.NotNull(ticket.Result);
            Assert.AreEqual("415", ticket.Result.Operation);
            Assert.AreEqual("Успешное завершение операции", ticket.Result.Operation_Comment);
        }

        private Documents CreateDocument701(Documents doc601)
        {
            var mo = doc601.Move_Order_Notification;
            var doc = new Documents
            {
                Version = doc601.Version,
                Session_Ui = doc601.Session_Ui,
                Accept = new Accept
                {
                    // Отправитель и получатель меняются местами
                    Subject_Id = mo.Receiver_Id,
                    Counterparty_Id = mo.Subject_Id,

                    // Дата и время текущие
                    Operation_Date = DateTime.Now,
                    Order_Details = new AcceptOrder_Details(),
                }
            };

            // Список подтверждаемой продукции:
            // Можно ли подтвердить не все, что в документе 601?
            // Ответ: можно подтвердить частично, только некоторые SGTIN/SSCC.
            // Но каждую коробку можно подтверждать только целиком:
            // нельзя подтвердить приемку части препаратов в коробке.
            var od = doc.Accept.Order_Details;
            foreach (var unit in mo.Order_Details)
            {
                // подтверждаем прием упаковки с несколькими ЛП
                if (unit.Sscc_Detail != null && unit.Sscc_Detail.Sscc != null)
                {
                    // Номер транспортной упаковки
                    od.Sscc.Add(unit.Sscc_Detail.Sscc);
                    continue;
                }

                // подтверждаем прием экземпляра ЛП
                if (unit.Sgtin != null)
                {
                    // Номер SGTIN указывается номер из ранее загруженной 415 схемы
                    od.Sgtin.Add(unit.Sgtin);
                    continue;
                }
            }

            // загружен через API, обработан, код d72a2afc-fddd-43e3-b308-a8c3eece70a4
            return doc;
        }

        [Test]
        public void FindIncomingDocument601()
        {
            // Документы структуры 601 создает сама песочница, мы такой документ загрузить не можем.
            // Чтобы получить документ 601, мы (отправитель) загружаем документ 415,
            // а в ответ песочница посылает нашему контрагенту (получателю) документ 601.
            // Получателем выступает второй тестовый участник.
            // Заходим в песочницу от имени второго участника.
            using (var client = CreateSecondClient())
            {
                // находим уведомление в списке входящих документов
                var docs = client.GetIncomeDocuments(new DocFilter
                {
                    DocType = 601,
                    SenderID = "00000000104494",
                    ProcessedDateFrom = new DateTime(2020, 05, 18, 19, 10, 00),
                    ProcessedDateTo = new DateTime(2020, 05, 18, 19, 18, 00),
                }, 0, 1);

                // оно там будет одно в указанный период
                Assert.AreEqual(1, docs.Total);
                Assert.AreEqual(1, docs.Documents.Length);

                // прежние документы структуры 601:
                // "6faca9fc-5390-406f-b935-03ee4705e4ac" — удален
                var doc = docs.Documents[0];
                Assert.AreEqual("ba494f1d-09e1-4b91-88e5-b37cbbb1be78", doc.DocumentID);

                // скачиваем уведомление по коду
                var doc601 = client.GetDocument(doc.DocumentID).Move_Order_Notification;
                Assert.NotNull(doc601);
                Assert.AreEqual("00000000104494", doc601.Subject_Id);
                Assert.AreEqual("00000000104453", doc601.Receiver_Id);

                // содержимое должно соответствовать отосланному документу схемы 415
                var details = doc601.Order_Details;
                Assert.AreEqual(2, details.Count);

                // тут у нас зарегистрированный КИЗ, который был в документе схемы 311,
                // введен в оборот документом схемы 313, но не упаковам документом схемы 915
                // и отправлен в наш адрес документом схемы 415
                Assert.AreEqual("50754041398745" + "123456790A123", details[0].Sgtin);

                // а тут у нас код третичной упаковки тот же, что был в документе схемы 915
                // это ящик, в котором упакованы 4 КИЗа документом 915
                // этот ящик тоже отправлен в наш адрес документом схемы 415
                Assert.AreEqual("507540413987451236", details[1].Sscc_Detail.Sscc);
            }
        }

        [Test, Explicit("Can't upload the same document more than once")]
        public void SendDocument701ToSandbox()
        {
            // прежние документы структуры 601:
            // "6faca9fc-5390-406f-b935-03ee4705e4ac" — удален
            // скачиваем уведомление 601 по коду
            using (var client = CreateSecondClient())
            {
                var doc601 = client.GetDocument("ba494f1d-09e1-4b91-88e5-b37cbbb1be78");
                Assert.NotNull(doc601.Move_Order_Notification);

                // формируем ответ на него: мол, подтверждаем получение всех ЛП в полном объеме
                var doc701 = CreateDocument701(doc601);
                var docId = client.SendDocument(doc701);
                WriteLine("Uploaded document #701: {0}", docId);
            }
        }

        [Test]
        public void GetDocument701AndTicket701FromSandbox()
        {
            using (var client = CreateSecondClient())
            {
                // прежние документы схемы 701:
                // d72a2afc-fddd-43e3-b308-a8c3eece70a4 — удален
                var document = client.GetDocument("602d156b-514c-46d4-9bc5-e87515f51c16");
                Assert.NotNull(document);
                Assert.NotNull(document.Accept);
                Assert.NotNull(document.Accept.Order_Details);
                Assert.AreEqual(1, document.Accept.Order_Details.Sgtin.Count);
                Assert.AreEqual(1, document.Accept.Order_Details.Sscc.Count);
                Assert.AreEqual("00000000104494", document.Accept.Counterparty_Id);

                var ticket = client.GetTicket("602d156b-514c-46d4-9bc5-e87515f51c16");
                Assert.NotNull(ticket);
                Assert.NotNull(ticket.Result);
                Assert.AreEqual("701", ticket.Result.Operation);
                Assert.AreEqual("Успешное завершение операции", ticket.Result.Operation_Comment);
            }
        }

        [Test]
        public void GetDocument607FromSandbox()
        {
            // находим уведомление в списке входящих документов
            var docs = Client.GetIncomeDocuments(new DocFilter
            {
                DocType = 607,
                SenderID = "00000000104453",
                ProcessedDateFrom = new DateTime(2020, 05, 18, 19, 37, 00),
                ProcessedDateTo = new DateTime(2020, 05, 18, 19, 38, 00),
            }, 0, 1);

            // оно там будет одно в указанный период
            Assert.AreEqual(1, docs.Total);
            Assert.AreEqual(1, docs.Documents.Length);
            var docId = docs.Documents[0].DocumentID;

            // код документа стал известен после первого запуска теста
            Assert.AreEqual("590b3155-ea61-49e4-90a4-19bdda30f3da", docId);
            var text = Client.GetDocumentText(docId);
            WriteLine(text);

            // получим документ подтверждения
            var doc = Client.GetDocument(docId);
            Assert.NotNull(doc.Accept_Notification);
            var details = doc.Accept_Notification.Order_Details;
            Assert.NotNull(details);

            // в нем будут: один КИЗ и упаковка из четырех КИЗ
            Assert.AreEqual(1, details.Sgtin.Count);
            Assert.AreEqual("50754041398745" + "123456790A123", details.Sgtin[0]);
            Assert.AreEqual(1, details.Sscc.Count);
            Assert.AreEqual("507540413987451236", details.Sscc[0]);
        }

        private Documents CreateDocument210(string senderId, string sgtin = null, string ssccUp = null, string ssccDown = null)
        {
            // Схема 210 устарела и будет вскоре удалена
            // создаем запрос содержимого упаковки
            // в этом документе надо указывать одно из трех: либо SGTIN, либо SSCC up, либо SSCC down
            var doc = new Documents();
            doc.Query_Kiz_Info = new Query_Kiz_Info
            {
                Subject_Id = senderId,
                Sgtin = sgtin,
                Sscc_Down = ssccDown,
                Sscc_Up = ssccUp,
            };

            return doc;
        }

        [Test, Ignore("I've run this test once to check whether the sandbox accepts the malformed XML-documents")]
        public void SandboxAccepdsMalformedXmlDocuments()
        {
            //var documentId = Client.SendDocument("Привет! Хочу проверить, принимаются ли некорректные XML-документы.");
            //Assert.NotNull(documentId);

            // got documentId: 56835c65-96c5-4917-9867-a0fa953a6b66
            var ticket = Client.GetTicket("56835c65-96c5-4917-9867-a0fa953a6b66");
        }

        [Test, Explicit("Schema210 is obsolete and will be replaced with schema 220")]
        public void SendDocument210WithSgtinAndSsccToSandbox()
        {
            // Документ 210 возвращает информацию о содержимом короба, либо о КИЗ
            // из личного кабинета тестового участника-Типографии
            // берем код места деятельности, расположенного по адресу:
            // край Забайкальский р-н Могойтуйский пгт Могойтуй ул Банзарова
            // отсюда делалась отправка ЛП
            var senderId = "00000000104494";

            // Код препарата. GTIN – указывается из реестра ЛП тестового участника
            // из личного кабинета тестового участника-Типографии берем ЛП: Найзин
            var gtin = "50754041398745";

            // SGTIN = GTIN + S/N
            var sgtin = gtin + "1234567906123";

            // Идентификатор SSCC из документа 915 (он же был в документах 415 и 601)
            var sscc = "507540413987451236";

            // Пошлем документ, в данном случае получили код:
            // 89219f2a-f1db-4d2d-bcfb-05274c2188cd
            // 734a0898-0c10-487e-af6b-cf7fb3ef050f
            // f1bdc175-3740-4a4e-b7dd-bbb61c140d4c
            var doc210 = CreateDocument210(senderId, sgtin, sscc, sscc);
            var docId = Client.SendDocument(doc210);
            WriteLine("Sent document 210: {0}", docId);
        }

        [Test, Explicit("Schema210 is obsolete and will be replaced with schema 220")]
        public void SendDocument210WithSsccDownToSandbox()
        {
            // Документ 210 возвращает информацию о содержимом короба, либо о КИЗ
            // из личного кабинета тестового участника-Типографии
            // берем код места деятельности, расположенного по адресу:
            // край Забайкальский р-н Могойтуйский пгт Могойтуй ул Банзарова
            // отсюда делалась отправка ЛП
            var senderId = "00000000104494";

            // Идентификатор SSCC из документа 915 (он же был в документах 415 и 601)
            var sscc = "507540413987451236";

            // Пошлем документ, в данном случае получили код:
            var doc210 = CreateDocument210(senderId, ssccDown: sscc);
            var docId = Client.SendDocument(doc210);
            WriteLine("Sent document 210: {0}", docId);
        }

        [Test]
        public void GetDocument210FromSandbox()
        {
            // Код отправленного документа схемы 210:
            // "734a0898-0c10-487e-af6b-cf7fb3ef050f");
            // "f1bdc175 -3740-4a4e-b7dd-bbb61c140d4c");
            // "f44d0b72-7259-499c-859d-a50b4c6232e4"
            // "7db0d364-6577-4699-aa22-a6dbe7f3184c"
            var doc = Client.GetDocumentMetadata("7db0d364-6577-4699-aa22-a6dbe7f3184c");
            WriteLine(doc.DocStatus);

            // ответ на схему 210 — схема 211, получаем ее как квитанцию к документу
            var ticket = Client.GetTicket(doc.DocumentID);
            Assert.NotNull(ticket.Kiz_Info);

            // если был запрос SGTIN, то было бы заполнено следующее:
            //Assert.NotNull(ticket.Kiz_Info.Sgtin);
            //Assert.NotNull(ticket.Kiz_Info.Sgtin.Info_Sgtin);
            //Assert.AreEqual("50754041398745", ticket.Kiz_Info.Sgtin.Info_Sgtin.Gtin);

            // запрос по коду третичной упаковки
            Assert.NotNull(ticket.Kiz_Info.Sscc_Down);
            Assert.IsTrue(ticket.Kiz_Info.Sscc_Down.Any());
            Assert.NotNull(ticket.Kiz_Info.Sscc_Down[0]);
            Assert.NotNull(ticket.Kiz_Info.Sscc_Down[0].Sgtin);
            Assert.IsTrue(ticket.Kiz_Info.Sscc_Down.Any(s => s.Sgtin.Info_Sgtin.Sgtin == "507540413987451234567906123"));
        }

        [Test]
        public void UploadedDocumentIsImmediatelyAvailableForDownload()
        {
            var doc = CreateDocument210(senderId: "00000000104494", sgtin: "50754041398745" + "1234567906123");
            var docId = Client.SendDocument(doc);
            WriteLine("Uploaded document: {0}", docId);

            // may throw 404 NotFound?
            var md = Client.GetDocumentMetadata(docId);
            Assert.NotNull(md);
        }

        [Test]
        public void Sandbox_Issue_SimilarToTest_TestServer_IssueSR00497874()
        {
            // 1. получаем список входящих документов
            var docs = Client.GetIncomeDocuments(new DocFilter
            {
                DocType = 601,
                DocStatus = DocStatusEnum.PROCESSED_DOCUMENT,
                ProcessedDateFrom = new DateTime(2020, 01, 08), // new DateTime(2019, 11, 01),
                ProcessedDateTo = new DateTime(2020, 01, 12), // new DateTime(2019, 12, 01)
            }, 0, 1);
            Assert.NotNull(docs);
            Assert.NotNull(docs.Documents);
            Assert.AreEqual(1, docs.Documents.Length);

            // 2. скачиваем первый документ из списка, получаем ошибку
            var docId = docs.Documents[0].DocumentID;
            Assert.IsFalse(string.IsNullOrWhiteSpace(docId));
            var doc = Client.GetDocumentText(docId);
            Assert.NotNull(doc);
        }

        [Test, Explicit]
        public void GetIncomeMoveOrderNotifications()
        {
            Client.Tracer = null;
            var docs = Client.GetIncomeDocuments(new DocFilter
            {
                DocType = 601,
                DocStatus = DocStatusEnum.PROCESSED_DOCUMENT,
                ProcessedDateFrom = DateTime.Today.AddDays(-200),
            }, 0, 400);

            foreach (var d in docs.Documents)
            {
                var xml = Client.GetDocumentText(d.DocumentID);
                var md = XmlSerializationHelper.Deserialize(xml);
                Assert.NotNull(md.Move_Order_Notification);
                if (md.Move_Order_Notification.Order_Details.IsNullOrEmpty())
                {
                    continue;
                }

                var od = md.Move_Order_Notification.Order_Details;
                if (od.Where(o => o.Sscc_Detail != null).Any() && od.Where(o => o.Sgtin != null).Any())
                {
                    WriteLine("==== Move order with SGTINs and SSCCs =======");
                    WriteLine(xml);
                }
            }
        }

        private Documents CreateDocument220(string senderId, string sscc)
        {
            // создаем запрос содержимого упаковки
            var doc = new Documents();
            doc.Query_Hierarchy_Info = new Query_Hierarchy_Info
            {
                Subject_Id = senderId,
                Sscc = sscc,
            };

            return doc;
        }

        private bool TryGetWorkflowSeedFromSuccessfulPacking(MdlpClient client, out string senderId, out string gtin)
        {
            try
            {
                var documents = client.GetOutcomeDocuments(new DocFilter
                {
                    DocType = 10311,
                    DocStatus = DocStatusEnum.PROCESSED_DOCUMENT,
                    ProcessedDateFrom = DateTime.Now.AddYears(-10),
                    ProcessedDateTo = DateTime.Now,
                },
                0,
                10);

                foreach (var metadata in documents?.Documents ?? Array.Empty<OutcomeDocument>())
                {
                    if (string.IsNullOrWhiteSpace(metadata?.DocumentID))
                    {
                        continue;
                    }

                    Documents document;
                    try
                    {
                        document = client.GetDocument(metadata.DocumentID);
                    }
                    catch (MdlpException ex) when (
                        ex.StatusCode == HttpStatusCode.NotFound ||
                        ex.StatusCode == HttpStatusCode.Forbidden ||
                        ex.StatusCode == HttpStatusCode.BadRequest)
                    {
                        continue;
                    }

                    var packing = document?.Skzkm_Register_End_Packing;
                    if (!IsFixedLengthDigits(packing?.Subject_Id, 14) || !IsFixedLengthDigits(packing?.Gtin, 14))
                    {
                        continue;
                    }

                    senderId = packing.Subject_Id;
                    gtin = packing.Gtin;
                    return true;
                }
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            senderId = null;
            gtin = null;
            return false;
        }

        private IEnumerable<string> GetWorkflowSenderCandidates(MdlpClient client)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var productionBranchIds = Array.Empty<string>();
            var branchIds = Array.Empty<string>();

            try
            {
                var branches = client.GetBranches(null, 0, 100);
                productionBranchIds = branches?.Entries
                    ?.Where(entry => IsFixedLengthDigits(entry?.ID, 14) && entry.WorkList != null && entry.WorkList.Any(item =>
                        item?.IndexOf("Производ", StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(entry => entry.ID)
                    .ToArray() ?? Array.Empty<string>();

                branchIds = branches?.Entries
                    ?.Select(entry => entry?.ID)
                    .Where(id => IsFixedLengthDigits(id, 14))
                    .ToArray() ?? Array.Empty<string>();
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            foreach (var branchId in productionBranchIds)
            {
                if (yielded.Add(branchId))
                {
                    yield return branchId;
                }
            }

            foreach (var branchId in branchIds)
            {
                if (yielded.Add(branchId))
                {
                    yield return branchId;
                }
            }

            if (yielded.Add(KnownWorkflowSenderId))
            {
                yield return KnownWorkflowSenderId;
            }
        }

        private IEnumerable<string> GetWorkflowGtinCandidates(MdlpClient client)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gtins = Array.Empty<string>();

            try
            {
                var medProducts = client.GetCurrentMedProducts(null, 0, 100);
                gtins = medProducts?.Entries
                    ?.Select(entry => entry?.Gtin)
                    .Where(value => IsFixedLengthDigits(value, 14))
                    .ToArray() ?? Array.Empty<string>();
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            foreach (var gtin in gtins)
            {
                if (yielded.Add(gtin))
                {
                    yield return gtin;
                }
            }

            if (yielded.Add(KnownWorkflowGtin))
            {
                yield return KnownWorkflowGtin;
            }
        }

        private IEnumerable<WorkflowSeedCandidate> GetWorkflowSeedCandidates(MdlpClient client)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (TryGetWorkflowSeedFromSuccessfulPacking(client, out var historicalSenderId, out var historicalGtin) &&
                yielded.Add(historicalSenderId + "|" + historicalGtin))
            {
                yield return new WorkflowSeedCandidate(historicalSenderId, historicalGtin, "successful 10311");
            }

            if (yielded.Add(KnownWorkflowSenderId + "|" + KnownWorkflowGtin))
            {
                yield return new WorkflowSeedCandidate(KnownWorkflowSenderId, KnownWorkflowGtin, "known workflow fallback");
            }

            var senderIds = GetWorkflowSenderCandidates(client).Take(3).ToArray();
            var gtins = GetWorkflowGtinCandidates(client).Take(4).ToArray();
            foreach (var senderId in senderIds)
            {
                foreach (var gtin in gtins)
                {
                    if (yielded.Add(senderId + "|" + gtin))
                    {
                        yield return new WorkflowSeedCandidate(senderId, gtin, "live registry");
                    }
                }
            }
        }

        private bool TryGetWorkflowDeviceIdCandidate(MdlpClient client, out string deviceId, out string source)
        {
            try
            {
                var documents = client.GetOutcomeDocuments(new DocFilter
                {
                    DocType = 10311,
                    DocStatus = DocStatusEnum.PROCESSED_DOCUMENT,
                    ProcessedDateFrom = DateTime.Now.AddYears(-10),
                    ProcessedDateTo = DateTime.Now,
                },
                0,
                10);

                deviceId = documents?.Documents
                    ?.Select(metadata => metadata?.DeviceID)
                    .FirstOrDefault(IsValidWorkflowDeviceId);
                if (IsValidWorkflowDeviceId(deviceId))
                {
                    source = "successful 10311";
                    return true;
                }
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            try
            {
                var devices = client.GetEmissionDevices(new EmissionDeviceFilter
                {
                    ProvisionStartDate = DateTime.Now.AddYears(-100),
                    ProvisionEndDate = DateTime.Now,
                    Status = 0,
                }, 0, 10);

                deviceId = devices?.Entries
                    ?.Select(entry => entry?.DeviceID)
                    .FirstOrDefault(IsValidWorkflowDeviceId);
                if (IsValidWorkflowDeviceId(deviceId))
                {
                    source = "emission registry";
                    return true;
                }
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            deviceId = null;
            source = null;
            return false;
        }

        private static bool IsValidWorkflowDeviceId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length == 16;
        }

        private static string[] BuildWorkflowSgtins(string gtin, string runTag, int count)
        {
            var digits = new string(runTag.Where(char.IsDigit).ToArray());
            var suffixWidth = Math.Max(1, count.ToString(CultureInfo.InvariantCulture).Length);
            var serialSeedWidth = 13 - suffixWidth;
            var serialSeed = digits.Length >= serialSeedWidth
                ? digits.Substring(digits.Length - serialSeedWidth)
                : digits.PadLeft(serialSeedWidth, '0');
            var sgtins = new string[count];
            for (var i = 0; i < count; i++)
            {
                var serialSuffix = i.ToString(CultureInfo.InvariantCulture).PadLeft(suffixWidth, '0');
                var serial = serialSeed + serialSuffix;
                sgtins[i] = gtin + serial;
            }

            return sgtins;
        }

        private static string BuildWorkflowSscc(string gtin, string runTag)
        {
            var digits = new string(runTag.Where(char.IsDigit).ToArray());
            var suffix = digits.Length >= 3
                ? digits.Substring(digits.Length - 3)
                : digits.PadLeft(3, '0');
            var body = gtin + suffix;
            return body + ComputeGs1CheckDigit(body).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildWorkflowDeviceId(string preferredDeviceId, string runTag)
        {
            if (IsValidWorkflowDeviceId(preferredDeviceId))
            {
                return preferredDeviceId;
            }

            var digits = new string(runTag.Where(char.IsDigit).ToArray());
            if (digits.Length >= 16)
            {
                return digits.Substring(digits.Length - 16);
            }

            return digits.PadLeft(16, '0');
        }

        private static int ComputeGs1CheckDigit(string digitsWithoutCheckDigit)
        {
            var sum = 0;
            var weight = 3;
            for (var i = digitsWithoutCheckDigit.Length - 1; i >= 0; i--)
            {
                sum += (digitsWithoutCheckDigit[i] - '0') * weight;
                weight = weight == 3 ? 1 : 3;
            }

            return (10 - (sum % 10)) % 10;
        }

        private static Documents CreateWorkflowDocument311(string sessionUi, string senderId, string gtin, IEnumerable<string> sgtins, string runTag, string deviceId)
        {
            var doc = new Documents
            {
                Version = "1.38",
                Session_Ui = sessionUi,
                Skzkm_Register_End_Packing = new Skzkm_Register_End_Packing
                {
                    Subject_Id = senderId,
                    Operation_Date = DateTime.Now,
                    Order_Type = Order_Type_Enum.Item1,
                    Series_Number = ("WF" + runTag).Substring(0, Math.Min(20, ("WF" + runTag).Length)),
                    Expiration_Date = DateTime.Today.AddYears(1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Gtin = gtin,
                    Device_Info = new Skzkm_Info_Type
                    {
                        Device_Id = BuildWorkflowDeviceId(deviceId, runTag),
                        Skzkm_Origin_Msg_Id = "wf311-" + runTag,
                    },
                }
            };

            foreach (var sgtin in sgtins)
            {
                doc.Skzkm_Register_End_Packing.Signs.Add(sgtin);
            }

            return doc;
        }

        private static Documents CreateWorkflowDocument313(string sessionUi, string senderId, IEnumerable<string> sgtins, string runTag)
        {
            var doc = new Documents
            {
                Version = "1.34",
                Session_Ui = sessionUi,
                Register_Product_Emission = new Register_Product_Emission
                {
                    Subject_Id = senderId,
                    Operation_Date = DateTime.Now,
                    Release_Info = new Release_Info_Type
                    {
                        Doc_Num = "WF313-" + runTag.Substring(runTag.Length - 6),
                        Doc_Date = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                        Confirmation_Num = "WF313-" + runTag.Substring(runTag.Length - 6),
                    },
                    Signs = new Register_Product_EmissionSigns(),
                }
            };

            foreach (var sgtin in sgtins)
            {
                doc.Register_Product_Emission.Signs.Sgtin.Add(sgtin);
            }

            return doc;
        }

        private static Documents CreateWorkflowDocument915(string sessionUi, string senderId, string sscc, IEnumerable<string> sgtins)
        {
            var doc = new Documents
            {
                Version = "1.34",
                Session_Ui = sessionUi,
                Multi_Pack = new Multi_Pack
                {
                    Subject_Id = senderId,
                    Operation_Date = DateTime.Now,
                }
            };

            var pack = new Multi_PackBy_SgtinDetail
            {
                Sscc = sscc,
            };

            foreach (var sgtin in sgtins)
            {
                pack.Content.Add(sgtin);
            }

            doc.Multi_Pack.By_Sgtin.Add(pack);
            return doc;
        }

        private static SentDocumentReference SendTrackedDocument(MdlpClient client, Documents document)
        {
            var xml = XmlSerializationHelper.Serialize(document, client.ApplicationName);
            var requestId = Guid.NewGuid().ToString();
            var signature = (string)ComputeSignatureMethod.Invoke(client, new object[] { xml });
            var result = client.Post<SendDocumentResult>("documents/send", new
            {
                document = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)),
                sign = signature,
                request_id = requestId,
            }, apiMethodName: "SendDocument");

            return new SentDocumentReference(result.DocumentId, requestId);
        }

        private static bool IsFinalStatus(string status)
        {
            return status == DocStatusEnum.PROCESSED_DOCUMENT ||
                status == DocStatusEnum.FAILED_RESULT_READY ||
                status == DocStatusEnum.FAILED;
        }

        private static DocumentMetadata TryGetMetadataByRequestId(MdlpClient client, string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return null;
            }

            try
            {
                return client.GetDocumentsByRequestID(requestId)?.Documents?.FirstOrDefault();
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
                return null;
            }
        }

        private static DocumentMetadata TryGetMetadataFromOutcomeList(MdlpClient client, string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                return null;
            }

            try
            {
                return client.GetOutcomeDocuments(new DocFilter
                {
                    DocumentID = documentId,
                }, 0, 5)?.Documents?.FirstOrDefault(doc => string.Equals(doc.DocumentID, documentId, StringComparison.OrdinalIgnoreCase));
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
                return null;
            }
        }

        private static DocumentMetadata TryGetTrackedDocumentMetadata(MdlpClient client, SentDocumentReference document, out string source)
        {
            try
            {
                source = "document_id";
                return client.GetDocumentMetadata(document.DocumentId);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }

            var byRequestId = TryGetMetadataByRequestId(client, document.RequestId);
            if (byRequestId != null)
            {
                source = "request_id";
                return byRequestId;
            }

            var byOutcomeList = TryGetMetadataFromOutcomeList(client, document.DocumentId);
            if (byOutcomeList != null)
            {
                source = "outcome list";
                return byOutcomeList;
            }

            source = null;
            return null;
        }

        private static DocumentMetadata WaitForFinalStatus(MdlpClient client, SentDocumentReference document, TimeSpan timeout, Action<string, object[]> tracer)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var doc = TryGetTrackedDocumentMetadata(client, document, out var source);
                if (doc != null)
                {
                    if (string.Equals(source, "document_id", StringComparison.Ordinal))
                    {
                        tracer("Waiting... {0}", new object[] { doc.DocStatus });
                    }
                    else
                    {
                        tracer("Waiting... {0} via {1}", new object[] { doc.DocStatus, source });
                    }

                    if (IsFinalStatus(doc.DocStatus))
                    {
                        return doc;
                    }
                }
                else
                {
                    tracer(
                        "Waiting... metadata not visible yet for {0} (request_id={1})",
                        new object[] { document.DocumentId, document.RequestId });
                }

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            return null;
        }

        private static string FormatTicketSummary(Documents ticket)
        {
            var result = ticket?.Result;
            if (result == null)
            {
                return "ticket result is empty";
            }

            var parts = new List<string>
            {
                string.Format(CultureInfo.InvariantCulture, "operation={0}", result.Operation),
                string.Format(CultureInfo.InvariantCulture, "result={0}", result.Operation_Result),
            };

            if (!string.IsNullOrWhiteSpace(result.Operation_Comment))
            {
                parts.Add("comment=" + result.Operation_Comment);
            }

            if (result.ErrorsSpecified)
            {
                var errors = result.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error?.Object_Id)
                        ? string.Format(CultureInfo.InvariantCulture, "{0}: {1}", error?.Error_Code, error?.Error_Desc)
                        : string.Format(CultureInfo.InvariantCulture, "{0}: {1} (object_id={2})", error.Error_Code, error.Error_Desc, error.Object_Id))
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                parts.Add("errors=" + string.Join("; ", errors));
            }

            if (result.Operation_WarningsSpecified)
            {
                var warnings = result.Operation_Warnings
                    .Select(warning => warning?.Operation_Warning)
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                parts.Add("warnings=" + string.Join("; ", warnings));
            }

            return string.Join(", ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private string TryGetTicketSummary(MdlpClient client, string documentId, string requestId)
        {
            if (!string.IsNullOrWhiteSpace(documentId))
            {
                try
                {
                    var directTicket = client.GetTicket(documentId);
                    var directSummary = FormatTicketSummary(directTicket);
                    if (!string.IsNullOrWhiteSpace(directSummary))
                    {
                        return directSummary + " (source=document_id)";
                    }
                }
                catch (MdlpException ex) when (
                    ex.StatusCode == HttpStatusCode.NotFound ||
                    ex.StatusCode == HttpStatusCode.Forbidden ||
                    ex.StatusCode == HttpStatusCode.BadRequest)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                try
                {
                    var requestTicket = client.GetFirstTicketByRequestId(requestId);
                    var requestSummary = FormatTicketSummary(requestTicket);
                    if (!string.IsNullOrWhiteSpace(requestSummary))
                    {
                        return requestSummary + " (source=request_id)";
                    }
                }
                catch (MdlpException ex) when (
                    ex.StatusCode == HttpStatusCode.NotFound ||
                    ex.StatusCode == HttpStatusCode.Forbidden ||
                    ex.StatusCode == HttpStatusCode.BadRequest)
                {
                }
            }

            return null;
        }

        private DocumentMetadata SendDocument220ToSandbox(MdlpClient client, string senderId, string sscc, out string docId)
        {
            var doc220 = CreateDocument220(senderId, sscc);
            var sentDocument = SendTrackedDocument(client, doc220);
            docId = sentDocument.DocumentId;
            WriteLine("Sent document 220: {0}", docId);
            return WaitForFinalStatus(client, sentDocument, TimeSpan.FromMinutes(3), WriteLine);
        }

        private bool TryEnsureProcessedDocument(MdlpClient client, SentDocumentReference document, string operationName, TimeSpan timeout, out DocumentMetadata metadata)
        {
            metadata = WaitForFinalStatus(client, document, timeout, WriteLine);
            if (metadata == null)
            {
                NoteHierarchy221(string.Format(
                    CultureInfo.InvariantCulture,
                    "Timed out waiting for {0} to finish. doc_id={1}, request_id={2}",
                    operationName,
                    document.DocumentId,
                    document.RequestId));
                return false;
            }

            if (metadata.DocStatus == DocStatusEnum.PROCESSED_DOCUMENT)
            {
                return true;
            }

            NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "{0} finished with non-success status {1}. doc_id={2}", operationName, metadata.DocStatus, document.DocumentId));
            var summary = TryGetTicketSummary(client, document.DocumentId, document.RequestId);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "{0} ticket: {1}", operationName, summary));
            }

            return false;
        }

        private bool TrySend220AndGetHierarchyTicket(MdlpClient client, SsccQueryCandidate candidate, out string queryDocumentId, out Documents ticket)
        {
            try
            {
                var metadata = SendDocument220ToSandbox(client, candidate.SenderId, candidate.Sscc, out var docId);
                if (metadata == null)
                {
                    NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Timed out waiting for document 220 to finish. sscc={0}", candidate.Sscc));
                    queryDocumentId = null;
                    ticket = null;
                    return false;
                }

                if (metadata.DocStatus != DocStatusEnum.PROCESSED_DOCUMENT)
                {
                    NoteHierarchy221(string.Format(
                        CultureInfo.InvariantCulture,
                        "Document 220 finished with non-success status {0}. sscc={1}, doc_id={2}",
                        metadata.DocStatus,
                        candidate.Sscc,
                        docId));
                    queryDocumentId = null;
                    ticket = null;
                    return false;
                }

                var currentTicket = client.GetTicket(docId);
                var package = currentTicket?.Hierarchy_Info?.Sscc_Down;
                if (package?.Sscc_Info?.Sscc == null)
                {
                    NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Ticket 221 did not contain Sscc_Down hierarchy. sscc={0}", candidate.Sscc));
                    queryDocumentId = null;
                    ticket = null;
                    return false;
                }

                if (!string.Equals(package.Sscc_Info.Sscc, candidate.Sscc, StringComparison.OrdinalIgnoreCase))
                {
                    NoteHierarchy221(string.Format(
                        CultureInfo.InvariantCulture,
                        "Ticket 221 returned a different SSCC. requested={0}, actual={1}",
                        candidate.Sscc,
                        package.Sscc_Info.Sscc));
                    queryDocumentId = null;
                    ticket = null;
                    return false;
                }

                queryDocumentId = docId;
                ticket = currentTicket;
                return true;
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest ||
                ex.StatusCode == HttpStatusCode.Conflict)
            {
                NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Live SSCC candidate rejected by sandbox. sscc={0}, error={1}", candidate.Sscc, ex.Message));
                queryDocumentId = null;
                ticket = null;
                return false;
            }
        }

        private bool TryGetHierarchyTicketFromFreshWorkflow(MdlpClient client, out SsccQueryCandidate successfulCandidate, out string queryDocumentId, out Documents ticket)
        {
            var preferredDeviceId = TryGetWorkflowDeviceIdCandidate(client, out var discoveredDeviceId, out var deviceSource)
                ? discoveredDeviceId
                : null;
            NoteHierarchy221(IsValidWorkflowDeviceId(preferredDeviceId)
                ? string.Format(CultureInfo.InvariantCulture, "Fresh workflow will use device_id={0} from {1}.", preferredDeviceId, deviceSource)
                : "Fresh workflow could not discover a real device_id; using synthetic 16-character fallback.");

            foreach (var seed in GetWorkflowSeedCandidates(client).Take(3))
            {
                if (string.IsNullOrWhiteSpace(seed.SenderId) || string.IsNullOrWhiteSpace(seed.Gtin))
                {
                    continue;
                }

                var sessionUi = Guid.NewGuid().ToString("D");
                var runTag = DateTime.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
                var sgtins = BuildWorkflowSgtins(seed.Gtin, runTag, 4);
                var sscc = BuildWorkflowSscc(seed.Gtin, runTag);
                NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Fresh workflow seed: sender_id={0}, gtin={1}, sscc={2}, source={3}", seed.SenderId, seed.Gtin, sscc, seed.Source));

                try
                {
                    var doc311 = SendTrackedDocument(client, CreateWorkflowDocument311(sessionUi, seed.SenderId, seed.Gtin, sgtins, runTag, preferredDeviceId));
                    NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Sent document 311: {0}, request_id={1}", doc311.DocumentId, doc311.RequestId));
                    if (!TryEnsureProcessedDocument(client, doc311, "Document 311", TimeSpan.FromMinutes(5), out var doc311Metadata))
                    {
                        if (doc311Metadata?.DocStatus == DocStatusEnum.FAILED)
                        {
                            NoteHierarchy221("Fresh document 311 was definitively rejected by sandbox; skipping remaining fresh seed attempts.");
                            break;
                        }

                        continue;
                    }

                    var doc313 = SendTrackedDocument(client, CreateWorkflowDocument313(sessionUi, seed.SenderId, sgtins, runTag));
                    NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Sent document 313: {0}, request_id={1}", doc313.DocumentId, doc313.RequestId));
                    if (!TryEnsureProcessedDocument(client, doc313, "Document 313", TimeSpan.FromMinutes(5), out _))
                    {
                        continue;
                    }

                    var doc915 = SendTrackedDocument(client, CreateWorkflowDocument915(sessionUi, seed.SenderId, sscc, sgtins));
                    NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Sent document 915: {0}, request_id={1}", doc915.DocumentId, doc915.RequestId));
                    if (!TryEnsureProcessedDocument(client, doc915, "Document 915", TimeSpan.FromMinutes(5), out _))
                    {
                        continue;
                    }

                    var candidate = new SsccQueryCandidate(seed.SenderId, sscc, 915, doc915.DocumentId);
                    NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Trying fresh SSCC candidate {0} created by document {1}", candidate.Sscc, candidate.SourceDocumentId));
                    if (TrySend220AndGetHierarchyTicket(client, candidate, out queryDocumentId, out ticket))
                    {
                        successfulCandidate = candidate;
                        return true;
                    }
                }
                catch (MdlpException ex)
                {
                    NoteHierarchy221(string.Format(CultureInfo.InvariantCulture, "Fresh 311->313->915 workflow rejected by sandbox. sender_id={0}, gtin={1}, error={2}", seed.SenderId, seed.Gtin, ex.Message));
                }
            }

            successfulCandidate = null;
            queryDocumentId = null;
            ticket = null;
            return false;
        }

        private IEnumerable<SsccQueryCandidate> GetDynamicSsccQueryCandidates(MdlpClient client)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in GetSsccQueryCandidatesFromHierarchy(client, yielded))
            {
                yield return candidate;
            }

            foreach (var candidate in GetSsccQueryCandidatesFromOutcomeDocuments(client, 915, yielded))
            {
                yield return candidate;
            }

            foreach (var candidate in GetSsccQueryCandidatesFromOutcomeDocuments(client, 415, yielded))
            {
                yield return candidate;
            }
        }

        private IEnumerable<SsccQueryCandidate> GetSsccQueryCandidatesFromHierarchy(MdlpClient client, HashSet<string> yielded)
        {
            foreach (var sscc in GetPotentialSsccs(client))
            {
                SsccHierarchyResponse<SsccInfo> hierarchy;
                try
                {
                    hierarchy = client.GetSsccHierarchy(sscc);
                }
                catch (MdlpException ex) when (IsSandboxStaticResourceNotFound(ex))
                {
                    NoteHierarchy221("SSCC hierarchy endpoint is unavailable in sandbox; falling back to document-backed discovery.");
                    yield break;
                }
                catch (MdlpException ex) when (
                    ex.StatusCode == HttpStatusCode.NotFound ||
                    ex.StatusCode == HttpStatusCode.Forbidden ||
                    ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    continue;
                }

                if (hierarchy == null || hierarchy.ErrorCode != null)
                {
                    continue;
                }

                var requestedPackage = hierarchy.Up?.FirstOrDefault() ?? hierarchy.Down?.FirstOrDefault();
                if (requestedPackage == null ||
                    string.IsNullOrWhiteSpace(requestedPackage.Sscc) ||
                    string.IsNullOrWhiteSpace(requestedPackage.SystemSubjectID))
                {
                    continue;
                }

                var key = requestedPackage.SystemSubjectID + "|" + requestedPackage.Sscc;
                if (!yielded.Add(key))
                {
                    continue;
                }

                yield return new SsccQueryCandidate(
                    requestedPackage.SystemSubjectID,
                    requestedPackage.Sscc,
                    220,
                    "reestr/sscc/hierarchy");
            }
        }

        private IEnumerable<string> GetPotentialSsccs(MdlpClient client)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sscc in KnownSsccCandidates)
            {
                if (!string.IsNullOrWhiteSpace(sscc) && yielded.Add(sscc))
                {
                    yield return sscc;
                }
            }

            var endDate = DateTime.Now;
            for (var i = 0; i < 12; i++)
            {
                var startDate = endDate.AddDays(-365);
                EntriesResponse<SgtinExtended> sgtins;
                try
                {
                    sgtins = client.GetSgtins(new SgtinFilter
                    {
                        EmissionDateFrom = startDate,
                        EmissionDateTo = endDate,
                    },
                    startFrom: 0,
                    count: 120);
                }
                catch (MdlpException ex) when (IsSandboxStaticResourceNotFound(ex))
                {
                    yield break;
                }
                catch (MdlpException ex) when (
                    ex.StatusCode == HttpStatusCode.NotFound ||
                    ex.StatusCode == HttpStatusCode.Forbidden ||
                    ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    yield break;
                }

                if (sgtins?.Entries == null)
                {
                    yield break;
                }

                foreach (var entry in sgtins.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry?.Sscc) || !yielded.Add(entry.Sscc))
                    {
                        continue;
                    }

                    yield return entry.Sscc;
                }

                endDate = startDate;
            }
        }

        private IEnumerable<SsccQueryCandidate> GetSsccQueryCandidatesFromOutcomeDocuments(MdlpClient client, int docType, HashSet<string> yielded)
        {
            DocumentsResponse<OutcomeDocument> documents;
            try
            {
                documents = client.GetOutcomeDocuments(new DocFilter
                {
                    DocType = docType,
                    DocStatus = DocStatusEnum.PROCESSED_DOCUMENT,
                    ProcessedDateFrom = DateTime.Now.AddYears(-10),
                    ProcessedDateTo = DateTime.Now,
                },
                0,
                20);
            }
            catch (MdlpException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.BadRequest)
            {
                yield break;
            }

            if (documents?.Documents == null)
            {
                yield break;
            }

            foreach (var metadata in documents.Documents)
            {
                if (string.IsNullOrWhiteSpace(metadata?.DocumentID))
                {
                    continue;
                }

                Documents document;
                try
                {
                    document = client.GetDocument(metadata.DocumentID);
                }
                catch (MdlpException ex) when (
                    ex.StatusCode == HttpStatusCode.NotFound ||
                    ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    continue;
                }

                IEnumerable<SsccQueryCandidate> extracted = docType == 915
                    ? ExtractSsccCandidatesFrom915(document, metadata.DocumentID)
                    : ExtractSsccCandidatesFrom415(document, metadata.DocumentID);

                foreach (var candidate in extracted)
                {
                    var key = candidate.SenderId + "|" + candidate.Sscc;
                    if (!yielded.Add(key))
                    {
                        continue;
                    }

                    yield return candidate;
                }
            }
        }

        private static IEnumerable<SsccQueryCandidate> ExtractSsccCandidatesFrom915(Documents document, string documentId)
        {
            var senderId = document?.Multi_Pack?.Subject_Id;
            if (string.IsNullOrWhiteSpace(senderId) || document.Multi_Pack?.By_Sgtin == null)
            {
                yield break;
            }

            foreach (var pack in document.Multi_Pack.By_Sgtin)
            {
                if (string.IsNullOrWhiteSpace(pack?.Sscc))
                {
                    continue;
                }

                yield return new SsccQueryCandidate(senderId, pack.Sscc, 915, documentId);
            }
        }

        private static IEnumerable<SsccQueryCandidate> ExtractSsccCandidatesFrom415(Documents document, string documentId)
        {
            var senderId = document?.Move_Order?.Subject_Id;
            if (string.IsNullOrWhiteSpace(senderId) || document.Move_Order?.Order_Details == null)
            {
                yield break;
            }

            foreach (var item in document.Move_Order.Order_Details)
            {
                var sscc = item?.Sscc_Detail?.Sscc;
                if (string.IsNullOrWhiteSpace(sscc))
                {
                    continue;
                }

                yield return new SsccQueryCandidate(senderId, sscc, 415, documentId);
            }
        }

        private static bool IsSandboxStaticResourceNotFound(MdlpException ex)
        {
            return ex.StatusCode == HttpStatusCode.NotFound &&
                ex.Message.IndexOf("No static resource", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool TryGetHierarchyTicketForLiveSscc(MdlpClient client, out SsccQueryCandidate successfulCandidate, out string queryDocumentId, out Documents ticket)
        {
            foreach (var candidate in GetDynamicSsccQueryCandidates(client).Take(3))
            {
                WriteLine(
                    "Trying live SSCC candidate {0} from document {1} (type {2}) and sender {3}",
                    candidate.Sscc,
                    candidate.SourceDocumentId,
                    candidate.SourceDocType,
                    candidate.SenderId);

                if (TrySend220AndGetHierarchyTicket(client, candidate, out queryDocumentId, out ticket))
                {
                    successfulCandidate = candidate;
                    return true;
                }
            }

            successfulCandidate = null;
            queryDocumentId = null;
            ticket = null;
            return false;
        }

        [Test]
        public void GetDocument221FromSandbox()
        {
            using var client = CreateHierarchyQueryClient();
            _hierarchy221Diagnostics.Clear();

            if (!TryGetHierarchyTicketFromFreshWorkflow(client, out var candidate, out var docId, out var ticket) &&
                !TryGetHierarchyTicketForLiveSscc(client, out candidate, out docId, out ticket))
            {
                var details = _hierarchy221Diagnostics.Count == 0
                    ? string.Empty
                    : ": " + string.Join(" | ", _hierarchy221Diagnostics.TakeLast(8));
                Assert.Ignore("Could not obtain 221 hierarchy response from fresh workflow or live sandbox sources" + details);
            }

            var hierarchy = ticket.Hierarchy_Info;
            Assert.NotNull(hierarchy);

            // Для запроса 220 песочница возвращает интересующий короб в разделе Sscc_Down.
            var package = hierarchy.Sscc_Down;
            Assert.NotNull(package);
            Assert.NotNull(package.Sscc_Info);
            Assert.AreEqual(candidate.Sscc, package.Sscc_Info.Sscc);

            Assert.NotNull(package.Sscc_Info.Childs);
            Assert.IsTrue(package.Sscc_Info.Childs.Count >= 1);
            Assert.IsTrue(
                package.Sscc_Info.Childs.Any(child =>
                    (child.Sscc_Info != null && child.Sscc_Info.Count > 0) ||
                    (child.Sgtin_Info != null && child.Sgtin_Info.Count > 0)),
                "Hierarchy response does not contain nested SSCC or SGTIN entries.");

            WriteLine(
                "Validated dynamic 221 ticket. source_doc={0}, query_doc={1}, sscc={2}",
                candidate.SourceDocumentId,
                docId,
                candidate.Sscc);
        }

        [Test]
        public void Chapter8_04_4_Sandbox_GetSsccFullHierarchyForMultipleSsccss()
        {
            var l = Client.GetSsccFullHierarchy(new[] { "000000000105900000", "147600887000110010" });
            Assert.NotNull(l);
            Assert.AreEqual(2, l.Length);

            var h = l[0];
            Assert.NotNull(h.Up);
            Assert.NotNull(h.Down);

            // validate up hierarchy
            Assert.AreEqual("000000000105900000", h.Up.Sscc);
            Assert.IsNotNull(h.Up.ChildSsccs);
            Assert.IsNotNull(h.Up.ChildSgtins);
            Assert.AreEqual(0, h.Up.ChildSgtins.Length);
            Assert.AreEqual(0, h.Up.ChildSsccs.Length);

            // validate down hierarchy
            Assert.AreEqual("000000000105900000", h.Down.Sscc);
            Assert.IsNotNull(h.Down.ChildSsccs);
            Assert.IsNotNull(h.Down.ChildSgtins);
            Assert.AreEqual(19, h.Down.ChildSgtins.Length);
            Assert.AreEqual(0, h.Down.ChildSsccs.Length);
            Assert.AreEqual("189011481011940000000001041", h.Down.ChildSgtins[0].Sgtin);
            Assert.AreEqual("189011481011940000000001042", h.Down.ChildSgtins[1].Sgtin);

            h = l[1];
            Assert.NotNull(h.Up);
            Assert.NotNull(h.Down);

            // validate up hierarchy
            Assert.AreEqual("147600887000120010", h.Up.Sscc);
            Assert.IsNotNull(h.Up.ChildSsccs);
            Assert.IsNotNull(h.Up.ChildSgtins);
            Assert.AreEqual(0, h.Up.ChildSgtins.Length);
            Assert.AreEqual(1, h.Up.ChildSsccs.Length);
            Assert.AreEqual("147600887000110010", h.Up.ChildSsccs[0].Sscc);
            Assert.IsNotNull(h.Up.ChildSsccs[0].ChildSsccs);
            Assert.IsNotNull(h.Up.ChildSsccs[0].ChildSgtins);
            Assert.AreEqual(0, h.Up.ChildSsccs[0].ChildSsccs.Length);
            Assert.AreEqual(0, h.Up.ChildSsccs[0].ChildSgtins.Length);

            // validate down hierarchy
            Assert.AreEqual("147600887000110010", h.Down.Sscc);
            Assert.IsNotNull(h.Down.ChildSsccs);
            Assert.IsNotNull(h.Down.ChildSgtins);
            Assert.AreEqual(1, h.Down.ChildSgtins.Length);
            Assert.AreEqual(0, h.Down.ChildSsccs.Length);
            Assert.AreEqual("046200115828050000000000153", h.Down.ChildSgtins[0].Sgtin);
        }

        [Test]
        public void Chapter8_13_1_Sandbox_GetBatchShortDistribution()
        {
            // public SGTIN information has BatchNumber
            // "046200115828050000000000153"
            var sgtin = Client.GetPublicSgtins("50754041398745" + "1234567906123");
            Assert.NotNull(sgtin.Entries);
            Assert.AreEqual(1, sgtin.Entries.Length);
            Assert.NotNull(sgtin.Entries[0]);
            var batchNumber = sgtin.Entries[0].BatchNumber;
            var gtin = sgtin.Entries[0].Sgtin.Substring(0, 14);

            // 18901148101194
            // 04620011582805
            var batches = Client.GetBatchShortDistribution(gtin, batchNumber);
            Assert.NotNull(batches);
            Assert.AreEqual(0, batches.Total);
            Assert.IsNull(batches.Entries);
        }

        // sessionUi — просто Guid, объединяющий документы в смысловую группу
        private string SandboxSessionUI = "D1FDD60B-1F09-4171-AB39-D1737584B943";

        private Documents CreateDocument311(string gtin)
        {
            // Создаем документ схемы 311 от имени организации Типография для типографий
            var doc = new Documents
            {
                Session_Ui = SandboxSessionUI,

                // Регистрация окончания упаковки = схема 10311
                Skzkm_Register_End_Packing = new Skzkm_Register_End_Packing
                {
                    // из личного кабинета тестового участника-Типографии
                    // берем код места деятельности, расположенного по адресу:
                    // край Забайкальский р-н Могойтуйский пгт Могойтуй ул Банзарова
                    Subject_Id = "00000000104494",

                    // в этом месте мы сегодня заканчиваем упаковку препаратов
                    Operation_Date = DateTime.Now,

                    Series_Number = "1234567890",
                    Expiration_Date = "03.07.2025",
                    Gtin = gtin,
                }
            };

            // Перечень идентификационных кодов потребительских упаковок.
            // Идентификаторы SGTIN. – формируются путем добавления к GTIN 
            // 13-значного серийного номера. Для каждой отгрузки 
            // необходимо генерировать уникальный серийный номер
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "0000000000101");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "0000000000102");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "0000000000103");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "0000000000104");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "0000000000105");
            doc.Skzkm_Register_End_Packing.Signs.Add(gtin + "0000000000106");
            return doc;
        }

        private Documents CreateDocument313(string gtin)
        {
            // Создаем документ схемы 313 от имени организации Типография для типографий
            var doc = new Documents
            {
                Session_Ui = SandboxSessionUI,

                // Регистрация сведений о выпуске готовой продукции = схема 313
                Register_Product_Emission = new Register_Product_Emission
                {
                    // из личного кабинета тестового участника-Типографии
                    // берем код места деятельности, расположенного по адресу:
                    // край Забайкальский р-н Могойтуйский пгт Могойтуй ул Банзарова
                    Subject_Id = "00000000104494",

                    // в этом месте мы сегодня заканчиваем упаковку препаратов
                    Operation_Date = DateTime.Now,

                    // реквизиты сведений о вводе в оборот
                    Release_Info = new Release_Info_Type
                    {
                        Confirmation_Num = "249",
                        Doc_Num = "249",
                        Doc_Date = DateTime.Today.ToString(@"dd\.MM\.yyyy"),
                    },
                }
            };

            // Перечень идентификационных кодов потребительских упаковок.
            // Идентификаторы SGTIN. – формируются путем добавления к GTIN 
            // 13-значного серийного номера. Для каждой отгрузки 
            // необходимо генерировать уникальный серийный номер
            doc.Register_Product_Emission.Signs = new Register_Product_EmissionSigns();
            doc.Register_Product_Emission.Signs.Sgtin.Add(gtin + "0000000000101");
            doc.Register_Product_Emission.Signs.Sgtin.Add(gtin + "0000000000102");
            doc.Register_Product_Emission.Signs.Sgtin.Add(gtin + "0000000000103");
            doc.Register_Product_Emission.Signs.Sgtin.Add(gtin + "0000000000104");
            doc.Register_Product_Emission.Signs.Sgtin.Add(gtin + "0000000000105");
            doc.Register_Product_Emission.Signs.Sgtin.Add(gtin + "0000000000106");
            return doc;
        }

        private Documents CreateDocument415(string receiverId, params string[] sgtins) // Documents doc311)
        {
            // Создаем документ схемы 415 от имени организации Типография для типографий
            var doc = new Documents
            {
                // Если не указать версию, загрузка документа не срабатывает:
                // пишет, что тип документа не определен
                Version = "1.34",
                Session_Ui = SandboxSessionUI,

                // Перемещение на склад получателя = схема 415
                Move_Order = new Move_Order
                {
                    // из личного кабинета тестового участника-Типографии
                    // берем код места деятельности, расположенного по адресу:
                    // край Забайкальский р-н Могойтуйский пгт Могойтуй ул Банзарова
                    // здесь у нас пока хранятся упакованные ЛП, ждущие отправки
                    Subject_Id = "00000000104494",

                    // сюда мы будем отправлять упакованные ЛП
                    Receiver_Id = receiverId,

                    // сегодня отправляем препараты
                    Operation_Date = DateTime.Now,

                    // Реквизиты документа отгрузки: номер и дата документа
                    Doc_Num = "249",
                    Doc_Date = DateTime.Today.ToString(@"dd\.MM\.yyyy"),

                    // Тип операции отгрузки со склада: 1 - продажа, 2 - возврат
                    Turnover_Type = Turnover_Type_Enum.Item1,

                    // Источник финансирования: 1 - собственные средства, 2-3 - бюджет
                    Source = Source_Type.Item1,

                    // Тип договора: 1 - купля-продажа
                    Contract_Type = Contract_Type_Enum.Item1,

                    // Реестровый номер контракта (договора)
                    // в Единой информационной системе в сфере закупок
                    // нам в данном случае не требуется
                    Contract_Num = null,
                },
            };

            // Список отгружаемой продукции
            var order = doc.Move_Order.Order_Details;
            foreach (var sgtin in sgtins) // doc311.Register_End_Packing.Signs)
            {
                order.Add(new Move_OrderOrder_DetailsUnion
                {
                    Sgtin = sgtin,
                    Cost = 1000, // цена единицы продукции
                    Vat_Value = 100, // сумма НДС
                });
            }

            // итого в документе перемещения схемы 415 у нас только список SGTIN
            return doc;
        }

        private void CreateTestSgtinsUploadDocumentAndCheckResults()
        {
            // создаем sgtin-ы
            var doc3x = CreateDocument311("55413760478406");
            var doc3xId = Client.SendDocument(doc3x);
            WriteLine($"Uploaded document: {doc3xId}");

            // ждем, пока документ будет обработан
            var doc3xmd = Client.GetDocumentMetadata(doc3xId);
            while (doc3xmd.DocStatus != DocStatusEnum.PROCESSED_DOCUMENT &&
                doc3xmd.DocStatus != DocStatusEnum.FAILED_RESULT_READY)
            {
                doc3xmd = Client.GetDocumentMetadata(doc3xId);
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            // получаем текст документа
            var docXml = Client.GetDocumentText(doc3xId);
            WriteLine(docXml);

            // получаем квитанцию и смотрим, что там за ответ
            var docTicket = Client.GetTicketText(doc3xId);
            var docTicketXml = XDocument.Parse(docTicket);
            WriteLine(docTicketXml.ToString());
        }

        [Test, Explicit]
        public void CreateTestMoveOrderNotification()
        {
            var receiver = "00000000120755"; // Крыленко

            // отправляем КАЛИЯ ХЛОРИД+МАГНИЯ ХЛОРИД+НАТРИЯ АЦЕТАТ+НАТРИЯ ГЛЮКОНАТ+НАТРИЯ ХЛОРИД
            var doc = CreateDocument415(receiver,
                "55413760478406uI22xO2hloD7Y",
                "55413760478406u8WACOLE9UVKA",
                "55413760478406tkWZ4VXFnHwk9",
                "55413760478406t45BLRLAMA3Cv",
                "55413760478406stsKoDEUCsD9G");

            // загрузился документ: 653d57ad-a486-40f1-8b27-520f02f33ad2
            var docId = "653d57ad-a486-40f1-8b27-520f02f33ad2"; // Client.SendDocument(doc);
            WriteLine($"Uploaded document: {docId}");

            var xml = Client.GetDocumentText(docId);
            WriteLine(xml);
        }
    }
}
