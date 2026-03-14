namespace MdlpApiClient.Tests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using MdlpApiClient.DataContracts;
    using MdlpApiClient.Xsd;
    using NUnit.Framework;

    [TestFixture]
    public class TwoCounterpartyWorkflowTests : UnitTestsBase
    {
        private const string SenderSubjectId = "00000000104494";
        private const string ReceiverSubjectId = "00000000104453";
        private const string WorkflowGtin = "50754041398745";

        [Test, Explicit("WIP workflow: runs live sandbox flow 311 -> 313 -> 415 for two counterparties")]
        public void EmitThenShipWorkflow_TwoCounterparties()
        {
            RequireSandboxAvailabilityOrIgnore();

            var sessionUi = Guid.NewGuid().ToString("D");
            var runTag = DateTime.UtcNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);
            var sgtins = BuildSgtins(WorkflowGtin, runTag, 2);

            using var sender = CreateSenderClient();
            using var receiver = CreateReceiverClient();

            var doc311 = CreateDocument311(sessionUi, WorkflowGtin, sgtins);
            var doc311Id = sender.SendDocument(doc311);
            var md311 = WaitForFinalStatus(sender, doc311Id, TimeSpan.FromMinutes(5));
            Assert.AreEqual(DocStatusEnum.PROCESSED_DOCUMENT, md311.DocStatus, "Document 311 must be processed.");

            var doc313 = CreateDocument313(sessionUi, sgtins);
            var doc313Id = sender.SendDocument(doc313);
            var md313 = WaitForFinalStatus(sender, doc313Id, TimeSpan.FromMinutes(5));
            Assert.AreEqual(DocStatusEnum.PROCESSED_DOCUMENT, md313.DocStatus, "Document 313 must be processed.");

            var doc415 = CreateDocument415(sessionUi, ReceiverSubjectId, sgtins);
            var doc415Id = sender.SendDocument(doc415);
            var md415 = WaitForFinalStatus(sender, doc415Id, TimeSpan.FromMinutes(5));
            Assert.AreEqual(DocStatusEnum.PROCESSED_DOCUMENT, md415.DocStatus, "Document 415 must be processed.");

            var incoming = receiver.GetIncomeDocuments(new DocFilter
            {
                DocType = 601,
                SenderID = SenderSubjectId,
                ProcessedDateFrom = DateTime.Now.AddHours(-2),
            },
            startFrom: 0,
            count: 50);

            Assert.NotNull(incoming);
            Assert.NotNull(incoming.Documents);
            Assert.IsTrue(
                incoming.Documents.Any(d =>
                    string.Equals(d.RequestID, md415.RequestID, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d.DocumentID, doc415Id, StringComparison.OrdinalIgnoreCase)),
                "Receiver did not get a linked 601 notification for the sent 415 document.");
        }

        private static string[] BuildSgtins(string gtin, string runTag, int count)
        {
            var sgtins = new string[count];
            for (var i = 0; i < count; i++)
            {
                var serial = (runTag + i.ToString(CultureInfo.InvariantCulture)).PadRight(13, '0').Substring(0, 13);
                sgtins[i] = gtin + serial;
            }

            return sgtins;
        }

        private MdlpClient CreateSenderClient()
        {
            return new MdlpClient(new ResidentCredentials
            {
                ClientID = ClientID1,
                ClientSecret = ClientSecret1,
                UserID = SandboxUserThumbprint1,
            },
            TestApiBaseUrl)
            {
                ApplicationName = "Workflow-Sender",
                Tracer = WriteLine,
            };
        }

        private MdlpClient CreateReceiverClient()
        {
            return new MdlpClient(new ResidentCredentials
            {
                ClientID = ClientID2,
                ClientSecret = ClientSecret2,
                UserID = SandboxUserThumbprint2,
            },
            TestApiBaseUrl)
            {
                ApplicationName = "Workflow-Receiver",
                Tracer = WriteLine,
            };
        }

        private static DocumentMetadata WaitForFinalStatus(MdlpClient client, string documentId, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var metadata = client.GetDocumentMetadata(documentId);
                if (metadata.DocStatus == DocStatusEnum.PROCESSED_DOCUMENT ||
                    metadata.DocStatus == DocStatusEnum.FAILED_RESULT_READY ||
                    metadata.DocStatus == DocStatusEnum.FAILED)
                {
                    return metadata;
                }

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            Assert.Fail("Timed out waiting for final document status. document_id={0}", documentId);
            return null;
        }

        private static Documents CreateDocument311(string sessionUi, string gtin, string[] sgtins)
        {
            var doc = new Documents
            {
                Version = "1.34",
                Session_Ui = sessionUi,
                Skzkm_Register_End_Packing = new Skzkm_Register_End_Packing
                {
                    Subject_Id = SenderSubjectId,
                    Operation_Date = DateTime.Now,
                    Order_Type = Order_Type_Enum.Item1,
                    Series_Number = "WF" + DateTime.UtcNow.ToString("yyMMdd", CultureInfo.InvariantCulture),
                    Expiration_Date = DateTime.Today.AddYears(1).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Gtin = gtin,
                }
            };

            foreach (var sgtin in sgtins)
            {
                doc.Skzkm_Register_End_Packing.Signs.Add(sgtin);
            }

            return doc;
        }

        private static Documents CreateDocument313(string sessionUi, string[] sgtins)
        {
            var doc = new Documents
            {
                Version = "1.34",
                Session_Ui = sessionUi,
                Register_Product_Emission = new Register_Product_Emission
                {
                    Subject_Id = SenderSubjectId,
                    Operation_Date = DateTime.Now,
                    Release_Info = new Release_Info_Type
                    {
                        Doc_Num = "WF313",
                        Doc_Date = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                        Confirmation_Num = "WF313",
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

        private static Documents CreateDocument415(string sessionUi, string receiverId, string[] sgtins)
        {
            var doc = new Documents
            {
                Version = "1.34",
                Session_Ui = sessionUi,
                Move_Order = new Move_Order
                {
                    Subject_Id = SenderSubjectId,
                    Receiver_Id = receiverId,
                    Operation_Date = DateTime.Now,
                    Doc_Num = "WF415",
                    Doc_Date = DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Turnover_Type = Turnover_Type_Enum.Item1,
                    Source = Source_Type.Item1,
                    Contract_Type = Contract_Type_Enum.Item1,
                }
            };

            foreach (var sgtin in sgtins)
            {
                doc.Move_Order.Order_Details.Add(new Move_OrderOrder_DetailsUnion
                {
                    Sgtin = sgtin,
                    Cost = 1000,
                    Vat_Value = 100,
                });
            }

            return doc;
        }
    }
}
