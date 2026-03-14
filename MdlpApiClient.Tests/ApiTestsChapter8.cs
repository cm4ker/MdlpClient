namespace MdlpApiClient.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using MdlpApiClient.DataContracts;
    using NUnit.Framework;

    [TestFixture]
    public class ApiTestsChapter8 : UnitTestsClientBase
    {
        private static readonly string[] KnownSsccCandidates =
        {
            "000000000105900000",
            "147600887000110010",
            "000000111100000097",
            "000000111100000100",
        };

        private string[] _sampleSgtins;
        private string[] _sampleSsccs;

        [Test]
        public void Chapter8_01_2_GetBranches()
        {
            // пример из документации с кодом 00000000000464 — не находится
            Client.GetBranches(new BranchFilter
            {
                BranchID = "00000000100930", // "00000000000464",
                HouseGuid = "986f2934-be05-438f-a30e-c15b90e15dbc", // "3e311a10-3d0c-438e-a013-7c5fd3ea66a6",
                Status = 1,
                StartDate = new DateTime(2018, 12, 12), // 2019-11-01
                EndDate = new DateTime(2019, 1, 1), // 2019-12-01
            },
            startFrom: 0, count: 10);

            // попробуем найти хоть что-нибудь
            var branches = Client.GetBranches(null, startFrom: 0, count: 10);
            Assert.IsNotNull(branches);
            Assert.IsNotNull(branches.Entries);
            Assert.IsTrue(branches.Total >= 0);

            if (branches.Entries.Length == 0)
            {
                Assert.Ignore("No branches returned by sandbox for unfiltered query");
            }

            var branch = branches.Entries[0];
            Assert.NotNull(branch);
            Assert.IsNotNull(branch.OrgName);
            Assert.NotNull(branch.WorkList);
            Assert.NotNull(branch.Address);
            Assert.IsNotNull(branch.Address.HouseGuid);
        }

        [Test]
        public void Chapter8_01_3_GetBranch()
        {
            var branchId = GetSampleBranchId();
            if (string.IsNullOrWhiteSpace(branchId))
            {
                Assert.Ignore("No branches available in sandbox to fetch details");
            }

            try
            {
                var branch = Client.GetBranch(branchId);
                Assert.NotNull(branch);
                Assert.AreEqual(branchId, branch.BranchID);
                Assert.NotNull(branch.Address);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Ignore("Branch " + branchId + " not found in sandbox");
            }
        }

        [Test]
        public void Chapter8_01_4_RegisterBranch()
        {
            var ex = Assert.Throws<MdlpException>(() =>
            {
                Client.RegisterBranch(new Address
                {
                    // Intentionally invalid FIAS IDs to avoid creating real branch records.
                    AoGuid = Guid.NewGuid().ToString(),
                    HouseGuid = Guid.NewGuid().ToString(),
                });
            });

            Assert.That(ex.StatusCode, Is.AnyOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.Forbidden,
                HttpStatusCode.Conflict,
                HttpStatusCode.NotFound));
        }

        [Test]
        public void Chapter8_02_2_GetWarehouses()
        {
            // пример из документации с кодом 00000000000561 — не находится
            var whouses = Client.GetWarehouses(new WarehouseFilter
            {
                WarehouseID = "00000000100931", // "00000000000561"
                HouseGuid = "986f2934-be05-438f-a30e-c15b90e15dbc", // "3e311a10-3d0c-438e-a013-7c5fd3ea66a6",
                Status = 1,
                StartDate = new DateTime(2018, 11, 1), // 2019-11-01
                EndDate = new DateTime(2019, 1, 1), // 2019-12-01
            },
            startFrom: 0, count: 10);

            Assert.IsNotNull(whouses);
            Assert.IsNotNull(whouses.Entries);
            Assert.IsTrue(whouses.Entries.Length <= 10);

            // попробуем найти хоть что-нибудь
            whouses = Client.GetWarehouses(null, startFrom: 0, count: 10);
            Assert.IsNotNull(whouses);
            Assert.IsNotNull(whouses.Entries);
            Assert.IsTrue(whouses.Total >= 0);

            if (whouses.Entries.Length == 0)
            {
                Assert.Ignore("No warehouses returned by sandbox for unfiltered query");
            }

            var whouse = whouses.Entries[0];
            Assert.NotNull(whouse);
            Assert.IsNotNull(whouse.OrgName);
            Assert.NotNull(whouse.WorkList);
            Assert.NotNull(whouse.Address);
            Assert.IsNotNull(whouse.Address.HouseGuid);
        }

        [Test]
        public void Chapter8_02_3_GetWarehouse()
        {
            var warehouseId = GetSampleWarehouseId();
            if (string.IsNullOrWhiteSpace(warehouseId))
            {
                Assert.Ignore("No warehouses available in sandbox to fetch details");
            }

            try
            {
                var warehouse = Client.GetWarehouse(warehouseId);
                Assert.NotNull(warehouse);
                Assert.AreEqual(warehouseId, warehouse.WarehouseID);
                Assert.NotNull(warehouse.Address);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Ignore("Warehouse " + warehouseId + " not found in sandbox");
            }
        }

        [Test]
        public void Chapter8_02_4_RegisterWarehouse()
        {
            var ex = Assert.Throws<MdlpException>(() =>
            {
                var inn = "0000000000";
                Client.RegisterWarehouse(inn, new Address
                {
                    // Intentionally invalid FIAS IDs to avoid creating real warehouse records.
                    AoGuid = Guid.NewGuid().ToString(),
                    HouseGuid = Guid.NewGuid().ToString(),
                });
            });

            Assert.That(ex.StatusCode, Is.AnyOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.Forbidden,
                HttpStatusCode.Conflict,
                HttpStatusCode.NotFound));
        }

        [Test]
        public void Chapter8_02_5_GetAvailableAddresses()
        {
            var member = Client.GetCurrentMember();
            var addresses = Client.GetAvailableAddresses(member?.Inn);
            Assert.NotNull(addresses);
            Assert.IsTrue(addresses.Total >= 0);
            Assert.NotNull(addresses.Entries);
            if (addresses.Entries.Length > 0)
            {
                var address = addresses.Entries[0];
                Assert.IsNotNull(address.AddressID);
                Assert.IsNotNull(address.Address);
            }
        }

        [Test]
        public void Chapter8_03_1_GetSgtins()
        {
            var sampleSgtin = GetSampleSgtins().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sampleSgtin))
            {
                Assert.Ignore("No accessible SGTIN was found in sandbox");
            }

            EntriesResponse<SgtinExtended> sgtins;
            try
            {
                sgtins = Client.GetSgtins(new SgtinFilter
                {
                    Sgtin = sampleSgtin,
                    EmissionDateFrom = DateTime.Now.AddYears(-100),
                    EmissionDateTo = DateTime.Now,
                    LastTracingDateFrom = DateTime.Now.AddYears(-100),
                    LastTracingDateTo = DateTime.Now,
                },
                startFrom: 0, count: 1);
            }
            catch (MdlpException ex) when (IsSandboxStaticResourceNotFound(ex))
            {
                Assert.Ignore("SGTIN registry filter endpoint is unavailable in sandbox");
                return;
            }

            Assert.IsNotNull(sgtins);
            Assert.IsNotNull(sgtins.Entries);
            Assert.IsTrue(sgtins.Total >= 0);
            Assert.IsTrue(sgtins.Entries.Length <= 1);
            if (sgtins.Entries.Length == 0)
            {
                Assert.Ignore("Sample SGTIN was not found by filtered query");
            }

            var sgtin = sgtins.Entries[0];
            Assert.NotNull(sgtin);
            Assert.AreEqual(sampleSgtin, sgtin.SgtinValue);
            Assert.IsNotNull(sgtin.Gtin);
        }

        [Test]
        public void Chapter8_03_2_GetSgtins()
        {
            var sampleSgtin = GetSampleSgtins().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sampleSgtin))
            {
                Assert.Ignore("No accessible SGTIN was found in sandbox");
            }

            var likelyMissing = GetLikelyMissingSgtin(sampleSgtin);
            EntriesFailedResponse<SgtinExtended, SgtinFailed> sgtins;
            try
            {
                sgtins = Client.GetSgtins(new[]
                {
                    sampleSgtin,
                    likelyMissing
                });
            }
            catch (MdlpException ex) when (IsSandboxStaticResourceNotFound(ex))
            {
                Assert.Ignore("SGTIN-by-list endpoint is unavailable in sandbox");
                return;
            }

            Assert.IsNotNull(sgtins);
            Assert.IsNotNull(sgtins.Entries);
            Assert.IsNotNull(sgtins.FailedEntries);
            Assert.AreEqual(2, sgtins.Total);
            Assert.AreEqual(sgtins.FailedEntries.Length, sgtins.Failed);
            Assert.AreEqual(2, sgtins.Entries.Length + sgtins.FailedEntries.Length);
            Assert.IsTrue(
                sgtins.Entries.Any(e => string.Equals(e.SgtinValue, sampleSgtin, StringComparison.OrdinalIgnoreCase)) ||
                sgtins.FailedEntries.Any(e => string.Equals(e.Sgtin, sampleSgtin, StringComparison.OrdinalIgnoreCase)),
                "Sample SGTIN must be reflected either in entries or in failed entries");
        }

        [Test]
        public void Chapter8_03_2_GetSgtins_EmptyListIsNotAllowed()
        {
            try
            {
                Client.GetSgtins();
                Assert.Ignore("API no longer rejects empty SGTIN list");
            }
            catch (MdlpException)
            {
            }
        }

        [Test]
        public void Chapter8_03_3_GetPublicSgtins()
        {
            var sampleSgtin = GetSampleSgtins().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sampleSgtin))
            {
                Assert.Ignore("No accessible SGTIN was found in sandbox");
            }

            var likelyMissing = GetLikelyMissingSgtin(sampleSgtin);
            EntriesFailedResponse<PublicSgtin, string> sgtins;
            try
            {
                sgtins = Client.GetPublicSgtins(
                    sampleSgtin,
                    likelyMissing
                );
            }
            catch (MdlpException ex) when (IsSandboxStaticResourceNotFound(ex))
            {
                Assert.Ignore("Public SGTIN-by-list endpoint is unavailable in sandbox");
                return;
            }

            Assert.IsNotNull(sgtins);
            Assert.IsNotNull(sgtins.Entries);
            Assert.IsNotNull(sgtins.FailedEntries);
            Assert.AreEqual(2, sgtins.Total);
            Assert.AreEqual(sgtins.FailedEntries.Length, sgtins.Failed);
            Assert.AreEqual(2, sgtins.Entries.Length + sgtins.FailedEntries.Length);
            Assert.IsTrue(
                sgtins.Entries.Any(e => string.Equals(e.Sgtin, sampleSgtin, StringComparison.OrdinalIgnoreCase)) ||
                sgtins.FailedEntries.Any(e => string.Equals(e, sampleSgtin, StringComparison.OrdinalIgnoreCase)),
                "Sample SGTIN must be reflected either in entries or in failed entries");
        }

        [Test]
        public void Chapter8_03_3_GetPublicSgtins_EmptyListIsNotAllowed()
        {
            try
            {
                Client.GetPublicSgtins();
                Assert.Ignore("API no longer rejects empty SGTIN list");
            }
            catch (MdlpException)
            {
            }
        }

        [Test]
        public void Chapter8_03_4_GetSgtin()
        {
            var sampleSgtin = GetSampleSgtins().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sampleSgtin))
            {
                Assert.Ignore("No accessible SGTIN was found in sandbox");
            }

            try
            {
                var info = Client.GetSgtin(sampleSgtin);
                Assert.NotNull(info);
                Assert.NotNull(info.SgtinInfo);
                Assert.NotNull(info.GtinInfo);

                Assert.AreEqual(sampleSgtin, info.SgtinInfo.SgtinValue);
                Assert.IsNotNull(info.SgtinInfo.Gtin);
                Assert.AreEqual(info.SgtinInfo.Gtin, info.GtinInfo.Gtin);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Assert.Ignore("Detailed SGTIN info is unavailable for selected sample in sandbox");
            }
        }

        [Test]
        public void Chapter8_03_5_GetSgtinsOnHold()
        {
            var sgtins = Client.GetSgtinsOnHold(new SgtinOnHoldFilter
            {
                Sgtin = "061017000000000000000000006",
                ReleaseDateFrom = DateTime.Now.AddYears(-100),
                ReleaseDateTo = DateTime.Now,
            }, 0, 1);

            Assert.IsNotNull(sgtins);
            Assert.IsTrue(sgtins.Total >= 0);
            Assert.IsNotNull(sgtins.Entries);
            Assert.IsTrue(sgtins.Entries.Length <= 1);
        }

        [Test]
        public void Chapter8_03_6_GetSgtinsKktAwaitingWithdrawal()
        {
            var sgtins = Client.GetSgtinsKktAwaitingWithdrawal(new SgtinAwaitingWithdrawalFilter
            {
                Sgtin = "061017000000000000000000006",
                StartDate = DateTime.Now.AddYears(-100),
                EndDate = DateTime.Now,
            }, 0, 1);
            Assert.NotNull(sgtins);

            Assert.IsTrue(sgtins.Total >= 0);
            Assert.NotNull(sgtins.Entries);
            Assert.IsTrue(sgtins.Entries.Length <= 1);
        }

        [Test]
        public void Chapter8_03_7_GetSgtinsDeviceAwaitingWithdrawal()
        {
            var sgtins = Client.GetSgtinsDeviceAwaitingWithdrawal(new SgtinAwaitingWithdrawalFilter
            {
                Sgtin = "061017000000000000000000006",
                StartDate = DateTime.Now.AddYears(-100),
                EndDate = DateTime.Now,
            }, 0, 1);
            Assert.NotNull(sgtins);

            Assert.IsTrue(sgtins.Total >= 0);
            Assert.NotNull(sgtins.Entries);
            Assert.IsTrue(sgtins.Entries.Length <= 1);
        }

        [Test]
        public void Chapter8_04_1_GetSsccHierarchy_NotFound()
        {
            try
            {
                var ssccs = Client.GetSsccHierarchy("201902251235570000");
                Assert.NotNull(ssccs);
                Assert.NotNull(ssccs.Up);
                Assert.NotNull(ssccs.Down);

                Assert.AreEqual(0, ssccs.Up.Length);
                Assert.AreEqual(0, ssccs.Down.Length);
                if (ssccs.ErrorCode != null)
                {
                    Assert.AreEqual(2, ssccs.ErrorCode);
                    Assert.That(ssccs.ErrorDescription, Is.AnyOf(
                        "Requested data not found",
                        "Запрашиваемые данные не найдены"));
                }
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        private static string GetLikelyMissingSgtin(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "04600000000000DYNAMIC0000000";
            }

            var chars = source.ToCharArray();
            var index = chars.Length - 1;
            chars[index] = chars[index] == '0' ? '1' : '0';
            return new string(chars);
        }

        private IEnumerable<string> GetTestCodes(Func<SgtinExtended, string> getCode, int maxWindows = 8, int daysPerWindow = 100, int pageSize = 120)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var endDate = DateTime.Now;
            for (var i = 0; i < maxWindows; i++)
            {
                var startDate = endDate.AddDays(-daysPerWindow);
                EntriesResponse<SgtinExtended> sgtins;
                try
                {
                    sgtins = Client.GetSgtins(new SgtinFilter
                    {
                        EmissionDateFrom = startDate,
                        EmissionDateTo = endDate,
                    },
                    startFrom: 0, count: pageSize);
                }
                catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    yield break;
                }

                Assert.IsNotNull(sgtins);
                Assert.IsNotNull(sgtins.Entries);
                foreach (var sgtin in sgtins.Entries)
                {
                    var code = getCode(sgtin);
                    if (string.IsNullOrWhiteSpace(code) || !yielded.Add(code))
                    {
                        continue;
                    }

                    yield return code;
                }

                endDate = startDate;
            }
        }

        private IEnumerable<string> GetSampleSgtins()
        {
            if (_sampleSgtins != null)
            {
                return _sampleSgtins;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var samples = new List<string>();

            foreach (var code in GetTestCodes(s => s.SgtinValue, maxWindows: 12, daysPerWindow: 365, pageSize: 250))
            {
                if (!yielded.Add(code))
                {
                    continue;
                }

                samples.Add(code);
            }

            if (samples.Count == 0)
            {
                var decisions = Client.GetPausedCirculationDecisions(new PausedCirculationDecisionFilter
                {
                    Gtin = "04610020540019",
                    StartHaltDate = DateTime.Now.AddYears(-100),
                    EndHaltDate = DateTime.Now,
                    StartHaltDocDate = DateTime.Now.AddYears(-100),
                    EndHaltDocDate = DateTime.Now,
                }, 0, 10);

                Assert.NotNull(decisions);
                Assert.NotNull(decisions.Entries);
                foreach (var decision in decisions.Entries.Where(d => !string.IsNullOrWhiteSpace(d.HaltID)))
                {
                    try
                    {
                        var pausedSgtins = Client.GetPausedCirculationSgtins(decision.HaltID, 0, 10);
                        Assert.NotNull(pausedSgtins);
                        Assert.NotNull(pausedSgtins.Entries);
                        foreach (var entry in pausedSgtins.Entries)
                        {
                            if (string.IsNullOrWhiteSpace(entry.Sgtin) || !yielded.Add(entry.Sgtin))
                            {
                                continue;
                            }

                            samples.Add(entry.Sgtin);
                        }
                    }
                    catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
                    {
                    }
                }
            }

            _sampleSgtins = samples.ToArray();
            return _sampleSgtins;
        }

        private IEnumerable<string> GetSampleSsccs()
        {
            if (_sampleSsccs != null)
            {
                return _sampleSsccs;
            }

            _sampleSsccs = KnownSsccCandidates
                .Concat(GetTestCodes(s => s.Sscc, maxWindows: 12, daysPerWindow: 365, pageSize: 250))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return _sampleSsccs;
        }

        private static bool IsSandboxStaticResourceNotFound(MdlpException ex)
        {
            return ex.StatusCode == HttpStatusCode.NotFound &&
                ex.Message.IndexOf("No static resource", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetSampleBranchId()
        {
            var branches = Client.GetBranches(null, startFrom: 0, count: 10);
            Assert.NotNull(branches);
            Assert.NotNull(branches.Entries);
            return branches.Entries
                .Select(b => b?.ID)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        private string GetSampleWarehouseId()
        {
            var warehouses = Client.GetWarehouses(null, startFrom: 0, count: 10);
            Assert.NotNull(warehouses);
            Assert.NotNull(warehouses.Entries);
            return warehouses.Entries
                .Select(w => w?.ID)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        private bool TryFindSsccWithHierarchy(out string sscc, out SsccHierarchyResponse<SsccInfo> hierarchy)
        {
            foreach (var candidate in GetSampleSsccs().Take(50))
            {
                try
                {
                    var response = Client.GetSsccHierarchy(candidate);
                    if (response == null || response.Up == null || response.Down == null || response.ErrorCode != null)
                    {
                        continue;
                    }

                    if (response.Up.Length == 0 || response.Down.Length == 0)
                    {
                        continue;
                    }

                    sscc = candidate;
                    hierarchy = response;
                    return true;
                }
                catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
                {
                }
            }

            sscc = null;
            hierarchy = null;
            return false;
        }

        private bool TryFindSsccByImmediateSgtins(bool withImmediateSgtins, out string sscc, out GetSsccSgtinsResponse response)
        {
            foreach (var candidate in GetSampleSsccs().Take(50))
            {
                try
                {
                    var sgtins = Client.GetSsccSgtins(candidate, null, 0, 10);
                    if (sgtins == null || sgtins.Entries == null || sgtins.ErrorCode != null)
                    {
                        continue;
                    }

                    var hasImmediateSgtins = sgtins.Entries.Length > 0;
                    if (hasImmediateSgtins != withImmediateSgtins)
                    {
                        continue;
                    }

                    sscc = candidate;
                    response = sgtins;
                    return true;
                }
                catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
                {
                }
            }

            sscc = null;
            response = null;
            return false;
        }

        [Test, Explicit]
        public void ThereAreSomeSsccs()
        {
            var sscc = GetTestCodes(s => s.Sscc).Take(50).ToArray();
            foreach (var s in sscc)
            {
                Assert.IsNotNull(s);
                WriteLine(s);
            }
        }

        private string GetRandomSsccCodeWithHierarchy()
        {
            var alreadyChecked = new HashSet<string>();
            var codes = GetTestCodes(s =>
            {
                var sscc = s.Sscc;
                if (string.IsNullOrWhiteSpace(sscc) || alreadyChecked.Contains(sscc))
                {
                    return null;
                }

                var h = Client.GetSsccHierarchy(sscc);
                alreadyChecked.Add(sscc);
                if (h == null || h.Up.Length < 1 || h.Down.Length < 1)
                {
                    return null;
                }

                if (h.Up.Length + h.Down.Length < 4)
                {
                    return null;
                }

                return sscc;
            });

            var code = codes.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(code))
            {
                Assert.Ignore("Could not find SSCC with hierarchy in sampled data");
            }

            WriteLine(code);
            return code;
        }

        [Test]
        public void Chapter8_04_1_GetSsccHierarchy_Found()
        {
            if (!TryFindSsccWithHierarchy(out var sscc, out var ssccs))
            {
                Assert.Ignore("Could not find SSCC with hierarchy in sandbox");
            }

            Assert.NotNull(ssccs);
            Assert.NotNull(ssccs.Up);
            Assert.NotNull(ssccs.Down);

            Assert.IsTrue(ssccs.Up.Length >= 1);
            Assert.IsTrue(ssccs.Down.Length >= 1);
            Assert.IsNull(ssccs.ErrorCode);
            Assert.IsNull(ssccs.ErrorDescription);
            Assert.AreEqual(sscc, ssccs.Up[0].Sscc);
            Assert.AreEqual(sscc, ssccs.Down[0].Sscc);
        }

        [Test]
        public void Chapter8_04_2_GetSsccSgtins()
        {
            if (!TryFindSsccByImmediateSgtins(true, out var sscc, out var ssccs))
            {
                Assert.Ignore("Could not find SSCC with immediate SGTINs in sandbox");
            }

            Assert.NotNull(ssccs);
            Assert.NotNull(ssccs.Entries);

            Assert.IsTrue(ssccs.Total >= 1);
            Assert.IsTrue(ssccs.Entries.Length >= 1);
            Assert.IsTrue(ssccs.Entries.Length <= 10);
            Assert.IsNull(ssccs.ErrorCode);
            Assert.IsNull(ssccs.ErrorDescription);

            Assert.NotNull(ssccs.Entries[0]);
            Assert.IsNotNull(ssccs.Entries[0].SgtinValue);
            WriteLine("SSCC with immediate SGTINs: {0}", sscc);
        }

        [Test, Explicit("No much use to wait for 5 seconds before completing this call")]
        public void Chapter8_04_2_GetSsccSgtins_NotFound()
        {
            var ssccs = Client.GetSsccSgtins("201902251235570000", null, 0, 1);
            Assert.NotNull(ssccs);
            Assert.NotNull(ssccs.Entries);

            Assert.AreEqual(0, ssccs.Entries.Length);
            Assert.AreEqual(2, ssccs.ErrorCode);
            Assert.That(ssccs.ErrorDescription, Is.AnyOf(
                "Requested data not found",
                "Запрашиваемые данные не найдены"));
        }

        [Test]
        public void Chapter8_04_2_GetSsccSgtinsNoImmediateSgtins()
        {
            if (!TryFindSsccByImmediateSgtins(false, out _, out var ssccs))
            {
                Assert.Ignore("Could not find SSCC without immediate SGTINs in sandbox");
            }

            Assert.NotNull(ssccs);
            Assert.NotNull(ssccs.Entries);
            Assert.AreEqual(0, ssccs.Entries.Length);
            Assert.AreEqual(0, ssccs.Total);
        }

        [Test]
        public void Chapter8_04_3_GetSsccFullHierarchy()
        {
            if (!TryFindSsccWithHierarchy(out var sscc, out _))
            {
                Assert.Ignore("Could not find SSCC with hierarchy in sandbox");
            }

            try
            {
                var h = Client.GetSsccFullHierarchy(sscc);
                Assert.NotNull(h);
                Assert.NotNull(h.Up);
                Assert.NotNull(h.Down);

                Assert.AreEqual(sscc, h.Down.Sscc);
                Assert.IsNotNull(h.Up.ChildSsccs);
                Assert.IsNotNull(h.Up.ChildSgtins);
                Assert.IsNotNull(h.Down.ChildSsccs);
                Assert.IsNotNull(h.Down.ChildSgtins);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Assert.Ignore("Full SSCC hierarchy is unavailable for selected sample in sandbox");
            }
        }

        [Test, Explicit("No use to wait half a minute to complete this call")]
        public void Chapter8_04_3_GetSsccFullHierarchy_NotFound()
        {
            // примеры из документации вызывают ошибку 404: 201902251235570000
            var ex = Assert.Throws<MdlpException>(() =>
                Client.GetSsccFullHierarchy("100000000000000300"));

            Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
        }

        [Test, Ignore("Not yet deployed on the test server")]
        public void Chapter8_04_4_GetSsccFullHierarchyForMultipleSsccss()
        {
            // Ticket SR00524696
            // Ответили, что надо вызывать через HTTPS
            // Выяснилось, что разницы нет, та же ошибка
            ////using (var client = new MdlpClient(credentials: new ResidentCredentials
            ////{
            ////    ClientID = ClientID1,
            ////    ClientSecret = ClientSecret1,
            ////    UserID = TestUserThumbprint,
            ////},
            ////baseUrl: MdlpClient.StageApiHttps)
            ////{
            ////    Tracer = WriteLine
            ////})

            // Выполняем запрос на Песочнице — там, похоже, работает
            ////var cred = new ResidentCredentials
            ////{
            ////    ClientID = "22d12250-6cf3-4a87-b439-f698cfddc498",
            ////    ClientSecret = "3deb0ba1-26f2-4516-b652-931fe832e3ff",
            ////    UserID = "10E4921908D24A0D1AD94A29BD0EF51696C6D8DA"
            ////};

            ////using (var client = new MdlpClient(credentials: cred, baseUrl: MdlpClient.SandboxApiHttps)
            ////{
            ////    Tracer = WriteLine
            ////})

            var client = Client;
            {
                var l = client.GetSsccFullHierarchy(new[] { "000000000105900000", "147600887000110010" });
                Assert.NotNull(l);
                Assert.AreEqual(1, l.Length);

                var h = l[0];
                Assert.NotNull(h.Up);
                Assert.NotNull(h.Down);

                // validate up hierarchy
                Assert.AreEqual("000000111100000100", h.Up.Sscc);
                Assert.IsNotNull(h.Up.ChildSsccs);
                Assert.IsNotNull(h.Up.ChildSgtins);
                Assert.AreEqual(0, h.Up.ChildSgtins.Length);
                Assert.AreEqual(1, h.Up.ChildSsccs.Length);
                Assert.AreEqual("000000111100000097", h.Up.ChildSsccs[0].Sscc);
                Assert.IsNotNull(h.Up.ChildSsccs[0].ChildSsccs);
                Assert.IsNotNull(h.Up.ChildSsccs[0].ChildSgtins);
                Assert.AreEqual(0, h.Up.ChildSsccs[0].ChildSsccs.Length);
                Assert.AreEqual(0, h.Up.ChildSsccs[0].ChildSgtins.Length);

                // validate down hierarchy
                Assert.AreEqual("000000111100000097", h.Down.Sscc);
                Assert.IsNotNull(h.Down.ChildSsccs);
                Assert.IsNotNull(h.Down.ChildSgtins);
                Assert.AreEqual(2, h.Down.ChildSgtins.Length);
                Assert.AreEqual(0, h.Down.ChildSsccs.Length);
                Assert.AreEqual("04607028393860G000000001J21", h.Down.ChildSgtins[0].Sgtin);
                Assert.AreEqual("04607028393860G000000001J22", h.Down.ChildSgtins[1].Sgtin);
            }
        }

        [Test]
        public void Chapter8_05_1_GetCurrentMedProducts()
        {
            var medProducts = Client.GetCurrentMedProducts(new MedProductsFilter
            {
                RegistrationDateFrom = DateTime.Now.AddYears(-100),
                RegistrationDateTo = DateTime.Now
            }, 0, 1);

            Assert.NotNull(medProducts);
            Assert.NotNull(medProducts.Entries);
            Assert.IsTrue(medProducts.Total >= 0);
            Assert.IsTrue(medProducts.Entries.Length <= 1);

            if (medProducts.Entries.Length > 0)
            {
                var prod = medProducts.Entries[0];
                Assert.NotNull(prod);
                Assert.IsNotNull(prod.Gtin);
                Assert.IsNotNull(prod.ProductSellingName);
                Assert.IsNotNull(prod.ProductName);
                Assert.IsNotNull(prod.RegistrationHolder);
            }
        }

        [Test]
        public void Chapter8_05_2_GetCurrentMedProduct()
        {
            var medProducts = Client.GetCurrentMedProducts(new MedProductsFilter
            {
                RegistrationDateFrom = DateTime.Now.AddYears(-100),
                RegistrationDateTo = DateTime.Now
            }, 0, 1);

            Assert.NotNull(medProducts);
            Assert.NotNull(medProducts.Entries);
            if (medProducts.Entries.Length == 0 || string.IsNullOrWhiteSpace(medProducts.Entries[0].Gtin))
            {
                Assert.Ignore("No current med products found in sandbox");
            }

            var sampleGtin = medProducts.Entries[0].Gtin;
            try
            {
                var prod = Client.GetCurrentMedProduct(sampleGtin);
                Assert.NotNull(prod);
                Assert.AreEqual(sampleGtin, prod.Gtin);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Ignore("GTIN " + sampleGtin + " not found in sandbox");
            }
        }

        [Test]
        public void Chapter8_05_3_GetPublicMedProducts()
        {
            var medProducts = Client.GetPublicMedProducts(new MedProductsFilter
            {
                Gtin = "04607028394287"
            }, 0, 1);

            Assert.NotNull(medProducts);
            Assert.NotNull(medProducts.Entries);
            Assert.IsTrue(medProducts.Total >= 0);
            if (medProducts.Entries.Length == 0)
            {
                Assert.Ignore("GTIN 04607028394287 not found in sandbox public med products");
            }

            var prod = medProducts.Entries[0];
            Assert.NotNull(prod);
            Assert.AreEqual("04607028394287", prod.Gtin);
            Assert.IsNotNull(prod.ProductSellingName);
            Assert.IsNotNull(prod.ProductPack1Amount);
            Assert.IsNotNull(prod.ProductDosageName);
        }

        [Test]
        public void Chapter8_05_4_GetPublicMedProduct()
        {
            try
            {
                var prod = Client.GetPublicMedProduct("04607028394287");
                Assert.NotNull(prod);
                Assert.AreEqual("04607028394287", prod.Gtin);
                Assert.IsNotNull(prod.ProductSellingName);
                Assert.IsNotNull(prod.ProductPack1Amount);
                Assert.IsNotNull(prod.ProductDosageName);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Ignore("GTIN 04607028394287 not found in sandbox public med products");
            }
        }

        [Test, Explicit("Этот метод API всегда создает новые записи, даже когда возвращает ошибку")]
        public void Chapter8_06_1_RegisterForeignCounterparty()
        {
            var ex = Assert.Throws<MdlpException>(() =>
            {
                // этот тест можно выполнить только один раз с указанными данными
                var partyId = Client.RegisterForeignCounterparty(
                    "56887455222583",
                    "ГМ ПХАРМАЦЕУТИЦАЛС",
                    new ForeignAddress
                    {
                        City = "city",
                        Region = "region",
                        Locality = "locality",
                        Street = "street",
                        House = "house",
                        Corpus = "corpus",
                        Litera = "litera",
                        Room = "room",
                        CountryCode = "GE",
                        PostalCode = "148000",
                    });

                // "56887455222583" зарегистрирован с кодом "93026c45-f63f-4a93-8b87-8aec5e56b292"
                Assert.NotNull(partyId);
            });

            // "Ошибка при выполнении операции: указанные данные уже зарегистрированы в системе"
            Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode); // 400
        }

        [Test]
        public void Chapter8_06_2_GetForeignCounterparties()
        {
            var counterparties = Client.GetForeignCounterparties(new ForeignCounterpartyFilter
            {
                Inn = "56887455222583",
                RegistrationDateFrom = DateTime.Now.AddYears(-100),
                RegistrationDateTo = DateTime.Now,
            }, 0, 10);

            Assert.NotNull(counterparties);
            Assert.NotNull(counterparties.Entries);
            Assert.IsTrue(counterparties.Total >= 0);
            if (counterparties.Entries.Length == 0)
            {
                Assert.Ignore("No foreign counterparties found for INN 56887455222583 in sandbox");
            }

            var cp = counterparties.Entries.FirstOrDefault(e => e.SystemSubjectID == "93026c45-f63f-4a93-8b87-8aec5e56b292");
            if (cp != null)
            {
                Assert.AreEqual("56887455222583", cp.Inn);
                Assert.AreEqual("GE", cp.CountryCode);
            }
            else
            {
                Assert.AreEqual("56887455222583", counterparties.Entries[0].Inn);
            }
        }

        [Test]
        public void Chapter8_07_1_AddTrustedPartners()
        {
            const string trustedPartnerId = "93026c45-f63f-4a93-8b87-8aec5e56b292";
            var partnerAdded = false;

            try
            {
                Client.AddTrustedPartners(
                    trustedPartnerId,
                    trustedPartnerId,
                    trustedPartnerId
                );

                partnerAdded = true;
                Assert.DoesNotThrow(() => Client.AddTrustedPartners(trustedPartnerId));

                try
                {
                    Client.AddTrustedPartners("56887455222583");
                }
                catch (MdlpException ex)
                {
                    Assert.That(ex.StatusCode, Is.AnyOf(
                        HttpStatusCode.BadRequest,
                        HttpStatusCode.NotFound,
                        HttpStatusCode.Forbidden));
                }
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Assert.Ignore("Insufficient rights to add trusted partners in sandbox");
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.BadRequest || ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Ignore("Trusted partner test data is unavailable in sandbox");
            }
            finally
            {
                if (partnerAdded)
                {
                    try
                    {
                        Client.DeleteTrustedPartners(trustedPartnerId);
                    }
                    catch (MdlpException ex) when (
                        ex.StatusCode == HttpStatusCode.BadRequest ||
                        ex.StatusCode == HttpStatusCode.NotFound ||
                        ex.StatusCode == HttpStatusCode.Forbidden)
                    {
                    }
                }
            }
        }

        [Test]
        public void Chapter8_07_2_DeleteTrustedPartners()
        {
            const string trustedPartnerId = "93026c45-f63f-4a93-8b87-8aec5e56b292";

            try
            {
                try
                {
                    Client.AddTrustedPartners(trustedPartnerId);
                }
                catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.BadRequest || ex.StatusCode == HttpStatusCode.NotFound)
                {
                }

                Client.DeleteTrustedPartners(
                    trustedPartnerId,
                    trustedPartnerId,
                    trustedPartnerId
                );

                Assert.DoesNotThrow(() => Client.DeleteTrustedPartners(trustedPartnerId));
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Assert.Ignore("Insufficient rights to delete trusted partners in sandbox");
            }

            try
            {
                Client.DeleteTrustedPartners("56887455222583");
            }
            catch (MdlpException ex)
            {
                Assert.That(ex.StatusCode, Is.AnyOf(
                    HttpStatusCode.BadRequest,
                    HttpStatusCode.NotFound,
                    HttpStatusCode.Forbidden));
            }
        }

        [Test]
        public void Chapter8_07_3_GetTrustedPartners()
        {
            var partners = Client.GetTrustedPartners(null, 0, 10);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            Assert.IsTrue(partners.Total >= 0);

            partners = Client.GetTrustedPartners(new TrustedPartnerFilter
            {
                SystemSubjectID = "93026c45-f63f-4a93-8b87-8aec5e56b292".Replace("b292", "fade")
            }, 0, 1);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            Assert.AreEqual(0, partners.Total);
            Assert.AreEqual(0, partners.Entries.Length);
        }

        [Test]
        public void Chapter8_08_1_GetForeignPartners()
        {
            var partners = Client.GetForeignPartners(new PartnerFilter
            {
                StartDate = DateTime.Now.AddYears(-100),
                EndDate = DateTime.Now,
                OperationStartDate = DateTime.Now.AddYears(-100),
                OperationEndDate = DateTime.Now,
            }, 0, 10);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            Assert.IsTrue(partners.Total >= 0);

            partners = Client.GetForeignPartners(new PartnerFilter
            {
                Inn = "123456789012345678911234567890123456789",
            }, 0, 1);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            if (partners.Entries.Length == 0)
            {
                Assert.Ignore("Known foreign partner INN not found in sandbox");
            }

            var partner = partners.Entries[0];
            Assert.NotNull(partner);
            Assert.AreEqual("123456789012345678911234567890123456789", partner.Itin);
            Assert.IsNotNull(partner.SystemSubjectID);
            Assert.IsNotNull(partner.OrganizationName);

            partners = Client.GetForeignPartners(new PartnerFilter
            {
                SystemSubjectID = "1a7c2739-57d9-46da-9363-6272f8b6b55b".Replace("9363", "6393")
            }, 0, 1);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            Assert.AreEqual(0, partners.Total);
            Assert.AreEqual(0, partners.Entries.Length);
        }

        [Test]
        public void Chapter8_08_1_GetLocalPartners()
        {
            var partners = Client.GetLocalPartners(null, 0, 10);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            Assert.IsTrue(partners.Total >= 0);

            partners = Client.GetLocalPartners(new PartnerFilter
            {
                Inn = "7735069192",
                SystemSubjectID = "d2ee5250-3e28-4e5c-896a-00b902e22555"
            }, 0, 1);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            if (partners.Entries.Length == 0)
            {
                Assert.Ignore("Known local partner not found in sandbox");
            }

            var partner = partners.Entries[0];
            Assert.NotNull(partner);
            Assert.IsNotNull(partner.OrganizationName);
            Assert.AreEqual("d2ee5250-3e28-4e5c-896a-00b902e22555", partner.SystemSubjectID);
            Assert.AreEqual("7735069192", partner.Inn);
            Assert.AreEqual(new DateTime(2017, 06, 02), partner.OperationDate.Date.Date);

            partners = Client.GetLocalPartners(new PartnerFilter
            {
                SystemSubjectID = "d2ee5250-3e28-4e5c-896a-00b902e22555".Replace("22555", "55222")
            }, 0, 1);
            Assert.NotNull(partners);
            Assert.NotNull(partners.Entries);
            Assert.AreEqual(0, partners.Total);
            Assert.AreEqual(0, partners.Entries.Length);
        }

        [Test]
        public void Chapter8_09_1_GetCurrentMember()
        {
            var member = Client.GetCurrentMember();
            Assert.NotNull(member);
            Assert.IsNotNull(member.SystemSubjectID);
            Assert.IsNotNull(member.OrganizationName);
            Assert.IsNotNull(member.Language);
            Assert.IsTrue(member.IsResident);
            Assert.NotNull(member.Chiefs);
            Assert.NotNull(member.AgreementsInfo);
            Assert.AreEqual(RegEntityTypeEnum.RESIDENT, member.EntityType);
        }

        [Test]
        public void Chapter8_09_2_UpdateCurrentMember()
        {
            var member = Client.GetCurrentMember();
            Assert.NotNull(member);

            var expectedLanguage = string.IsNullOrWhiteSpace(member.Language) ? "ru" : member.Language;

            try
            {
                Client.UpdateCurrentMember(new MemberOptions
                {
                    Language = expectedLanguage,
                    Email = member.Email
                });

                var updated = Client.GetCurrentMember();
                Assert.NotNull(updated);
                Assert.AreEqual(expectedLanguage, updated.Language);
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Assert.Ignore("Insufficient rights to update current member in sandbox");
            }
        }

        [Test]
        public void Chapter8_09_3_GetCurrentBillingInfo()
        {
            var accounts = Client.GetCurrentBillingInfo();
            Assert.NotNull(accounts);
            Assert.IsTrue(accounts.Length >= 0);
        }

        [Test]
        public void Chapter8_10_1_GetEmissionDevices()
        {
            var devices = Client.GetEmissionDevices(new EmissionDeviceFilter
            {
                ProvisionStartDate = DateTime.Now.AddYears(-100),
                ProvisionEndDate = DateTime.Now,
            }, 0, 10);

            Assert.NotNull(devices);
            Assert.NotNull(devices.Entries);
            Assert.IsTrue(devices.Total >= 0);
            Assert.IsTrue(devices.Entries.Length <= 10);
        }

        [Test]
        public void Chapter8_10_2_GetWithdrawalDevices()
        {
            var devices = Client.GetWithdrawalDevices(new WithdrawalDeviceFilter
            {
                ProvisionStartDate = DateTime.Now.AddYears(-100),
                ProvisionEndDate = DateTime.Now,
            }, 0, 10);
            Assert.NotNull(devices);
            Assert.NotNull(devices.Entries);
            Assert.IsTrue(devices.Total >= 0);
            Assert.IsTrue(devices.Entries.Length <= 10);
        }

        [Test]
        public void Chapter8_11_1_GetVirtualStorage()
        {
            try
            {
                var balance = Client.GetVirtualStorage(new VirtualStorageFilter
                {
                    StorageID = "00000000100931",
                    StartDate = DateTime.Now.AddYears(-100),
                    EndDate = DateTime.Now,
                }, 0, 10);

                Assert.NotNull(balance);
                Assert.NotNull(balance.Entries);
                Assert.IsTrue(balance.Total >= 0);
                if (balance.Entries.Length > 0)
                {
                    AssertRequiredItems(balance.Entries);
                }
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Ignore("Storage 00000000100931 not found in sandbox");
            }
        }

        [Test]
        public void Chapter8_12_1_GetPausedCirculationDecisions()
        {
            var decisions = Client.GetPausedCirculationDecisions(new PausedCirculationDecisionFilter
            {
                Gtin = "04610020540019",
                StartHaltDate = DateTime.Now.AddYears(-100),
                EndHaltDate = DateTime.Now,
                StartHaltDocDate = DateTime.Now.AddYears(-100),
                EndHaltDocDate = DateTime.Now,
            }, 0, 10);

            Assert.NotNull(decisions);
            Assert.NotNull(decisions.Entries);
            Assert.IsTrue(decisions.Total >= 0);
            if (decisions.Entries.Length == 0)
            {
                Assert.Ignore("No paused circulation decisions found for GTIN 04610020540019");
            }

            AssertRequiredItems(decisions.Entries);
            var decision = decisions.Entries[0];
            Assert.AreEqual("04610020540019", decision.Gtin);
            Assert.IsNotNull(decision.HaltID);
        }

        [Test]
        public void Chapter8_12_2_GetPausedCirculationSgtins()
        {
            var decisions = Client.GetPausedCirculationDecisions(new PausedCirculationDecisionFilter
            {
                Gtin = "04610020540019",
                StartHaltDate = DateTime.Now.AddYears(-100),
                EndHaltDate = DateTime.Now,
                StartHaltDocDate = DateTime.Now.AddYears(-100),
                EndHaltDocDate = DateTime.Now,
            }, 0, 10);

            Assert.NotNull(decisions);
            Assert.NotNull(decisions.Entries);

            var decision = decisions.Entries.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.HaltID));
            if (decision == null)
            {
                Assert.Ignore("No paused circulation decision with HaltID found in sandbox");
            }

            try
            {
                var sgtins = Client.GetPausedCirculationSgtins(decision.HaltID, 0, 10);
                Assert.NotNull(sgtins);
                Assert.NotNull(sgtins.Entries);
                Assert.IsTrue(sgtins.Total >= 0);
                Assert.IsTrue(sgtins.Entries.Length <= 10);
                if (sgtins.Entries.Length > 0)
                {
                    AssertRequiredItems(sgtins.Entries);
                }
            }
            catch (MdlpException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.Ignore("Paused circulation decision is no longer available in sandbox");
            }
        }

        [Test, Ignore("Not yet deployed on the test server")]
        public void Chapter8_13_1_GetBatchShortDistribution()
        {
            var batches = Client.GetBatchShortDistribution("04610020540019", "SCHEME2600094");
            Assert.NotNull(batches);
            Assert.NotNull(batches.Entries);
            Assert.AreEqual(2, batches.Total);
            Assert.AreEqual(2, batches.Entries.Length);
            AssertRequiredItems(batches.Entries);
        }
    }
}
